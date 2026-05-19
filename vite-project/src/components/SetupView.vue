<script setup>
import { computed, ref, watch, onMounted } from 'vue'
import { i18n } from '../i18n'
import { bridge } from '../services/bridge'

const props = defineProps({
  processName: String,
  autoHideTaskbar: Boolean,
  enableDisplaySync: Boolean,
  enableOverlay: Boolean,
  associatedLaunchPath: String,
  launchOnAppStartup: Boolean,
  launchOnTaskStart: Boolean,
  autoStartFromThirdParty: Boolean,
  bgMode: String,
  selectedColor: String,
  bgImage: String,
  propertyErrors: {
    type: Object,
    default: () => ({})
  }
})

const emit = defineEmits([
  'update:processName', 
  'update:autoHideTaskbar', 
  'update:enableDisplaySync',
  'update:enableOverlay', 
  'update:associatedLaunchPath',
  'update:launchOnAppStartup',
  'update:launchOnTaskStart',
  'update:autoStartFromThirdParty',
  'update:bgMode', 
  'update:selectedColor',
  'update:bgImage'
])

const isProcessFound = ref(null);
let checkTimeout = null;

const hoveredLaunchTab = ref(null); // The tab currently being hovered

const checkProcess = async (name) => {
  if (!name) {
    isProcessFound.value = null;
    return;
  }
  isProcessFound.value = await bridge.CheckProcessExists(name);
};

watch(() => props.processName, (newVal) => {
  clearTimeout(checkTimeout);
  checkTimeout = setTimeout(() => {
    checkProcess(newVal);
  }, 500);
});

onMounted(() => {
  if (props.processName) {
    checkProcess(props.processName);
  }
});

const onAutoHideChange = (e) => {
    const val = e.target.checked;
    emit('update:autoHideTaskbar', val);
    bridge.SetEnableTaskbarAutoHide(val);
}

const onOverlayChange = (e) => {
    const val = e.target.checked;
    emit('update:enableOverlay', val);
    bridge.SetEnableBackgroundOverlay(val);
}

const onDisplaySyncChange = (e) => {
    const val = e.target.checked;
    emit('update:enableDisplaySync', val);
    bridge.SetEnableDisplaySync(val);
}

const onLaunchOnAppStartupChange = (e) => {
    const val = e.target.checked;
    emit('update:launchOnAppStartup', val);
    bridge.SetLaunchOnAppStartup(val);
}

const onLaunchOnTaskStartChange = (e) => {
    const val = e.target.checked;
    emit('update:launchOnTaskStart', val);
    bridge.SetLaunchOnTaskStart(val);
}

const onAutoStartFromThirdPartyChange = async () => {
    if (!props.autoStartFromThirdParty) {
        const isRegistered = await bridge.IsProtocolRegistered;
        if (isRegistered) {
            bridge.SetAutoStartFromThirdParty(true);
            emit('update:autoStartFromThirdParty', true);
        } else {
            // We emit an event to App.vue to show the modal
            emit('showProtocolModal');
        }
    } else {
        bridge.SetAutoStartFromThirdParty(false);
        emit('update:autoStartFromThirdParty', false);
    }
}

const onColorChange = (hex) => {
    emit('update:selectedColor', hex);
    const fullHex = "#FF" + hex.substring(1).toUpperCase();
    bridge.SetBackgroundColor(fullHex);
}

const selectImage = () => {
  bridge.SelectImage();
}

const selectAssociatedProgram = () => {
  bridge.SelectAssociatedProgram();
}

const clearImage = () => {
  bridge.ClearImage();
  emit('update:bgImage', '');
}

const detectCommandLine = async () => {
  if (!props.processName) return;
  const commandLine = await bridge.GetProcessCommandLine(props.processName);
  if (commandLine) {
    emit('update:associatedLaunchPath', commandLine);
    bridge.SetAssociatedLaunchPath(commandLine);
  }
}
</script>

