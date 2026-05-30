using System.Globalization;

namespace ImmersiveDisplay.Helpers;

public enum DialogKey
{
    // Errors
    StartMonitoringError,
    CopyImageFailed,

    // Warnings
    CommandLinePermission,
    CommandLinePermissionTitle,
    WindowStylePermission,
    WindowStylePermissionTitle,
    LayoutMismatch,
    LayoutMismatchTitle,

    // Info
    AboutTitle,
    AboutMessage,

    // File dialogs
    SelectBackgroundImage,
    SelectIconFile,
    SelectApplication,
}

public static class DialogMessages
{
    private static readonly Dictionary<string, string> Zh = new()
    {
        ["StartMonitoringError"]        = "启动监听时发生错误：{0}",
        ["CopyImageFailed"]             = "无法将图片复制到背景文件夹。",
        ["CommandLinePermissionTitle"]  = "权限提示",
        ["CommandLinePermission"]       = "权限不足，无法获取目标进程的启动命令行参数（Launch Arguments）。\n\n当前已自动降级为仅获取程序执行文件路径。若要抓取完整的启动参数，请以【管理员身份】重新运行本工具。",
        ["WindowStylePermissionTitle"]  = "权限不足",
        ["WindowStylePermission"]       = "无法修改目标窗口样式。\n\n这通常是因为目标程序（游戏）是以管理员权限运行的，而本工具权限不足。\n\n请尝试【以管理员身份运行】本工具后再试。\n\n(错误信息: {0})",
        ["LayoutMismatchTitle"]         = "布局同步异常",
        ["LayoutMismatch"]              = "检测到连续窗口布局同步失败。\n\n这可能是因为【同步显示器设置】未开启，导致窗口尺寸无法匹配当前屏幕方向。\n\n请在【目标进程】设置中开启「同步显示器设置」后重试。\n\n也可尝试以【管理员身份】运行本工具。",
        ["AboutTitle"]                  = "关于",
        ["AboutMessage"]                = "显示同步控制 v2.8.0\n\nGitHub: https://github.com/yumiaomaio/DisplayTrackTool",
        ["SelectBackgroundImage"]       = "选择背景图片",
        ["SelectIconFile"]              = "选择图标文件",
        ["SelectApplication"]           = "选择应用程序或快捷方式",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["StartMonitoringError"]        = "An error occurred while starting monitoring: {0}",
        ["CopyImageFailed"]             = "Failed to copy image to backgrounds folder.",
        ["CommandLinePermissionTitle"]  = "Permission Warning",
        ["CommandLinePermission"]       = "Insufficient permissions to capture process startup arguments.\n\nFalling back to executable path only. To capture complete launch parameters, please restart this tool as Administrator.",
        ["WindowStylePermissionTitle"]  = "Privilege Elevation Required",
        ["WindowStylePermission"]       = "Unable to modify target window styles.\n\nThis is usually because the target program is running with administrator privileges, while this tool lacks sufficient permissions.\n\nPlease try running this tool as Administrator.\n\n(Error: {0})",
        ["LayoutMismatchTitle"]         = "Layout Sync Error",
        ["LayoutMismatch"]              = "Consecutive window layout sync failures detected.\n\nThis may be caused by disabled Display Sync, preventing the window size from matching the current display orientation.\n\nPlease enable 'Display Sync' in the Target Process settings and try again.\n\nAlternatively, try running this tool as Administrator.",
        ["AboutTitle"]                  = "About",
        ["AboutMessage"]                = "Display Sync Control v2.8.0\n\nGitHub: https://github.com/yumiaomaio/DisplayTrackTool",
        ["SelectBackgroundImage"]       = "Select a Background Image",
        ["SelectIconFile"]              = "Select Icon File",
        ["SelectApplication"]           = "Select Application or Shortcut",
    };

    private static bool IsChinese =>
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public static string Get(DialogKey key)
    {
        var map = IsChinese ? Zh : En;
        return map.TryGetValue(key.ToString(), out var text) ? text : key.ToString();
    }

    public static string Format(DialogKey key, params string[] args)
    {
        return string.Format(Get(key), args);
    }
}
