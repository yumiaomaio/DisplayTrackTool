<script setup>
import { ref } from 'vue'
import { i18n } from '../i18n'
import { bridge } from '../services/bridge'

const emit = defineEmits(['back', 'complete'])

const selectedFileName = ref('')
const selectedBase64 = ref('')
const isDragOver = ref(false)

const urlName = ref('Immersive Auto Launch')
const startMenu = ref(true)
const desktop = ref(false)

const handleDrop = async (e) => {
  e.preventDefault()
  isDragOver.value = false
  const files = e.dataTransfer?.files
  if (!files || files.length === 0) return

  const file = files[0]
  if (!file.name.toLowerCase().endsWith('.ico')) return

  const reader = new FileReader()
  reader.onload = async () => {
    const dataUrl = reader.result
    const rawBase64 = dataUrl.split(',')[1]
    const result = await bridge.ImportDroppedIcon({ fileName: file.name, data: rawBase64 })
    if (result) {
      selectedFileName.value = result.fileName
      selectedBase64.value = result.base64
    }
  }
  reader.readAsDataURL(file)
}

const handleClickSelect = async () => {
  const result = await bridge.SelectIconFile()
  if (result) {
    selectedFileName.value = result.fileName
    selectedBase64.value = result.base64
  }
}

const handleCreate = async () => {
  const entries = [{
    name: urlName.value,
    locations: {
      startMenu: startMenu.value,
      desktop: desktop.value
    }
  }]
  await bridge.CreateAssociationUrls(JSON.stringify({
    iconFileName: selectedFileName.value || null,
    entries: entries
  }))
  emit('complete')
}
</script>

<template>
  <div class="registration-page">

    <div class="section-title">{{ i18n.t.iconRegistration.title }}</div>

    <div
      class="drop-zone"
      :class="{ 'drag-over': isDragOver }"
      @dragover.prevent="isDragOver = true"
      @dragleave="isDragOver = false"
      @drop="handleDrop"
      @click="handleClickSelect"
    >
      <template v-if="selectedBase64">
        <img :src="selectedBase64" class="icon-preview" alt="Icon preview">
        <p class="preview-label">{{ selectedFileName }}</p>
      </template>
      <template v-else>
        <div class="drop-placeholder">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" class="drop-icon">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
            <polyline points="17 8 12 3 7 8"></polyline>
            <line x1="12" y1="3" x2="12" y2="15"></line>
          </svg>
          <p class="drag-text">{{ i18n.t.iconRegistration.dragHere }}</p>
          <p class="click-hint">{{ i18n.t.iconRegistration.clickToSelect }}</p>
        </div>
      </template>
    </div>

    <div class="thick-divider"></div>

    <div class="section-title">{{ i18n.t.urlCreation.title }}</div>

    <label class="input-label">{{ i18n.t.urlCreation.urlNameLabel }}</label>
    <input
      type="text"
      v-model="urlName"
      class="text-input"
      placeholder="URL filename"
    >
    <p class="section-desc">{{ i18n.t.urlCreation.urlNameHint }}</p>

    <label class="input-label">{{ i18n.t.urlCreation.locationLabel }}</label>
    <div class="launch-tabs-grid">
      <div class="tab-item" :class="{ 'checked': startMenu }" @click="startMenu = !startMenu">
        <div class="checkbox-circle"></div>
        {{ i18n.t.urlCreation.startMenu }}
      </div>
      <div class="tab-item" :class="{ 'checked': desktop }" @click="desktop = !desktop">
        <div class="checkbox-circle"></div>
        {{ i18n.t.urlCreation.desktop }}
      </div>
    </div>

    <div class="button-row">
      <button class="back-btn" @click="emit('back')">{{ i18n.t.menu.cancel }}</button>
      <button class="create-btn" @click="handleCreate">{{ i18n.t.urlCreation.create }}</button>
    </div>
  </div>
</template>

<style scoped>
.registration-page {
  padding: 0 0 24px 0;
}

.button-row {
  display: flex;
  gap: 10px;
  margin-top: 8px;
}

.back-btn {
  flex: 1;
  background: var(--btn-bg);
  color: var(--text-main);
  border: none;
  height: 48px;
  border-radius: 999px;
  font-size: 14px;
  font-weight: bold;
  cursor: pointer;
  text-transform: uppercase;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  transition: transform 0.1s, background 0.2s, box-shadow 0.2s;
}