<template>
  <div id="setup-view">
    <div class="section-title">{{ i18n.t.targetCore }}</div>
    <label class="input-label">{{ i18n.t.processNameLabel }}</label>
    <div class="input-wrapper">
      <input 
        type="text" 
        :value="processName" 
        @input="e => emit('update:processName', e.target.value)" 
        placeholder="TargetApp.exe"
        :class="{ 'error': processName && isProcessFound === false, 'success': processName && isProcessFound === true }"
        style="padding-right: 40px;"
      >
      <span class="status-icon" :style="{ 
          opacity: 1, 
          transform: 'scale(1)',
          color: !processName ? 'var(--input-stroke)' : (isProcessFound === true ? 'var(--success-color, #00ff41)' : (isProcessFound === false ? 'var(--danger-color)' : 'var(--input-stroke)')),
          right: '14px'
        }">✓</span>
    </div>
    <div v-if="processName && isProcessFound === false" class="error-text" style="display: flex; align-items: center; gap: 4px; cursor: pointer; margin-bottom: 20px;" @click="checkProcess(processName)">
      {{ i18n.t.processNotFound }}
      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" style="margin-bottom: -1px;">
        <path d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0 3.181 3.183a8.25 8.25 0 0 0 13.803-3.7M4.031 9.865a8.25 8.25 0 0 1 13.803-3.7l3.181 3.182m0-4.991v4.99" />
      </svg>
    </div>
    <p v-else class="section-desc" style="margin-top: 8px;">{{ i18n.t.processNameDesc }}</p>

    <div class="row-setting">
      <span>{{ i18n.t.autoHideTaskbar }}</span>
      <label class="switch-label">
        <input type="checkbox" :checked="autoHideTaskbar" @change="onAutoHideChange">
        <span class="slider"></span>
      </label>
    </div>
    <p class="section-desc" style="margin-top: 8px; margin-bottom: 0;">{{ i18n.t.autoHideTaskbarDesc }}</p>

    <div class="row-setting">
      <span>{{ i18n.t.displaySync }}</span>
      <label class="switch-label">
        <input type="checkbox" :checked="enableDisplaySync" @change="onDisplaySyncChange">
        <span class="slider"></span>
      </label>
    </div>
    <p class="section-desc" style="margin-top: 8px; margin-bottom: 0;">{{ i18n.t.displaySyncDesc }}</p>

    <div class="thick-divider"></div>

    <div class="section-title">{{ i18n.t.associatedLaunch }}</div>
    <label class="input-label">{{ i18n.t.launchPath }}</label>
    <div class="input-wrapper" style="margin-bottom: 12px;">
      <input 
        type="text" 
        :value="associatedLaunchPath" 
        @input="e => emit('update:associatedLaunchPath', e.target.value)"
        @change="e => bridge.SetAssociatedLaunchPath(e.target.value)"
        placeholder="steam://... or C:\Path\To\Game.exe"
        class="path-input"
        style="padding-right: 105px;"
      >
      <div class="input-actions">
        <button class="action-btn detect-btn" @click="detectCommandLine" :title="i18n.t.detectCommandLine">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          </svg>
        </button>
        <button class="action-btn browse-btn" @click="selectAssociatedProgram" :title="i18n.t.browse">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path>
          </svg>
        </button>
      </div>
    </div>
    <p class="section-desc" style="margin-bottom: 20px;margin-top: 8px;">{{ i18n.t.associatedLaunchDesc }}</p>

    <label class="input-label">{{ i18n.t.launchTiming }}</label>
    <!-- Multi-Select Tab Grid -->
    <div class="launch-tabs-grid">
      <div class="tab-item" 
             :class="{ 'checked': autoStartFromThirdParty }" 
             @mouseenter="hoveredLaunchTab = 'invoked'"
             @mouseleave="hoveredLaunchTab = null"
             @click="onAutoStartFromThirdPartyChange">
        <div class="checkbox-circle"></div>
        {{ i18n.t.tabInvoked }}
      </div>

      <div class="tab-item" 
             :class="{ 'checked': launchOnAppStartup }" 
             @mouseenter="hoveredLaunchTab = 'startup'"
             @mouseleave="hoveredLaunchTab = null"
             @click="emit('update:launchOnAppStartup', !launchOnAppStartup); bridge.SetLaunchOnAppStartup(!launchOnAppStartup)">
        <div class="checkbox-circle"></div>
        {{ i18n.t.tabStartup }}
      </div>

      <div class="tab-item" 
             :class="{ 'checked': launchOnTaskStart }" 
             @mouseenter="hoveredLaunchTab = 'task'"
             @mouseleave="hoveredLaunchTab = null"
             @click="emit('update:launchOnTaskStart', !launchOnTaskStart); bridge.SetLaunchOnTaskStart(!launchOnTaskStart)">
        <div class="checkbox-circle"></div>
        {{ i18n.t.tabTask }}
      </div>
    </div>

    <!-- Description Area directly below the tabs -->
    <div style="margin-top: 12px; display: flex; align-items: center;">
      <p v-if="hoveredLaunchTab === 'invoked'" class="section-desc" style="margin: 0;">
        {{ i18n.t.autoStartFromThirdPartyDesc }}
      </p>
      <p v-else-if="hoveredLaunchTab === 'startup'" class="section-desc" style="margin: 0;">
        {{ i18n.t.launchOnAppStartupDesc }}
      </p>
      <p v-else-if="hoveredLaunchTab === 'task'" class="section-desc" style="margin: 0;">
        {{ i18n.t.launchOnTaskStartDesc }}
      </p>
      <p v-else class="section-desc" style="margin: 0;">
        {{ i18n.t.associatedLaunchDefaultDesc }}
      </p>
    </div>

    <div class="thick-divider"></div>

    <div class="section-title">{{ i18n.t.visualOverlay }}</div>
    <p class="section-desc">{{ i18n.t.visualOverlayDesc }}</p>
    
    <div class="row-setting" style="margin-bottom: 16px;">
      <span>{{ i18n.t.enableOverlay }}</span>
      <label class="switch-label">
        <input type="checkbox" :checked="enableOverlay" @change="onOverlayChange">
        <span class="slider"></span>
      </label>
    </div>
    
    <div v-show="enableOverlay" id="visual-area">
      <div class="mode-tabs">
        <label class="tab-label">
          <input type="radio" :checked="bgMode === 'color'" @change="emit('update:bgMode', 'color')">
          <div class="tab-bg">{{ i18n.t.solidColor }}</div>
        </label>
        <label class="tab-label">
          <input type="radio" :checked="bgMode === 'image'" @change="emit('update:bgMode', 'image')">
          <div class="tab-bg">{{ i18n.t.bgImage }}</div>
        </label>
      </div>

      <div v-if="bgMode === 'color'" id="panel-color">
        <label class="input-label">{{ i18n.t.pickPreset }}</label>
        <div class="color-row">
          <div class="color-presets">
            <div 
              v-for="color in ['#000000', '#808080', '#ff8c00', '#0077b6', '#00ff41']" 
              :key="color"
              class="color-dot" 
              :style="{ backgroundColor: color }"
              @click="onColorChange(color)"
            ></div>
          </div>
          <input type="color" :value="selectedColor" @input="e => onColorChange(e.target.value)" id="color-input">
        </div>
      </div>

      <div v-else id="panel-image">
        <label class="input-label">{{ i18n.t.renderSource }}</label>
        <div class="image-preview-large">
          <img v-if="bgImage" :src="bgImage" class="img-preview" alt="Preview">
          <div v-else class="preview-placeholder">NO IMAGE DATA</div>
          <div class="preview-actions">
            <button class="btn-preset primary" @click="selectImage">{{ i18n.t.import }}</button>
            <button v-if="bgImage" class="btn-preset" @click="clearImage">{{ i18n.t.clear }}</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.section-title {
    font-size: 16px; font-weight: 900; color: var(--text-main);
    display: flex; align-items: center; gap: 10px; margin-bottom: 18px; text-transform: uppercase; letter-spacing: 1px;
}
.section-title::before {
    content: ''; display: block; width: 6px; height: 16px; background: var(--primary-color); border-radius: 0 4px 0 4px;
}

