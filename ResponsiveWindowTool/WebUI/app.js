const bridge = window.chrome.webview.hostObjects.bridge;

// State
let currentLang = 'EN';
let isDark = false;

// Localization Strings
const i18n = {
    EN: {
        title: "Game Tool",
        uac: "Administrator rights required.",
        restart: "Restart as Admin",
        targetCard: "Target Application",
        labelProcess: "Process Name",
        layoutCard: "Window Layout",
        labelRatio: "Portrait Aspect Ratio",
        checkTaskbar: "Auto-hide Taskbar",
        visualsCard: "Visual Configuration",
        checkOverlay: "Background Overlay",
        labelColor: "Solid Color",
        labelImage: "Background Image",
        btnSelect: "Select",
        btnClear: "Clear",
        btnStart: "START MONITORING",
        activeStatus: "ACTIVE MONITORING",
        infoProcess: "PROCESS",
        infoRatio: "RATIO",
        logTitle: "Activity Logs",
        solidColor: "Using solid color",
        noImage: "No Image"
    },
    ZH: {
        title: "游戏窗口工具",
        uac: "需要管理员权限以继续。",
        restart: "以管理员重启",
        targetCard: "目标应用程序",
        labelProcess: "进程名称",
        layoutCard: "窗口布局",
        labelRatio: "纵向宽高比",
        checkTaskbar: "自动隐藏任务栏",
        visualsCard: "视觉配置",
        checkOverlay: "启用背景遮罩",
        labelColor: "纯色背景",
        labelImage: "图片背景",
        btnSelect: "选择图片",
        btnClear: "清除",
        btnStart: "开始监控",
        activeStatus: "正在监控中",
        infoProcess: "目标进程",
        infoRatio: "窗口比例",
        logTitle: "运行日志",
        solidColor: "正在使用纯色背景",
        noImage: "无预览"
    }
};

// Elements
const setupView = document.getElementById('setup-view');
const runningView = document.getElementById('running-view');
const floatingAction = document.getElementById('floating-action');
const processInput = document.getElementById('process-name');
const ratioW = document.getElementById('ratio-w');
const ratioH = document.getElementById('ratio-h');
const autoHideCheck = document.getElementById('auto-hide-taskbar');
const overlayCheck = document.getElementById('enable-overlay');
const imageNameText = document.getElementById('image-name');
const statusDot = document.getElementById('status-dot');
const logsContent = document.getElementById('logs-content');
const uacBanner = document.getElementById('uac-banner');
const colorInput = document.getElementById('color-input');
const imagePreview = document.getElementById('image-preview');

// Initialize
async function init() {
    processInput.value = await bridge.TargetProcessName;
    
    // Parse ratio string "W/H" into two inputs
    const ratioStr = await bridge.PortraitAspectRatio;
    if (ratioStr && ratioStr.includes('/')) {
        const parts = ratioStr.split('/');
        ratioW.value = parts[0];
        ratioH.value = parts[1];
    } else {
        ratioW.value = 9;
        ratioH.value = 16;
    }

    autoHideCheck.checked = await bridge.EnableTaskbarAutoHide;
    overlayCheck.checked = await bridge.EnableBackgroundOverlay;
    
    // Fix color picking
    const bgColor = await bridge.BackgroundColor;
    if (bgColor && bgColor.startsWith('#')) {
        // C# hex is #AARRGGBB, input type color needs #RRGGBB
        colorInput.value = '#' + bgColor.substring(bgColor.length - 6);
    }
    
    updateUacBanner(await bridge.IsAdmin);
    updateStatus(await bridge.IsRunning);
    refreshLogs();
    
    const initialImg = await bridge.CurrentImageFileName;
    updateImagePreview(initialImg);

    // Listeners for split ratio
    ratioW.addEventListener('change', updateRatioBridge);
    ratioH.addEventListener('change', updateRatioBridge);
    
    autoHideCheck.addEventListener('change', () => bridge.SetEnableTaskbarAutoHide(autoHideCheck.checked));
    overlayCheck.addEventListener('change', () => bridge.SetEnableBackgroundOverlay(overlayCheck.checked));

    applyLanguage();
}

function updateRatioBridge() {
    const val = `${ratioW.value}/${ratioH.value}`;
    bridge.SetPortraitAspectRatio(val);
}

function setSplitRatio(w, h) {
    ratioW.value = w;
    ratioH.value = h;
    updateRatioBridge();
}

function setColor(hex) {
    // hex is #FFRRGGBB
    colorInput.value = '#' + hex.substring(3);
    bridge.SetBackgroundColor(hex);
}

function onColorPickerChange(hex) {
    // hex is #RRGGBB -> convert to #FFRRGGBB
    const fullHex = "#FF" + hex.substring(1).toUpperCase();
    bridge.SetBackgroundColor(fullHex);
}

