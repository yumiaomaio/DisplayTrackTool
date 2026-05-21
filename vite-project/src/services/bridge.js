/**
 * Bridge service to handle communication between Vue and C# DLL (via C++ Native Host)
 * Pure RPC Proxy: Case-Insensitive Edition.
 */

const pendingCalls = new Map();

/**
 * Sends a raw JSON request to the C++ host.
 */
function sendRequest(action, payload = null) {
    return new Promise((resolve) => {
        const callId = Math.random().toString(36).substring(7);
        pendingCalls.set(callId, resolve);
        
        const message = JSON.stringify({
            action: action.toLowerCase(), // --- Outgoing: Force Lowercase ---
            payload: payload,
            callId: callId
        });

        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(message);
        } else {
            // Mock processing for browser dev
            setTimeout(() => {
                const resolve = pendingCalls.get(callId);
                if (resolve) {
                    pendingCalls.delete(callId);
                    resolve(null);
                }
            }, 100);
        }
    });
}

/**
 * Pure Dynamic Proxy.
 * ANY property access returns a function that triggers an RPC call.
 */
export const bridge = new Proxy({}, {
    get: function(target, prop) {
        if (typeof prop !== 'string' || prop === 'then' || prop === 'toJSON') {
            return target[prop];
        }

        return function(payload) {
             return sendRequest(prop, payload);
        };
    }
});

let stateChangeCallback = () => {};

/**
 * Creates a case-insensitive proxy for an object.
 * Accessing obj.anyCase will return obj.anycase internally.
 */
function createCaseInsensitiveProxy(obj) {
    if (!obj || typeof obj !== 'object') return obj;

    const normalized = {};
    for (const key in obj) {
        normalized[key.toLowerCase()] = obj[key];
    }

    return new Proxy(normalized, {
        get: (target, prop) => {
            if (typeof prop !== 'string') return target[prop];
            return target[prop.toLowerCase()];
        }
    });
}

/**
 * Receives messages from the DLL (Responses & State Pushes).
 */
window.onStateChangedFromDll = function(dataOrJson) {
    try {
        const data = typeof dataOrJson === 'string' ? JSON.parse(dataOrJson) : dataOrJson;
        
        // 1. Resolve Promise for Method Calls
        if (data.callId && pendingCalls.has(data.callId)) {
            const resolve = pendingCalls.get(data.callId);
            pendingCalls.delete(data.callId);
            
            // Methods can return objects, make them case-insensitive
            const result = (data.result && typeof data.result === 'object') 
                ? createCaseInsensitiveProxy(data.result) 
                : data.result;
            resolve(result);
        }
        
        // 2. Global State Push
        const rawUpdate = (data.status === 'ok' && data.hasOwnProperty('result')) ? data.result : data;
        
        if (rawUpdate && typeof rawUpdate === 'object') {
            // Provide a Case-Insensitive Proxy to the frontend components
            stateChangeCallback(createCaseInsensitiveProxy(rawUpdate));
        }
    } catch (e) {
        console.error("[Bridge] Message Error:", e, dataOrJson);
    }
};

/**
 * Hook for Vue components to listen for state changes.
 */
export const onStateChanged = (callback) => {
    stateChangeCallback = callback;
};