.section-desc {
    font-size: 11px;
    color: var(--text-muted);
    line-height: 1.6;
    margin-top: -12px;
    margin-bottom: 20px;
    font-weight: 600;
}

.thick-divider { height: 4px; background: var(--divider-color); margin: 24px 0; border-radius: 2px; position: relative; overflow: hidden; }
.thick-divider::after {
    content: ''; position: absolute; left: -10px; top: 0; width: 60px; height: 100%;
    background: var(--primary-color); transform: skewX(-30deg);
}

.input-label { display: block; font-size: 12px; color: var(--text-muted); margin-bottom: 8px; font-weight: bold; text-transform: uppercase; }
.input-wrapper { position: relative; display: flex; align-items: center; }

input[type="text"], input[type="number"] {
    width: 100%; background: var(--input-bg); border: 2px solid var(--input-stroke);
    color: var(--text-main); padding: 12px 14px; border-radius: var(--shape-radius);
    font-size: 14px; font-weight: 600; outline: none; transition: all 0.2s;
}
.input-actions {
    position: absolute; right: 6px; top: 6px; bottom: 6px;
    display: flex; gap: 2px;
    background: rgba(128, 128, 128, 0.15);
    padding: 2px;
    border-radius: 999px; /* Capsule shape */
}

.action-btn {
    background: transparent; color: var(--text-main);
    border: none; border-radius: 999px;
    width: 30px; height: 100%;
    display: flex; justify-content: center; align-items: center; cursor: pointer;
    transition: all 0.2s;
}
.action-btn:hover { background: var(--primary-color); color: white; }

