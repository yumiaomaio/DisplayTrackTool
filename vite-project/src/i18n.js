import { reactive } from 'vue'

export const translations = {
  en: {
    appTitle: 'IMMERSIVE CONTROL',
    processNameLabel: 'PROCESS NAME',
    processNameDesc: 'Right-click the process in Task Manager and check properties to find the exact executable name.',
    targetCore: 'Target Core',
    associatedLaunch: 'Associated Launch',
    associatedLaunchDesc: 'Automatically execute a URL scheme or program (with arguments) on startup or task start.',
    launchPath: 'URL / PROGRAM PATH',
    launchOnAppStartup: 'Launch on App Startup',
    launchOnTaskStart: 'Launch on Task Start',
    browse: 'BROWSE...',
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
      message: 'Monitoring active. Press F12 to stop and exit immersive mode.',
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
      aboutTitle: 'SYSTEM INFORMATION',
      aboutText: 'Immersive Window Control v1.0\nA hardcore UI prototype for window management.\nDeveloped for precision display override.',
      close: 'CLOSE'
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
    appTitle: '沉浸式窗口控制',
    processNameLabel: '进程名称',
    processNameDesc: '请在任务管理器中右键目标进程，查看“属性”以获取准确的可执行文件名（如：Target.exe）。',
    processNotFound: '未检测到目标进程，请确认该程序已运行。',
    targetCore: '核心目标',
    associatedLaunch: '关联启动',
    associatedLaunchDesc: '在应用启动或任务开启时，自动执行指定的 URL Scheme 或程序路径（支持启动参数）。',
    launchPath: 'URL / 程序路径',
    launchOnAppStartup: '应用启动时执行',
    launchOnTaskStart: '任务开启时执行',
    browse: '浏览...',
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
      message: '控制已激活。请按 F12 停止并退出沉浸模式。',
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
      aboutTitle: '系统信息',
      aboutText: '沉浸式窗口控制 v1.0\n硬核窗口管理 UI 原型。\n专为精准显示覆盖而开发。',
      close: '关闭'
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
