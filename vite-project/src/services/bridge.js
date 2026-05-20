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
        EnableTaskbarAutoHide: Promise.resolve(true),
        EnableDisplaySync: Promise.resolve(true),
        EnableBackgroundOverlay: Promise.resolve(true),
        BackgroundColor: Promise.resolve("#FF2D2D30"),
        IsAdmin: Promise.resolve(false),
        IsRunning: Promise.resolve(false),
        CurrentImageFileName: Promise.resolve(""),
        ShouldShowExitTip: Promise.resolve(true),
        AssociatedLaunchPath: Promise.resolve(""),
        LaunchOnAppStartup: Promise.resolve(false),
        LaunchOnTaskStart: Promise.resolve(false),
        AutoStartFromThirdParty: Promise.resolve(false),
        IsProtocolRegistered: Promise.resolve(false),
        GetLogs: () => Promise.resolve(["> Mock: System initialized."]),
        GetImageBase64: () => Promise.resolve(""),
        SetEnableTaskbarAutoHide: (val) => console.log("Mock: SetAutoHide", val),
        SetEnableDisplaySync: (val) => console.log("Mock: SetDisplaySync", val),
        SetEnableBackgroundOverlay: (val) => console.log("Mock: SetOverlay", val),
        SetBackgroundColor: (val) => console.log("Mock: SetColor", val),
        SetShowExitTip: (val) => console.log("Mock: SetShowExitTip", val),
        SetAssociatedLaunchPath: (val) => console.log("Mock: SetLaunchPath", val),
        SetLaunchOnAppStartup: (val) => console.log("Mock: SetLaunchOnAppStartup", val),
        SetLaunchOnTaskStart: (val) => console.log("Mock: SetLaunchOnTaskStart", val),
        SetAutoStartFromThirdParty: (val) => console.log("Mock: SetAutoStartFromThirdParty", val),
        SelectAssociatedProgram: () => console.log("Mock: SelectAssociatedProgram"),
        RestartAsAdmin: () => alert("Mock: Restart as Admin"),
        ShowAbout: () => alert("Mock: Show About"),
        StartMonitoring: (p) => { 
            console.log("Mock: Start Monitoring", p);
        },
        StopMonitoring: () => console.log("Mock: Stop Monitoring"),
        SelectImage: () => console.log("Mock: Select Image"),
        ClearImage: () => console.log("Mock: Clear Image"),
        RegisterProtocol: () => { console.log("Mock: RegisterProtocol"); return Promise.resolve(true); },
        UnregisterProtocol: () => { console.log("Mock: UnregisterProtocol"); return Promise.resolve(true); },
        CleanAssociation: () => { console.log("Mock: CleanAssociation"); return Promise.resolve(true); },
        GetProcessIconBase64: () => Promise.resolve(""),
        CheckProcessExists: () => Promise.resolve(false)
    };
};

export const bridge = getBridge();

/**
 * Hook to listen for state changes from C#
 * @param {Function} callback 
 */
export let onStateChanged = (callback) => {
    window.onStateChanged = (stateOrJson) => {
        try {
            const state = typeof stateOrJson === 'string' ? JSON.parse(stateOrJson) : stateOrJson;
            callback(state);
        } catch (e) {
            console.error("Failed to parse state update from C#", e);
        }
    };
};
