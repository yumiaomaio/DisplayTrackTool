namespace ImmersiveDisplay.Models;

public enum BridgeAction
{
    Unknown = 0,
    GetInitialState,
    
    // --- Monitoring ---
    StartMonitoring,
    StopMonitoring,
    
    // --- Configuration ---
    SetBackgroundColor,
    SetTargetProcessName,
    SetAssociatedLaunchPath,
    SetBackgroundMode,
    SetWindowDetectionTimeout,
    SetShowExitTip,
    
    // --- Toggles ---
    SetEnableTaskbarAutoHide,
    SetEnableDisplaySync,
    SetEnableBackgroundOverlay,
    SetLaunchOnAppStartup,
    SetLaunchOnTaskStart,
    SetAutoStartFromThirdParty,
    SetAutoStartMonitoringOnProtocolLaunch,

    // --- Actions ---
    SelectImage,
    ClearImage,
    SelectAssociatedProgram,
    ClearLogs,
    RestartAsAdmin,
    ExitApp,
    ShowAbout,

    // --- State & Protocols ---
    ShouldShowUacPrompt,
    RegisterProtocol,
    UnregisterProtocol,
    IsProtocolRegistered,
    IsAssociationValid,
    CleanAssociation,
    HandleAppProtocol,

    // --- Icon & URL Registration ---
    SelectIconFile,
    ImportDroppedIcon,
    CreateAssociationUrls,
    QuickRegisterAssociation,
    CleanAllAssociationUrls,
    CreateDesktopShortcut,

    // --- Resources ---
    GetImageBase64,
    GetProcessCommandLine,
    GetProcessIconBase64,
    CheckProcessExists,
    GetLogs
}
