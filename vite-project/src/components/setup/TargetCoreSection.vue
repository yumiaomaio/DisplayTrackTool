<script setup>
import { ref, watch, onMounted } from 'vue'
import { i18n } from '../../i18n'
import { bridge } from '../../services/bridge'

const props = defineProps({
  processName: String, autoHideTaskbar: Boolean, enableDisplaySync: Boolean
})
const emit = defineEmits([
  'update:processName', 'update:autoHideTaskbar', 'update:enableDisplaySync'
])

const isProcessFound = ref(null)
let checkTimeout = null

const checkProcess = async (name) => {
  if (!name) { isProcessFound.value = null; return }
  isProcessFound.value = await bridge.CheckProcessExists(name)
}

watch(() => props.processName, (v) => {
  clearTimeout(checkTimeout)
  checkTimeout = setTimeout(() => checkProcess(v), 500)
})

onMounted(() => { if (props.processName) checkProcess(props.processName) })

const toggleSwitch = (key, bridgeFn) => (e) => {
  const val = e.target.checked
  emit('update:' + key, val)
  bridgeFn(val)
}
</script>

<template>
  <div>
    <div class="section-title">{{ i18n.t.targetCore }}</div>
    <label class="input-label">{{ i18n.t.processNameLabel }}</label>
    <div class="input-wrapper">
      <input type="text"
        :value="processName"
        @input="e => emit('update:processName', e.target.value)"
        @change="e => bridge.SetTargetProcessName(e.target.value)"
        placeholder="TargetApp.exe"
        :class="{ error: processName && isProcessFound === false, success: processName && isProcessFound === true }"
        style="padding-right: 40px;">
      <span class="status-icon" :style="{
        color: !processName ? 'var(--input-stroke)' : (isProcessFound === true ? 'var(--success-color)' : (isProcessFound === false ? 'var(--danger-color)' : 'var(--input-stroke)'))
      }">&#10003;</span>
    </div>
    <div v-if="processName && isProcessFound === false" class="error-text" @click="checkProcess(processName)">
      {{ i18n.t.processNotFound }}
    </div>
    <p v-else class="section-desc" style="margin-top: 8px;">{{ i18n.t.processNameDesc }}</p>

    <div class="row-setting">
      <span>{{ i18n.t.autoHideTaskbar }}</span>
      <label class="switch-label">
        <input type="checkbox" :checked="autoHideTaskbar" @change="toggleSwitch('autoHideTaskbar', bridge.SetEnableTaskbarAutoHide)">
        <span class="slider"></span>
      </label>
    </div>
    <p class="section-desc" style="margin-top: 8px;">{{ i18n.t.autoHideTaskbarDesc }}</p>

    <div class="row-setting">
      <span>{{ i18n.t.displaySync }}</span>
      <label class="switch-label">
        <input type="checkbox" :checked="enableDisplaySync" @change="toggleSwitch('enableDisplaySync', bridge.SetEnableDisplaySync)">
        <span class="slider"></span>
      </label>
    </div>
    <p class="section-desc" style="margin-top: 8px;">{{ i18n.t.displaySyncDesc }}</p>
  </div>
</template>

<style scoped>
.section-title { font-size: 16px; font-weight: 900; color: var(--text-main); display: flex; align-items: center; gap: 10px; margin-bottom: 18px; text-transform: uppercase; letter-spacing: 1px; }
.section-title::before { content: ''; display: block; width: 6px; height: 16px; background: var(--primary-color); border-radius: 0 4px 0 4px; }
.section-desc { font-size: 11px; color: var(--text-muted); line-height: 1.6; margin-top: -12px; margin-bottom: 20px; font-weight: 600; }
.input-label { display: block; font-size: 12px; color: var(--text-muted); margin-bottom: 8px; font-weight: bold; text-transform: uppercase; }
.input-wrapper { position: relative; display: flex; align-items: center; }
input[type="text"] { width: 100%; background: var(--input-bg); border: 2px solid var(--input-stroke); color: var(--text-main); padding: 12px 14px; border-radius: var(--shape-radius); font-size: 14px; font-weight: 600; outline: none; transition: all 0.2s; }
input[type="text"]:focus { border-color: var(--primary-color); box-shadow: 0 0 12px rgba(255, 140, 0, 0.15); }
input.error { border-color: var(--danger-color) !important; }
.error-text { color: var(--danger-color); font-size: 11px; margin-top: 4px; font-weight: bold; text-transform: uppercase; cursor: pointer; display: flex; align-items: center; gap: 4px; margin-bottom: 20px; }
.status-icon { position: absolute; right: 14px; color: var(--primary-color); font-weight: 900; font-size: 16px; transition: opacity 0.3s, transform 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275); pointer-events: none; }
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
</style>