.detect-btn {
    /* No additional background, transparent inherits from .action-btn */
}
.detect-btn svg { color: var(--text-main); }
.detect-btn:hover svg { color: white; }

.input-wrapper .browse-btn {
    position: static;
    width: 30px;
}

input[type="text"]:focus, input[type="number"]:focus {
    border-color: var(--primary-color); box-shadow: 0 0 12px rgba(255, 140, 0, 0.15);
}

input.error {
    border-color: var(--danger-color) !important;
}
.error-text {
    color: var(--danger-color);
    font-size: 11px;
    margin-top: 4px;
    font-weight: bold;
    text-transform: uppercase;
}

.status-icon {
    position: absolute; right: 80px;
    color: var(--primary-color); font-weight: 900; font-size: 16px;
    transition: opacity 0.3s, transform 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
    pointer-events: none;
}

.path-input {
    direction: rtl;
    text-align: left;
    text-overflow: ellipsis;
}
.path-input:focus {
    direction: ltr;
}

input[type="number"]::-webkit-inner-spin-button,
input[type="number"]::-webkit-outer-spin-button {
    -webkit-appearance: none; margin: 0;
}
input[type="number"] { -moz-appearance: textfield; }

.btn-preset {
    border: 2px solid transparent; background: var(--btn-bg); color: var(--text-main);
    padding: 10px 14px; border-radius: var(--shape-radius-sm);
    font-size: 13px; font-weight: bold; cursor: pointer; transition: 0.2s;
}
.btn-preset:hover { background: var(--input-stroke); }
.btn-preset.active { background: var(--primary-color); color: white; }
.btn-preset.primary { background: var(--primary-color); color: white; }

.row-setting { display: flex; justify-content: space-between; align-items: center; margin-top: 16px; }
.row-setting span { font-size: 14px; font-weight: bold; }

