<script setup>
import { computed } from 'vue'
import { i18n } from '../i18n'
import { bridge } from '../services/bridge'

const props = defineProps({
  processName: String,
  ratioW: Number,
  ratioH: Number,
  autoHideTaskbar: Boolean,
  enableOverlay: Boolean,
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
  'update:ratioW', 
  'update:ratioH', 
  'update:autoHideTaskbar', 
  'update:enableOverlay', 
  'update:bgMode', 
  'update:selectedColor',
  'update:bgImage'
])

const isPreset1 = computed(() => props.ratioW === 9 && props.ratioH === 16)
const isPreset2 = computed(() => props.ratioW === 3 && props.ratioH === 4)

const hasRatioError = computed(() => props.propertyErrors.PortraitAspectRatio?.length > 0)

const onRatioChange = (w, h) => {
    emit('update:ratioW', parseInt(w) || 0);
    emit('update:ratioH', parseInt(h) || 0);
    bridge.SetPortraitAspectRatio(`${w}/${h}`);
}

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

const onColorChange = (hex) => {
    emit('update:selectedColor', hex);
    const fullHex = "#FF" + hex.substring(1).toUpperCase();
    bridge.SetBackgroundColor(fullHex);
}

const selectImage = () => {
  bridge.SelectImage();
}

const clearImage = () => {
  bridge.ClearImage();
  emit('update:bgImage', '');
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
      >
      <span class="status-icon" :style="{ opacity: processName ? 1 : 0, transform: processName ? 'scale(1)' : 'scale(0.5)' }">✓</span>
    </div>
    <p class="section-desc" style="margin-top: 8px;">{{ i18n.t.processNameDesc }}</p>

    <div class="thick-divider"></div>

    <div class="section-title">{{ i18n.t.windowMatrix }}</div>
    <p class="section-desc">{{ i18n.t.windowMatrixDesc }}</p>
    <label class="input-label">{{ i18n.t.aspectRatio }}</label>
    <div class="ratio-grid">
      <input 
        type="number" 
        :value="ratioW" 
        @input="e => onRatioChange(e.target.value, ratioH)" 
        placeholder="W"
        :class="{ error: hasRatioError }"
      >
      <span>:</span>
      <input 
        type="number" 
        :value="ratioH" 
        @input="e => onRatioChange(ratioW, e.target.value)" 
        placeholder="H"
        :class="{ error: hasRatioError }"
      >
      
      <div class="vertical-divider"></div>
      
      <button :class="['btn-preset', { active: isPreset1 }]" @click="onRatioChange(9, 16)">9:16</button>
      <button :class="['btn-preset', { active: isPreset2 }]" @click="onRatioChange(3, 4)">3:4</button>
    </div>
    <p v-if="hasRatioError" class="error-text">{{ propertyErrors.PortraitAspectRatio[0] }}</p>
    
    <div class="row-setting">
      <span>{{ i18n.t.autoHideTaskbar }}</span>
      <label class="switch-label">
        <input type="checkbox" :checked="autoHideTaskbar" @change="onAutoHideChange">
        <span class="slider"></span>
      </label>
    </div>
    <p class="section-desc" style="margin-top: 8px; margin-bottom: 0;">{{ i18n.t.autoHideTaskbarDesc }}</p>

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
              v-for="color in ['#000000', '#1c1c1f', '#ff8c00', '#0077b6', '#00ff41']" 
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

.input-label { display: block; font-size: 12px; color: var(--text-muted); margin-bottom: 8px; font-weight: bold; }
.input-wrapper { position: relative; display: flex; align-items: center; }

input[type="text"], input[type="number"] {
    width: 100%; background: var(--input-bg); border: 2px solid var(--input-stroke);
    color: var(--text-main); padding: 12px 14px; border-radius: var(--shape-radius);
    font-size: 14px; font-weight: 600; outline: none; transition: all 0.2s;
}
.input-wrapper input[type="text"] { padding-right: 42px; }
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
    position: absolute; right: 14px;
    color: var(--primary-color); font-weight: 900; font-size: 16px;
    transition: opacity 0.3s, transform 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
    pointer-events: none;
}

input[type="number"]::-webkit-inner-spin-button,
input[type="number"]::-webkit-outer-spin-button {
    -webkit-appearance: none; margin: 0;
}
input[type="number"] { -moz-appearance: textfield; }

.ratio-grid { display: flex; align-items: center; gap: 8px; margin-bottom: 12px; }
.ratio-grid input { text-align: center; padding: 10px; }
.ratio-grid span { font-weight: bold; color: var(--text-muted); padding: 0 2px; }

.vertical-divider {
    width: 2px; height: 22px; background: var(--input-stroke);
    margin: 0 4px; border-radius: 1px;
}

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

.mode-tabs { display: flex; gap: 12px; margin-bottom: 20px; }
.tab-label { flex: 1; cursor: pointer; position: relative; }
.tab-label input { display: none; }
.tab-bg {
    text-align: center; padding: 12px; background: var(--input-bg); border: 2px solid var(--input-stroke); 
    border-radius: var(--shape-radius); font-size: 13px; font-weight: bold; color: var(--text-muted); transition: 0.2s;
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
