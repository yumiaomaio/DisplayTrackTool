<script setup>
import { ref, onMounted, watch } from 'vue'
import { i18n } from './i18n'
import { bridge, onStateChanged } from './services/bridge'
import { useMenu } from './composables/useMenu'
import { useModal } from './composables/useModal'
import AppHeader from './components/AppHeader.vue'
import SetupView from './components/SetupView.vue'
import RunningView from './components/RunningView.vue'
import LogsView from './components/LogsView.vue'
import OverlayModal from './components/OverlayModal.vue'
import IconRegistration from './components/IconRegistration.vue'
import FloatingMenu from './components/FloatingMenu.vue'

// --- Menu ---
const { showMenu, toggleMenu, closeMenu } = useMenu()

// --- State ---
const isRunning = ref(false)
const isLightMode = ref(true)

const debugParams = ref({ animDuration: 4.5, minScale: 2.0, maxScale: 2.5, rippleMaxScale: 4.0, glowOpacity: 0.4 })

const processName = ref('')
const processIcon = ref('')
const associatedLaunchPath = ref('')
const launchOnAppStartup = ref(false)
const launchOnTaskStart = ref(false)
const autoStartFromThirdParty = ref(false)
const autoStartMonitoringOnProtocolLaunch = ref(false)
const autoHideTaskbar = ref(true)
const enableDisplaySync = ref(true)
const enableOverlay = ref(true)
const bgMode = ref('color')
const selectedColor = ref('#ff8c00')
const bgImage = ref('')
const logs = ref([])
const shouldShowExitTip = ref(true)
const dontShowTipAgain = ref(false)
const waitingCountdown = ref(0)
const propertyErrors = ref({})
const showRegistration = ref(false)

// Modal
const { modal, hide, showProcessNotFound, showWaiting, showExitTip, showRegisterAssociation, showProtocol, showUac, showError } = useModal()

// Watch for countdown
watch(waitingCountdown, (newVal) => {
  const val = Number(newVal)
  if (isRunning.value) { if (modal.value.show && modal.value.title === i18n.t.waitingTitle) hide(); return }
  if (val > 0) showWaiting(val)
  else if (val === 0 && modal.value.title === i18n.t.waitingTitle) hide()
  else if (val === -1) showProcessNotFound()
})

// --- Methods ---
const toggleTheme = () => { isLightMode.value = !isLightMode.value; document.body.classList.toggle('light', isLightMode.value) }

const tryStart = async () => {
  if (!processName.value) return
  if (!launchOnTaskStart.value && await bridge.CheckProcessExists(processName.value) === false) {
    showProcessNotFound(); return
  }
  bridge.StartMonitoring(processName.value)
}

const toggleRunState = () => {
  if (!isRunning.value) {
    if (!processName.value) return
    if (shouldShowExitTip.value) {
      showExitTip(() => { if (dontShowTipAgain.value) { bridge.SetShowExitTip(false); shouldShowExitTip.value = false } tryStart(); hide() })
    } else { tryStart() }
  } else { bridge.StopMonitoring() }
}

const onCleanAssociation = () => bridge.CleanAssociation()

const onRegisterAssociation = () => {
  showRegisterAssociation(
    () => { showRegistration.value = true; hide() },
    async () => { await bridge.QuickRegisterAssociation(); hide() })
}

const showProtocolModal = () => {
  showProtocol(
    async () => { if (await bridge.RegisterProtocol()) { bridge.SetAutoStartFromThirdParty(true); autoStartFromThirdParty.value = true; hide() } else showError(i18n.t.protocolRegisterError) },
    hide)
}

const checkUAC = async () => {
  if (await bridge.ShouldShowUacPrompt()) showUac(() => { bridge.RestartAsAdmin(); hide() }, hide)
}

async function init() {
  const state = await bridge.GetInitialState()
  if (!state) return

  processName.value = state.targetProcessName || ''
  if (processName.value) { bridge.GetProcessIconBase64(processName.value).then(icon => { if (icon) processIcon.value = icon }) }

  autoHideTaskbar.value = state.enableTaskbarAutoHide
  enableDisplaySync.value = state.enableDisplaySync
  enableOverlay.value = state.enableBackgroundOverlay
  associatedLaunchPath.value = state.associatedLaunchPath
  launchOnAppStartup.value = state.launchOnAppStartup
  launchOnTaskStart.value = state.launchOnTaskStart
  autoStartFromThirdParty.value = state.autoStartFromThirdParty
  autoStartMonitoringOnProtocolLaunch.value = state.autoStartMonitoringOnProtocolLaunch
  isRunning.value = state.isRunning
  shouldShowExitTip.value = state.shouldShowExitTip
  waitingCountdown.value = state.waitingCountdown
  bgMode.value = state.backgroundMode === 'image' ? 'image' : 'color'
  logs.value = state.logs || []
  if (state.backgroundColor && state.backgroundColor.startsWith('#')) {
    selectedColor.value = '#' + state.backgroundColor.substring(state.backgroundColor.length - 6)
  }
  if (state.backgroundImageFileName) {
    bridge.GetImageBase64(state.backgroundImageFileName).then(b64 => { if (b64) bgImage.value = b64 })
  }
  checkUAC()
}