async function updateImagePreview(fileName) {
    if (fileName) {
        imageNameText.innerText = fileName;
        imageNameText.dataset.custom = "true";
        // Fetch Base64 data from C# bridge
        const base64 = await bridge.GetImageBase64(fileName);
        if (base64) {
            imagePreview.style.backgroundImage = `url('${base64}')`;
            imagePreview.innerText = "";
            imagePreview.style.backgroundSize = "cover";
        } else {
            imagePreview.style.backgroundImage = "none";
            imagePreview.innerText = "Error";
        }
    } else {
        imageNameText.innerText = i18n[currentLang].solidColor;
        imageNameText.dataset.custom = "";
        imagePreview.style.backgroundImage = "none";
        imagePreview.innerText = i18n[currentLang].noImage;
    }
}

// UI Helpers
function toggleTheme() {
    isDark = !isDark;
    document.body.className = isDark ? 'dark' : 'light';
    document.getElementById('btn-theme').innerText = isDark ? '☀' : '🌓';
}

function toggleLanguage() {
    currentLang = currentLang === 'EN' ? 'ZH' : 'EN';
    document.getElementById('btn-lang').innerText = currentLang;
    applyLanguage();
}

function applyLanguage() {
    const t = i18n[currentLang];
    document.getElementById('txt-app-title').innerText = t.title;
    document.getElementById('txt-uac-msg').innerText = t.uac;
    document.getElementById('btn-restart-uac').innerText = t.restart;
    document.getElementById('txt-card-target').innerText = t.targetCard;
    document.getElementById('txt-label-process').innerText = t.labelProcess;
    document.getElementById('txt-card-layout').innerText = t.layoutCard;
    document.getElementById('txt-label-ratio').innerText = t.labelRatio;
    document.getElementById('txt-check-taskbar').innerText = t.checkTaskbar;
    document.getElementById('txt-card-visuals').innerText = t.visualsCard;
    document.getElementById('txt-check-overlay').innerText = t.checkOverlay;
    document.getElementById('txt-label-color').innerText = t.labelColor;
    document.getElementById('txt-label-image').innerText = t.labelImage;
    document.getElementById('btn-select').innerText = t.btnSelect;
    document.getElementById('btn-clear').innerText = t.btnClear;
    document.getElementById('btn-start').innerText = t.btnStart;
    document.getElementById('txt-active-status').innerText = t.activeStatus;
    document.getElementById('txt-info-process').innerText = t.infoProcess;
    document.getElementById('txt-info-ratio').innerText = t.infoRatio;
    document.getElementById('txt-log-title').innerText = t.logTitle;
    
    if (!imageNameText.dataset.custom) {
        imageNameText.innerText = t.solidColor;
    }
    if (!imagePreview.style.backgroundImage || imagePreview.style.backgroundImage === "none") {
        imagePreview.innerText = t.noImage;
    }
}

function toggleCheckbox(id) {
    const el = document.getElementById(id);
    el.click();
}

function startMonitoring() {
    if (!processInput.value) return;
    bridge.StartMonitoring(processInput.value);
}

function updateStatus(isRunning) {
    if (isRunning) {
        setupView.classList.add('hidden');
        floatingAction.classList.add('hidden');
        runningView.classList.remove('hidden');
        statusDot.className = "status-dot running";
        
        document.getElementById('info-process-val').innerText = processInput.value;
        document.getElementById('info-ratio-val').innerText = `${ratioW.value}:${ratioH.value}`;
        document.getElementById('info-details-val').innerText = `${autoHideCheck.checked ? 'AutoHide ON' : 'AutoHide OFF'} | ${overlayCheck.checked ? 'Overlay ON' : 'Overlay OFF'}`;
    } else {
        runningView.classList.add('hidden');
        setupView.classList.remove('hidden');
        floatingAction.classList.remove('hidden');
        statusDot.className = "status-dot stopped";
    }
}

function updateUacBanner(isAdmin) {
    if (isAdmin) uacBanner.classList.add('hidden');
    else uacBanner.classList.remove('hidden');
}

async function refreshLogs() {
    const logs = await bridge.GetLogs();
    renderLogs(logs);
}

function renderLogs(logs) {
    logsContent.innerHTML = '';
    logs.forEach(log => {
        const div = document.createElement('div');
        div.className = 'log-entry';
        div.innerText = log;
        logsContent.appendChild(div);
    });
    // Auto scroll to latest
    logsContent.scrollTop = logsContent.scrollHeight;
}

window.onStateChanged = function(stateJson) {
    const state = JSON.parse(stateJson);
    if (state.IsRunning !== undefined) updateStatus(state.IsRunning);
    if (state.Logs !== undefined) renderLogs(state.Logs);
    if (state.CurrentImageFileName !== undefined) {
        updateImagePreview(state.CurrentImageFileName);
    }
};

init();