.back-btn:hover {
  background: var(--input-stroke);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12);
}

.back-btn:active {
  transform: scale(0.97);
}

.section-title {
  font-size: 16px;
  font-weight: 900;
  color: var(--text-main);
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 18px;
  text-transform: uppercase;
  letter-spacing: 1px;
}

.section-title::before {
  content: '';
  display: block;
  width: 6px;
  height: 16px;
  background: var(--primary-color);
  border-radius: 0 4px 0 4px;
}

.input-label {
  display: block;
  font-size: 12px;
  color: var(--text-muted);
  margin-bottom: 8px;
  font-weight: bold;
  text-transform: uppercase;
}

.text-input {
  width: 100%;
  background: var(--input-bg);
  border: 2px solid var(--input-stroke);
  color: var(--text-main);
  padding: 12px 14px;
  border-radius: var(--shape-radius);
  font-size: 14px;
  font-weight: 600;
  outline: none;
  transition: all 0.2s;
}

.text-input:focus {
  border-color: var(--primary-color);
  box-shadow: 0 0 12px rgba(255, 140, 0, 0.15);
}

.section-desc {
  font-size: 11px;
  color: var(--text-muted);
  line-height: 1.6;
  margin-top: 8px;
  margin-bottom: 20px;
  font-weight: 600;
}

.drop-zone {
  width: 100%;
  min-height: 140px;
  border: 2px dashed var(--input-stroke);
  border-radius: var(--shape-radius);
  background: var(--input-bg);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  cursor: pointer;
  transition: all 0.2s;
  padding: 20px;
}

.drop-zone:hover {
  border-color: var(--primary-color);
}

.drop-zone.drag-over {
  border-color: var(--primary-color);
  background: rgba(255, 140, 0, 0.08);
  box-shadow: 0 0 16px rgba(255, 140, 0, 0.15);
}

.drop-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.drop-icon {
  color: var(--input-stroke);
  margin-bottom: 4px;
}

.icon-preview {
  width: 96px;
  height: 96px;
  object-fit: contain;
}

.preview-label {
  font-size: 12px;
  font-weight: bold;
  color: var(--text-main);
}

.drag-text {
  font-size: 14px;
  font-weight: 900;
  color: var(--text-muted);
  text-transform: uppercase;
}

.click-hint {
  font-size: 11px;
  color: var(--text-muted);
  font-weight: 600;
}

.thick-divider {
  height: 4px;
  background: var(--divider-color);
  margin: 24px 0;
  border-radius: 2px;
  position: relative;
  overflow: hidden;
}

.thick-divider::after {
  content: '';
  position: absolute;
  left: -10px;
  top: 0;
  width: 60px;
  height: 100%;
  background: var(--primary-color);
  transform: skewX(-30deg);
}

.launch-tabs-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 10px;
  margin-bottom: 24px;
}

.tab-item {
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--input-bg);
  padding: 14px 10px;
  border: none;
  border-radius: 2px 12px 2px 12px;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 12px;
  font-weight: 900;
  color: var(--text-muted);
  user-select: none;
}

.tab-item:hover {
  background: rgba(255, 140, 0, 0.1);
}

.tab-item.checked {
  background: rgba(255, 140, 0, 0.15);
  color: var(--primary-color);
}

.checkbox-circle {
  width: 14px;
  height: 14px;
  border-radius: 50%;
  border: 2px solid var(--input-stroke);
  margin-right: 8px;
  background: var(--modal-bg);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.tab-item.checked .checkbox-circle::after {
  content: '';
  width: 6px;
  height: 6px;
  background: var(--primary-color);
  border-radius: 50%;
}

.create-btn {
  flex: 1;
  background: var(--primary-gradient);
  color: white;
  border: none;
  height: 48px;
  border-radius: 999px;
  font-size: 15px;
  font-weight: bold;
  cursor: pointer;
  text-transform: uppercase;
  box-shadow: 0 4px 12px rgba(255, 140, 0, 0.3);
  transition: transform 0.1s, filter 0.2s;
}

.create-btn:hover {
  filter: brightness(1.1);
}

.create-btn:active {
  transform: scale(0.97);
}
</style>
