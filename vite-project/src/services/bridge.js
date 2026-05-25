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
 * Receives state pushes from the DLL via WebView2 PostWebMessageAsJson.
 */
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (e) => {
        try {
            const data = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
            if (!data || typeof data !== 'object') return;

            // 1. Resolve Promise for RPC calls
            if (data.callId && pendingCalls.has(data.callId)) {
                const resolve = pendingCalls.get(data.callId);
                pendingCalls.delete(data.callId);
                const result = (data.result && typeof data.result === 'object')
                    ? createCaseInsensitiveProxy(data.result)
                    : data.result;
                resolve(result);
                return;
            }

            // 2. Global State Push
            if (data.status === 'ok' && data.hasOwnProperty('result') && data.result && typeof data.result === 'object') {
                stateChangeCallback(createCaseInsensitiveProxy(data.result));
            } else {
                stateChangeCallback(createCaseInsensitiveProxy(data));
            }
        } catch (e) {
            console.error("[Bridge] Message Error:", e);
        }
    });
}

/**
 * Hook for Vue components to listen for state changes.
 */
export const onStateChanged = (callback) => {
    stateChangeCallback = callback;
};
