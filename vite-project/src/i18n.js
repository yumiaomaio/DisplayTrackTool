import { reactive } from 'vue'

export const translations = {
  en: {
    appTitle: 'IMMERSIVE DISPLAY',
    processNameLabel: 'PROCESS NAME',
    processNameDesc: 'Right-click the process in Task Manager and check properties to find the exact executable name.',
    processNotFound: 'Target process not detected. Please ensure it is running.',
    timeoutTitle: 'Process Not Found',
    targetCore: 'Target Core',
    associatedLaunch: 'Associated Launch',
    associatedLaunchDesc: 'Automatically execute a URL scheme or program (with arguments) on startup or task start.',
    associatedLaunchDefaultDesc: 'Set the timing for associated app launch. Multi-select is supported; duplicates are automatically prevented.',
    launchPath: 'URL / PROGRAM PATH',
    launchTiming: 'LAUNCH TIMING',
    tabInvoked: 'INVOKED',
    tabStartup: 'STARTUP',
    tabTask: 'TASK',
    launchOnAppStartup: 'Launch on App Startup',
    launchOnAppStartupDesc: 'Automatically run the specified command when this tool starts.',
    launchOnTaskStart: 'Launch on Task Start',
    launchOnTaskStartDesc: 'Run the command immediately when you manually click Start or press F9.',
    autoStartFromThirdParty: 'Passive Startup',
    autoStartFromThirdPartyDesc: 'Allow this tool to be launched by 3rd-party apps (e.g. Steam, Playnite) via URL protocol.',
    autoStartMonitoringOnProtocolLaunch: 'Auto Start Control',
    autoStartMonitoringOnProtocolLaunchDesc: 'When launched by a 3rd-party app, automatically enter control state after opening the target program.',
    protocolModalTitle: 'Enable Passive Startup?',
    protocolModalMessage: 'This will register a custom URL protocol (immersivedisplay://) and create shortcuts on your Desktop and Start Menu. This allows other programs to launch this tool and start monitoring automatically.',
    protocolConfirm: 'Enable & Register',
    protocolCancel: 'Cancel',
    errorTitle: 'REGISTRATION FAILED',
    protocolRegisterError: 'Failed to register the custom URL protocol or create shortcuts. Please run the tool as Administrator and try again.',
    waitingTitle: 'WAITING FOR WINDOW',
    waitingMessage: 'Target process detected, but no visible window found. Waiting for it to initialize...',
    seconds: 's',
    browse: 'BROWSE...',
    detect: 'DETECT',
    detectCommandLine: 'Detect command line of running target process',
    autoHideTaskbar: 'Auto-hide Taskbar',
    autoHideTaskbarDesc: 'Suspend Windows Taskbar visibility for total immersion. Original settings are restored upon termination.',
    displaySync: 'Sync Monitor Settings',
    displaySyncDesc: 'Automatically rotate physical monitor and adjust resolution when window orientation changes.',
    visualOverlay: 'Backing Background',
    visualOverlayDesc: 'Inject a static color or texture layer beneath the target process to fill display gaps.',
    enableOverlay: 'Enable Overlay',
    bgMode: 'Background Mode',
    solidColor: 'Solid Color',
    bgImage: 'Background Image',
    pickPreset: 'PICK PRESET OR CUSTOM',
    renderSource: 'RENDER SOURCE',
    import: 'IMPORT',
    clear: 'CLEAR',
    activeOverride: 'MONITORING ACTIVE',
    targetProcess: 'TARGET PROCESS',
    sysLog: 'SYS.LOG',
    initialize: 'START',
    stop: 'STOP',
    exitTip: {
      title: 'EXIT SHORTCUT',
      message: 'Monitoring active. Press F12 to stop and exit immersive mode. You can also press F9 to start monitoring.',
      dontShowAgain: "Don't show this again",
      gotIt: 'GOT IT'
    },
    uac: {
      title: 'PERMISSION DENIED',
      message: 'System requires Administrative Privileges (UAC) to inject hooks into target processes. Please restart the application as Administrator.',
      button: 'RESTART WITH ELEVATION',
      secondaryButton: 'CONTINUE WITHOUT PRIVILEGE'
    },
    menu: {
      theme: 'Theme',
      dark: 'Dark Mode',
      light: 'Light Mode',
      language: 'Language',
      about: 'About',
      registerAssociation: 'REGISTER ASSOCIATION',
      cleanAssociation: 'Clean Association',
      aboutTitle: 'SYSTEM INFORMATION',
      aboutText: 'Immersive Window Control v1.0\nA hardcore UI prototype for window management.\nDeveloped for precision display override.',
      cancel: 'CANCEL',
      close: 'CLOSE'
    },
    share: 'CREATE SHORTCUT',
    registerModal: {
      title: 'Register Association',
      message: 'This will create a desktop/start menu shortcut for launching via custom protocol. Would you like to use a custom .ico icon? Select "Yes" to customize, or "No" to use the default program icon.',
      yes: 'Yes',
      no: 'No (default)'
    },
    iconRegistration: {
      title: 'ICON REGISTRATION',
      dragHere: 'DROP .ICO FILE HERE',
      clickToSelect: 'or click to browse',
    },
    urlCreation: {
      title: 'URL SHORTCUT CREATION',
      urlNameLabel: 'SHORTCUT FILENAME',
      locationLabel: 'CREATE LOCATION',
      startMenu: 'Start Menu',
      desktop: 'Desktop',
      urlNameHint: 'The shortcut file name. Renaming to something different from the original can force Windows to flush the icon cache and pick up changes immediately.',
      create: 'CREATE SHORTCUTS',
    },
    logs: {
      initialized: '> System control initialized.',
      awaiting: '> Awaiting config data...',
      injecting: (name) => `> Injecting hook into [${name}]...`,
      active: '> Control Active.',
      terminated: '> Control terminated.',
      standby: '> System returned to standby.'
    }
  },
  zh: {
    appTitle: '沉浸显示',
    processNameLabel: '进程名称',
    processNameDesc: '请在任务管理器中右键目标进程，查看“属性”以获取准确的可执行文件名（如：Target.exe）。',
    processNotFound: '未检测到目标进程，请确认该程序已运行。',
    timeoutTitle: '未找到进程',
    targetCore: '核心目标',
    associatedLaunch: '关联启动',
    associatedLaunchDesc: '在应用启动或任务开启时，自动执行指定的 URL Scheme 或程序路径。',
    associatedLaunchDefaultDesc: '设置关联应用启动的时机，可以多选但不会重复关联启动。',
    launchPath: 'URL / 程序路径',
    launchTiming: '启动时机',
    tabInvoked: '调起时',
    tabStartup: '启动时',
    tabTask: '执行时',
    launchOnAppStartup: '应用启动时执行',
    launchOnAppStartupDesc: '在本工具启动时，自动执行上述关联程序。',
    launchOnTaskStart: '任务开启时执行',
    launchOnTaskStartDesc: '点击“启动”按钮或按 F9 开始监控时，立即执行上述程序。',
    autoStartFromThirdParty: '被动启动时执行',
    autoStartFromThirdPartyDesc: '允许通过 URL 协议被三方程序（如 Steam、Playnite）调起。',
    autoStartMonitoringOnProtocolLaunch: '自动开始控制',
    autoStartMonitoringOnProtocolLaunchDesc: '通过第三方应用启动时，会在打开目标程序后自动进入控制状态。',
    protocolModalTitle: '是否开启被动启动？',
    protocolModalMessage: '这将注册自定义 URL 协议 (immersivedisplay://) 并在桌面和开始菜单创建快捷方式。允许其他程序自动调起本工具并开启监控。',
    protocolConfirm: '确认开启并注册',
    protocolCancel: '取消',
    errorTitle: '注册失败',
    protocolRegisterError: '自定义 URL 协议或快捷方式创建失败。请尝试以管理员身份运行本工具并重试。',
    waitingTitle: '等待窗口中',
    waitingMessage: '已检测到目标进程，但尚未找到可见窗口。正在等待初始化...',
    seconds: '秒',
    browse: '浏览...',
    detect: '检测',
    detectCommandLine: '从当前运行的目标进程中抓取启动命令行参数',
    autoHideTaskbar: '自动隐藏任务栏',
    autoHideTaskbarDesc: '控制期间将自动挂起 Windows 任务栏显示状态，以实现全屏沉浸。控制结束时将自动恢复原始状态。',
    displaySync: '同步显示器设置',
    displaySyncDesc: '根据窗口方向自动旋转物理显示器并调整分辨率。',
    visualOverlay: '垫衬背景',
    visualOverlayDesc: '在目标进程窗口层级之下，注入一层静态色彩或纹理背景，用于垫衬非全屏比例下的显示空隙。',
    enableOverlay: '启用叠加层',
    bgMode: '背景模式',
    solidColor: '纯色',
    bgImage: '背景图片',
    pickPreset: '选择预设或自定义',
    renderSource: '渲染源',
    import: '导入',
    clear: '清除',
    activeOverride: '监听保持中',
    targetProcess: '目标进程',
    sysLog: '系统日志',
    initialize: '启动',
    stop: '停止',
    exitTip: {
      title: '退出快捷键',
      message: '控制已激活。请按 F12 停止并退出，或按 F9 启动监听。',
      dontShowAgain: '下次不再提示',
      gotIt: '知道了'
    },
    uac: {
      title: '权限被拒绝',
      message: '系统需要管理员权限 (UAC) 才能向目标进程注入钩子。请以管理员身份重新启动应用程序。',
      button: '以管理员身份重启',
      secondaryButton: '以普通权限继续'
    },
    menu: {
      theme: '主题',
      dark: '暗黑模式',
      light: '亮色模式',
      language: '语言',
      about: '关于',
      registerAssociation: '注册关联',
      cleanAssociation: '清理关联',
      aboutTitle: '系统信息',
      aboutText: '沉浸式窗口控制 v1.0\n硬核窗口管理 UI 原型。\n专为精准显示覆盖而开发。',
      cancel: '取消',
      close: '关闭'
    },
    share: '创建快捷方式',
    registerModal: {
      title: '注册关联',
      message: '这将创建桌面/开始菜单快捷方式，用于从第三方程序启动。是否需要自定义 .ico 图标？\n选择"是"进入图标选择页面，选择"否"将直接使用程序默认图标静默创建。',
      yes: '是',
      no: '否（默认）'
    },
    iconRegistration: {
      title: '图标注册',
      dragHere: '拖拽 .ico 文件到此处',
      clickToSelect: '或点击浏览',
    },
    urlCreation: {
      title: 'URL 快捷方式创建',
      urlNameLabel: '快捷方式文件名',
      locationLabel: '创建位置',
      startMenu: '开始菜单',
      desktop: '桌面',
      urlNameHint: '自定义快捷方式文件名，使用不同的文件名可以避免图标缓存刷新不及时，让修改的图标立即生效。',
      create: '创建快捷方式',
    },
    logs: {
      initialized: '> 系统控制已初始化。',
      awaiting: '> 等待配置数据...',
      injecting: (name) => `> 正在注入钩子到 [${name}]...`,
      active: '> 控制已激活。',
      terminated: '> 控制已终止。',
      standby: '> 系统返回待机状态。'
    }
  }
}

export const i18n = reactive({
  lang: 'en',
  t: {}
})

export function setLanguage(lang) {
  i18n.lang = lang
  i18n.t = translations[lang]
}

// Auto detect language
const navLang = navigator.language.toLowerCase()
if (navLang.startsWith('zh')) {
  setLanguage('zh')
} else {
  setLanguage('en')
}