onMounted(() => {
  document.body.classList.toggle('light', isLightMode.value)
  init()
  onStateChanged((state) => {
    if (state.isRunning !== undefined) {
      isRunning.value = state.isRunning
      if (state.isRunning && modal.value.title === i18n.t.waitingTitle) hide()
      if (state.isRunning && processName.value) bridge.GetProcessIconBase64(processName.value).then(icon => { if (icon) processIcon.value = icon })
    }
    if (state.logs !== undefined) logs.value = state.logs
    if (state.targetProcessName !== undefined) { processName.value = state.targetProcessName; bridge.GetProcessIconBase64(state.targetProcessName).then(icon => { processIcon.value = icon }) }
    if (state.backgroundMode !== undefined) bgMode.value = String(state.backgroundMode).toLowerCase()
    if (state.enableTaskbarAutoHide !== undefined) autoHideTaskbar.value = state.enableTaskbarAutoHide
    if (state.enableDisplaySync !== undefined) enableDisplaySync.value = state.enableDisplaySync
    if (state.propertyErrors !== undefined) propertyErrors.value = { ...propertyErrors.value, ...state.propertyErrors }
    if (state.backgroundImageFileName !== undefined) { state.backgroundImageFileName ? bridge.GetImageBase64(state.backgroundImageFileName).then(b64 => { bgImage.value = b64 }) : (bgImage.value = '') }
    if (state.associatedLaunchPath !== undefined) associatedLaunchPath.value = state.associatedLaunchPath
    if (state.launchOnAppStartup !== undefined) launchOnAppStartup.value = state.launchOnAppStartup
    if (state.launchOnTaskStart !== undefined) launchOnTaskStart.value = state.launchOnTaskStart
    if (state.autoStartFromThirdParty !== undefined) autoStartFromThirdParty.value = state.autoStartFromThirdParty
    if (state.autoStartMonitoringOnProtocolLaunch !== undefined) autoStartMonitoringOnProtocolLaunch.value = state.autoStartMonitoringOnProtocolLaunch
    if (state.waitingCountdown !== undefined) waitingCountdown.value = state.waitingCountdown
  })
})
</script>

<template>
  <div class="app-wrapper" :style="{
    backgroundColor: bgMode === 'color' ? selectedColor : '#000',
    backgroundImage: bgMode === 'image' && bgImage ? `url(${bgImage})` : 'none',
    '--debug-duration': debugParams.animDuration + 's', '--debug-min-scale': debugParams.minScale,
    '--debug-max-scale': debugParams.maxScale, '--debug-ripple-scale': debugParams.rippleMaxScale,
    '--debug-glow-opacity': debugParams.glowOpacity
  }">
    <div class="modal-container">
      <AppHeader :is-running="isRunning" :is-light-mode="isLightMode"
        @toggle-theme="toggleTheme" @show-about="bridge.ShowAbout()"
        @register-association="onRegisterAssociation" @clean-association="onCleanAssociation" />

      <div class="scroll-wrapper">
        <IconRegistration v-if="showRegistration"
          @back="showRegistration = false" @complete="showRegistration = false" />
        <template v-else>
          <SetupView v-if="!isRunning"
            v-model:processName="processName" v-model:autoHideTaskbar="autoHideTaskbar"
            v-model:enableDisplaySync="enableDisplaySync" v-model:enableOverlay="enableOverlay"
            v-model:associatedLaunchPath="associatedLaunchPath" v-model:launchOnAppStartup="launchOnAppStartup"
            v-model:launchOnTaskStart="launchOnTaskStart" v-model:autoStartFromThirdParty="autoStartFromThirdParty"
            v-model:autoStartMonitoringOnProtocolLaunch="autoStartMonitoringOnProtocolLaunch"
            v-model:bgMode="bgMode" v-model:selectedColor="selectedColor" v-model:bgImage="bgImage"
            :propertyErrors="propertyErrors" @show-protocol-modal="showProtocolModal" />
          <RunningView v-else :processName="processName" :processIcon="processIcon" />
          <LogsView :logs="logs" :isRunning="isRunning" />
          <div class="spacer"></div>
        </template>
      </div>

      <div class="bottom-float-area" v-if="!showRegistration">
        <button v-if="!isRunning" class="btn-primary" @click="toggleRunState">{{ i18n.t.initialize }}</button>
        <button v-else class="btn-stop-floating" @click="toggleRunState">{{ i18n.t.stop }}</button>
        <button class="menu-fab" @click="toggleMenu" title="Menu">
          <img src="./star.webp" class="fab-icon" alt="Menu">
        </button>
        <FloatingMenu :show="showMenu" :isLightMode="isLightMode"
          @toggleTheme="toggleTheme" @registerAssociation="onRegisterAssociation"
          @cleanAssociation="onCleanAssociation" @about="bridge.ShowAbout()" @close="closeMenu" />
      </div>

      <OverlayModal :show="modal.show" :title="modal.title" :message="modal.message"
        :buttonText="modal.buttonText" :secondaryButtonText="modal.secondaryButtonText"
        :showCheckbox="modal.showCheckbox" :checkboxLabel="modal.checkboxLabel"
        v-model:checkboxValue="dontShowTipAgain" :allowClose="modal.allowClose" :type="modal.type"
        @close="modal.show = false" @action="modal.onAction" @secondaryAction="modal.onSecondaryAction" />
    </div>
  </div>
