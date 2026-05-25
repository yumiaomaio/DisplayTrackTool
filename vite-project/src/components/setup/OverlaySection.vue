<script setup>
import { i18n } from '../../i18n'
import { bridge } from '../../services/bridge'

defineProps({ enableOverlay: Boolean, bgMode: String, selectedColor: String, bgImage: String })
const emit = defineEmits(['update:enableOverlay', 'update:bgMode', 'update:selectedColor', 'update:bgImage'])

const onColorChange = (hex) => {
  emit('update:selectedColor', hex)
  bridge.SetBackgroundColor("#FF" + hex.substring(1).toUpperCase())
}

const setMode = (mode) => {
  emit('update:bgMode', mode)
  bridge.SetBackgroundMode(mode)
}
</script>

<template>
  <div>
    <div class="section-title">{{ i18n.t.visualOverlay }}</div>
    <p class="section-desc">{{ i18n.t.visualOverlayDesc }}</p>

    <div class="row-setting" style="margin-bottom: 16px;">
      <span>{{ i18n.t.enableOverlay }}</span>
      <label class="switch-label">
        <input type="checkbox" :checked="enableOverlay"
          @change="e => { emit('update:enableOverlay', e.target.checked); bridge.SetEnableBackgroundOverlay(e.target.checked) }">
        <span class="slider"></span>
      </label>
    </div>

    <div v-show="enableOverlay">
      <div class="mode-tabs">
        <label class="tab-label">
          <input type="radio" :checked="bgMode === 'color'" @change="setMode('color')">
          <div class="tab-bg">{{ i18n.t.solidColor }}</div>
        </label>
        <label class="tab-label">
          <input type="radio" :checked="bgMode === 'image'" @change="setMode('image')">
          <div class="tab-bg">{{ i18n.t.bgImage }}</div>
        </label>
      </div>

      <div v-if="bgMode === 'color'">
        <label class="input-label">{{ i18n.t.pickPreset }}</label>
        <div class="color-row">
          <div class="color-presets">
            <div v-for="c in ['#000000','#808080','#ff8c00','#0077b6','#00ff41']" :key="c"
              class="color-dot" :style="{ backgroundColor: c }" @click="onColorChange(c)"></div>
          </div>
          <input type="color" :value="selectedColor" @input="e => onColorChange(e.target.value)" id="color-input">
        </div>
      </div>

      <div v-else>
        <label class="input-label">{{ i18n.t.renderSource }}</label>
        <div class="image-preview-large">
          <img v-if="bgImage" :src="bgImage" class="img-preview" alt="Preview">
          <div v-else class="preview-placeholder">NO IMAGE DATA</div>
          <div class="preview-actions">
            <button class="btn-preset primary" @click="bridge.SelectImage()">{{ i18n.t.import }}</button>
            <button v-if="bgImage" class="btn-preset" @click="bridge.ClearImage(); emit('update:bgImage', '')">{{ i18n.t.clear }}</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.section-title { font-size: 16px; font-weight: 900; color: var(--text-main); display: flex; align-items: center; gap: 10px; margin-bottom: 18px; text-transform: uppercase; letter-spacing: 1px; }
.section-title::before { content: ''; display: block; width: 6px; height: 16px; background: var(--primary-color); border-radius: 0 4px 0 4px; }
.section-desc { font-size: 11px; color: var(--text-muted); line-height: 1.6; margin-top: -12px; margin-bottom: 20px; font-weight: 600; }
.input-label { display: block; font-size: 12px; color: var(--text-muted); margin-bottom: 8px; font-weight: bold; text-transform: uppercase; }
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
.mode-tabs { display: flex; gap: 10px; margin-bottom: 20px; }
.tab-label { flex: 1; cursor: pointer; position: relative; }
.tab-label input { display: none; }
.tab-bg { text-align: center; padding: 8px 10px; background: var(--input-bg); border: 2px solid var(--input-stroke); border-radius: var(--shape-radius); font-size: 12px; font-weight: bold; color: var(--text-muted); transition: 0.2s; }
.tab-label input:checked + .tab-bg { background: var(--primary-color); color: white; border-color: var(--primary-color); box-shadow: 0 4px 12px rgba(255, 140, 0, 0.2); }
.color-row { display: flex; align-items: center; gap: 12px; }
.color-presets { display: flex; gap: 10px; flex: 1; }
.color-dot { width: 32px; height: 32px; cursor: pointer; border: 2px solid var(--input-stroke); border-radius: var(--shape-radius-sm); transition: transform 0.2s, border-color 0.2s; }
.color-dot:hover { transform: scale(1.1); border-color: white; }
#color-input { width: 40px; height: 40px; border: none; border-radius: 50%; background: none; cursor: pointer; padding: 0; }
#color-input::-webkit-color-swatch { border: 2px solid var(--input-stroke); border-radius: 50%; }
.image-preview-large { width: 100%; height: 160px; border: 2px dashed var(--input-stroke); border-radius: var(--shape-radius); background: var(--input-bg); display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; position: relative; overflow: hidden; }
.img-preview { width: 100%; height: 100%; object-fit: cover; position: absolute; top: 0; left: 0; opacity: 0.6; }
.preview-placeholder { font-size: 13px; color: var(--text-muted); font-weight: bold; z-index: 2; }
.preview-actions { display: flex; gap: 10px; z-index: 2; }
.btn-preset { border: 2px solid transparent; background: var(--btn-bg); color: var(--text-main); padding: 10px 14px; border-radius: var(--shape-radius-sm); font-size: 13px; font-weight: bold; cursor: pointer; transition: 0.2s; }
.btn-preset:hover { background: var(--input-stroke); }
.btn-preset.primary { background: var(--primary-color); color: white; }
</style>