.switch-label { position: relative; display: inline-block; width: 52px; height: 28px; }
.switch-label input { opacity: 0; width: 0; height: 0; }
.slider { position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background-color: var(--input-stroke); transition: .3s; border-radius: 999px; }
.slider:before { position: absolute; content: ""; height: 22px; width: 22px; left: 3px; bottom: 3px; background-color: white; transition: .3s; border-radius: 50%; }
.switch-label input:checked + .slider { background-color: var(--primary-color); }
.switch-label input:checked + .slider:before { transform: translateX(24px); }
.slider::after { content: 'OFF'; position: absolute; right: 6px; top: 8px; color: #fff; font-size: 10px; font-weight: bold; }
.switch-label input:checked + .slider::after { content: 'ON'; left: 8px; right: auto; }

.launch-tabs-grid {
    display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px;
}
.tab-item {
    display: flex; align-items: center; justify-content: center;
    background: var(--input-bg); padding: 14px 10px;
    border: none;
    border-radius: 2px 12px 2px 12px;
    cursor: pointer; transition: all 0.2s;
    font-size: 12px; font-weight: 900; color: var(--text-muted);
    position: relative;
    user-select: none;
}
.tab-item input { display: none; }
.checkbox-circle {
    width: 14px; height: 14px; border-radius: 50%; border: 2px solid var(--input-stroke);
    margin-right: 8px; background: var(--modal-bg);
    display: flex; align-items: center; justify-content: center; transition: all 0.2s;
}
.tab-item.checked {
    background: rgba(255, 140, 0, 0.15);
    color: var(--primary-color);
}
.tab-item.checked .checkbox-circle::after {
    content: ''; width: 6px; height: 6px; background: var(--primary-color); border-radius: 50%;
}
.tab-item.checked .checkbox-circle {
    border-color: var(--input-stroke); /* Keep outer ring gray even when checked */
}

/* Hover States */
.tab-item:hover {
    background: rgba(255, 140, 0, 0.1); /* Light orange hover */
}
.tab-item.checked:hover {
    background: rgba(255, 140, 0, 0.25);
}

.mode-tabs { display: flex; gap: 10px; margin-bottom: 20px; }
.tab-label { flex: 1; cursor: pointer; position: relative; }
.tab-label input { display: none; }
.tab-bg {
    text-align: center; padding: 8px 10px; background: var(--input-bg); border: 2px solid var(--input-stroke); 
    border-radius: var(--shape-radius); font-size: 12px; font-weight: bold; color: var(--text-muted); transition: 0.2s;
}
.tab-label input:checked + .tab-bg { background: var(--primary-color); color: white; border-color: var(--primary-color); box-shadow: 0 4px 12px rgba(255, 140, 0, 0.2); }

.color-row { display: flex; align-items: center; gap: 12px; }
.color-presets { display: flex; gap: 10px; flex: 1; }
.color-dot { width: 32px; height: 32px; cursor: pointer; border: 2px solid var(--input-stroke); border-radius: var(--shape-radius-sm); transition: transform 0.2s, border-color 0.2s; }
.color-dot:hover { transform: scale(1.1); border-color: white; }
#color-input { width: 40px; height: 40px; border: none; border-radius: 50%; background: none; cursor: pointer; padding: 0; }
#color-input::-webkit-color-swatch { border: 2px solid var(--input-stroke); border-radius: 50%; }

.image-preview-large {
    width: 100%; height: 160px; border: 2px dashed var(--input-stroke); border-radius: var(--shape-radius); background: var(--input-bg);
    display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; position: relative; overflow: hidden;
}
.img-preview { width: 100%; height: 100%; object-fit: cover; position: absolute; top: 0; left: 0; opacity: 0.6; }
.preview-placeholder { font-size: 13px; color: var(--text-muted); font-weight: bold; z-index: 2; }
.preview-actions { display: flex; gap: 10px; z-index: 2; }
</style>