</template>

<style scoped>
.app-wrapper { width: 100vw; height: 100vh; display: flex; justify-content: center; align-items: center; background-size: cover; background-position: center; transition: background 0.5s ease; overflow: hidden; }
.modal-container { width: 100%; max-width: 450px; height: 100%; background: var(--bg-modal); border-radius: 0; overflow: hidden; display: flex; flex-direction: column; position: relative; box-shadow: 0 0 100px rgba(0,0,0,0.5); }
.scroll-wrapper { flex: 1; overflow-y: auto; padding: 24px; display: flex; flex-direction: column; }
.scroll-wrapper::-webkit-scrollbar { width: 4px; }
.scroll-wrapper::-webkit-scrollbar-thumb { background: var(--input-stroke); border-radius: 2px; }
.bottom-float-area { position: absolute; bottom: 0; left: 0; right: 0; padding: 20px; background: linear-gradient(to bottom, transparent, var(--bg-modal) 30%); z-index: 10; display: flex; align-items: center; gap: 10px; }
.btn-primary { flex: 1; background: var(--primary-gradient); color: white; border: none; height: 52px; border-radius: 999px; font-size: 16px; font-weight: bold; cursor: pointer; text-transform: uppercase; box-shadow: 0 6px 16px rgba(255,140,0,0.4); transition: transform 0.1s, filter 0.2s; }
.btn-primary:active { transform: scale(0.97); }
.btn-primary:hover { filter: brightness(1.1); }
.menu-fab { flex-shrink: 0; width: 52px; height: 52px; border-radius: 50%; background: var(--bg-modal); border: 2.5px solid rgba(255,140,0,0.5); color: rgba(255,140,0,0.8); cursor: pointer; display: flex; align-items: center; justify-content: center; box-shadow: 0 6px 16px rgba(255,140,0,0.4); transition: all 0.2s; }
.menu-fab:hover { border-color: rgba(255,140,0,0.75); box-shadow: 0 6px 20px rgba(255,140,0,0.5); transform: scale(1.05); }
.menu-fab:active { transform: scale(0.95); }
.fab-icon { width: 100%; height: 100%; border-radius: 50%; object-fit: cover; }
.btn-stop-floating { flex: 1; background: var(--primary-gradient); color: white; border: none; height: 52px; border-radius: 999px; font-size: 16px; font-weight: bold; cursor: pointer; text-transform: uppercase; box-shadow: 0 6px 16px rgba(255,140,0,0.4); transition: all 0.3s ease; animation: pulse-orange 2s infinite; }
.btn-stop-floating:active { transform: scale(0.97); }
.btn-stop-floating:hover { background: linear-gradient(135deg, #ff4d4f 0%, #ff1a1a 50%, #ff7875 100%); box-shadow: 0 6px 20px rgba(255,77,79,0.6); animation: pulse-red 1s infinite; }
@keyframes pulse-orange { 0% { box-shadow: 0 0 0 0 rgba(255,140,0,0.7); } 70% { box-shadow: 0 0 0 10px rgba(255,140,0,0); } 100% { box-shadow: 0 0 0 0 rgba(255,140,0,0); } }
@keyframes pulse-red { 0% { box-shadow: 0 0 0 0 rgba(255,77,79,0.8); } 70% { box-shadow: 0 0 0 15px rgba(255,77,79,0); } 100% { box-shadow: 0 0 0 0 rgba(255,77,79,0); } }
.spacer { height: 75px; }
</style>
