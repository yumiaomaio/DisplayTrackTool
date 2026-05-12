/**
 * Bridge service to handle communication between Vue and C# WPF (WebView2)
 */

const getBridge = () => {
    if (window.chrome && window.chrome.webview && window.chrome.webview.hostObjects) {
        return window.chrome.webview.hostObjects.bridge;
    }
    
    // Mock for local browser development
    console.warn("WebView2 Bridge not found. Using mock implementation.");
    return {
        TargetProcessName: Promise.resolve("notepad.exe"),
        PortraitAspectRatio: Promise.resolve("9/16"),
        EnableTaskbarAutoHide: Promise.resolve(false),
        EnableBackgroundOverlay: Promise.resolve(true),
        BackgroundColor: Promise.resolve("#FF2D2D30"),
        IsAdmin: Promise.resolve(false),
        IsRunning: Promise.resolve(false),
        CurrentImageFileName: Promise.resolve(""),
        ShouldShowExitTip: Promise.resolve(true),
        GetLogs: () => Promise.resolve(["> Mock: System initialized."]),
        GetImageBase64: () => Promise.resolve(""),
        SetEnableTaskbarAutoHide: (val) => console.log("Mock: SetAutoHide", val),
        SetEnableBackgroundOverlay: (val) => console.log("Mock: SetOverlay", val),
        SetPortraitAspectRatio: (val) => console.log("Mock: SetRatio", val),
        SetBackgroundColor: (val) => console.log("Mock: SetColor", val),
        SetShowExitTip: (val) => console.log("Mock: SetShowExitTip", val),
        RestartAsAdmin: () => alert("Mock: Restart as Admin"),
        ShowAbout: () => alert("Mock: Show About"),
        StartMonitoring: (p) => { 
            console.log("Mock: Start Monitoring", p);
            // Simulate state change if needed for testing
        },
        StopMonitoring: () => console.log("Mock: Stop Monitoring"),
        SelectImage: () => console.log("Mock: Select Image"),
        ClearImage: () => console.log("Mock: Clear Image")
    };
};

export const bridge = getBridge();

/**
 * Hook to listen for state changes from C#
 * @param {Function} callback 
 */
export let onStateChanged = (callback) => {
    window.onStateChanged = (stateJson) => {
        try {
            const state = JSON.parse(stateJson);
            callback(state);
        } catch (e) {
            console.error("Failed to parse state update from C#", e);
        }
    };
};
