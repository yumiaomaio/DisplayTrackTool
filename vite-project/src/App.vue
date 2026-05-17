<script setup>
import { ref, onMounted } from 'vue'
import { i18n } from './i18n'
import { bridge, onStateChanged } from './services/bridge'
import AppHeader from './components/AppHeader.vue'
import SetupView from './components/SetupView.vue'
import RunningView from './components/RunningView.vue'
import LogsView from './components/LogsView.vue'
import OverlayModal from './components/OverlayModal.vue'

// --- State ---
const isRunning = ref(false)
const isLightMode = ref(true) 

// Debug Parameters
const debugParams = ref({
  animDuration: 4.5,
  minScale: 2.0,
  maxScale: 2.5,
  rippleMaxScale: 4.0,
  glowOpacity: 0.4
})

const processName = ref('')
const processIcon = ref('')
const autoHideTaskbar = ref(false)
const enableDisplaySync = ref(true)
const enableOverlay = ref(true)
const bgMode = ref('color') 
const selectedColor = ref('#ff8c00')
const bgImage = ref('')
const logs = ref([])
const shouldShowExitTip = ref(true)
const dontShowTipAgain = ref(false)

// Validation State
const propertyErrors = ref({})

// Modal State
const modal = ref({
  show: false,
  title: '',
  message: '',
  buttonText: '',
  secondaryButtonText: '',
  allowClose: true,
  type: 'info',
  onAction: () => {},
  onSecondaryAction: () => {}
})

// --- Methods ---
const toggleTheme = () => {
  isLightMode.value = !isLightMode.value
  document.body.classList.toggle('light', isLightMode.value)
}

const toggleRunState = () => {
  if (!isRunning.value) {
    if (!processName.value) return;

    if (shouldShowExitTip.value) {
      modal.value = {
        show: true,
        title: i18n.t.exitTip.title,
        message: i18n.t.exitTip.message,
        buttonText: i18n.t.exitTip.gotIt,
        showCheckbox: true,
        checkboxLabel: i18n.t.exitTip.dontShowAgain,
        allowClose: true,
        type: 'info',
        onAction: () => {
          if (dontShowTipAgain.value) {
            bridge.SetShowExitTip(false);
            shouldShowExitTip.value = false;
          }
          bridge.StartMonitoring(processName.value);
          modal.value.show = false;
        }
      }
    } else {
      bridge.StartMonitoring(processName.value);
    }
  } else {
    bridge.StopMonitoring();
  }
}

const openAbout = () => {
  bridge.ShowAbout();
}

const checkUAC = async () => {
  const isAdmin = await bridge.IsAdmin;
  if (!isAdmin) {
    modal.value = {
      show: true,
      title: i18n.t.uac.title,
      message: i18n.t.uac.message,
      buttonText: i18n.t.uac.button,
      secondaryButtonText: i18n.t.uac.secondaryButton,
      allowClose: false,
      type: 'warning',
      onAction: () => { 
        bridge.RestartAsAdmin();
        modal.value.show = false;
      },
      onSecondaryAction: () => {
        modal.value.show = false;
      }
    }
  }
}

async function init() {
  // Initial Data Fetch
  processName.value = await bridge.TargetProcessName;
  if (processName.value) {
    processIcon.value = await bridge.GetProcessIconBase64(processName.value);
  }
  
  autoHideTaskbar.value = await bridge.EnableTaskbarAutoHide;
  enableDisplaySync.value = await bridge.EnableDisplaySync;
  enableOverlay.value = await bridge.EnableBackgroundOverlay;
  isRunning.value = await bridge.IsRunning;
  shouldShowExitTip.value = await bridge.ShouldShowExitTip;

  // Handle Background Mode
  const mode = await bridge.BackgroundMode;
  if (mode === 'image') bgMode.value = 'image';
  else bgMode.value = 'color';
  
  // Handle Logs
  const initialLogs = await bridge.GetLogs();
  logs.value = initialLogs;

  // Handle Color (C# #AARRGGBB to CSS #RRGGBB)
  const bgColor = await bridge.BackgroundColor;
  if (bgColor && bgColor.startsWith('#')) {
    selectedColor.value = '#' + bgColor.substring(bgColor.length - 6);
  }

  // Handle Image
  const currentImg = await bridge.CurrentImageFileName;
  if (currentImg) {
    const base64 = await bridge.GetImageBase64(currentImg);
    if (base64) bgImage.value = base64;
  }

  checkUAC();
}

onMounted(() => {
  document.body.classList.toggle('light', isLightMode.value)
  
  init();

  onStateChanged((state) => {
    if (state.IsRunning !== undefined) {
      isRunning.value = state.IsRunning;
      if (state.IsRunning && processName.value) {
        bridge.GetProcessIconBase64(processName.value).then(icon => {
           if (icon) processIcon.value = icon;
        });
      }
    }
    if (state.Logs !== undefined) logs.value = state.Logs;
    if (state.TargetProcessName !== undefined) {
      processName.value = state.TargetProcessName;
      bridge.GetProcessIconBase64(state.TargetProcessName).then(icon => {
        processIcon.value = icon;
      });
    }
    if (state.BackgroundMode !== undefined) {
      bgMode.value = state.BackgroundMode;
    }
    if (state.PropertyErrors !== undefined) {
      propertyErrors.value = { ...propertyErrors.value, ...state.PropertyErrors };
    }
    if (state.CurrentImageFileName !== undefined) {
      if (state.CurrentImageFileName) {
        bridge.GetImageBase64(state.CurrentImageFileName).then(b64 => {
          bgImage.value = b64;
        });
      } else {
        bgImage.value = '';
      }
    }
  });
})
</script>

<template>
  <div class="app-wrapper" :style="{
    backgroundColor: bgMode === 'color' ? selectedColor : '#000',
    backgroundImage: bgMode === 'image' && bgImage ? `url(${bgImage})` : 'none',
    '--debug-duration': debugParams.animDuration + 's',
    '--debug-min-scale': debugParams.minScale,
    '--debug-max-scale': debugParams.maxScale,
    '--debug-ripple-scale': debugParams.rippleMaxScale,
    '--debug-glow-opacity': debugParams.glowOpacity
  }">
    <div class="modal-container">
      <AppHeader 
        :isRunning="isRunning" 
        :isLightMode="isLightMode" 
        @toggleTheme="toggleTheme" 
        @showAbout="openAbout"
      />

      <div class="scroll-wrapper">
        <SetupView 
          v-if="!isRunning"
          v-model:processName="processName"
          v-model:autoHideTaskbar="autoHideTaskbar"
          v-model:enableDisplaySync="enableDisplaySync"
          v-model:enableOverlay="enableOverlay"
          v-model:bgMode="bgMode"
          v-model:selectedColor="selectedColor"
          v-model:bgImage="bgImage"
          :propertyErrors="propertyErrors"
        />

        <RunningView 
          v-else
          :processName="processName"
          :processIcon="processIcon"
        />

        <LogsView :logs="logs" :isRunning="isRunning" />

        <div class="spacer"></div>
      </div>

      <div class="bottom-float-area">
        <button v-if="!isRunning" class="btn-primary" @click="toggleRunState">
          {{ i18n.t.initialize }}
        </button>
        <button v-else class="btn-stop-floating" @click="toggleRunState">
          {{ i18n.t.stop }}
        </button>
      </div>

      <!-- Global System Modal -->
      <OverlayModal 
        :show="modal.show"
        :title="modal.title"
        :message="modal.message"
        :buttonText="modal.buttonText"
        :secondaryButtonText="modal.secondaryButtonText"
        :showCheckbox="modal.showCheckbox"
        :checkboxLabel="modal.checkboxLabel"
        v-model:checkboxValue="dontShowTipAgain"
        :allowClose="modal.allowClose"
        :type="modal.type"
        @close="modal.show = false"
        @action="modal.onAction"
        @secondaryAction="modal.onSecondaryAction"
      />
    </div>
  </div>
</template>

<style scoped>
.app-wrapper {
    width: 100vw; height: 100vh;
    display: flex; justify-content: center; align-items: center;
    background-size: cover; background-position: center;
    transition: background 0.5s ease;
    overflow: hidden;
}

.modal-container {
    width: 100%; max-width: 420px; height: 100%;
    background: var(--bg-modal); border-radius: 0; 
    overflow: hidden; display: flex; flex-direction: column; position: relative;
    box-shadow: 0 0 100px rgba(0,0,0,0.5);
    border-left: 1px solid rgba(255,255,255,0.1);
    border-right: 1px solid rgba(255,255,255,0.1);
}

.scroll-wrapper { flex: 1; overflow-y: auto; padding: 24px; display: flex; flex-direction: column; }
.scroll-wrapper::-webkit-scrollbar { width: 4px; }
.scroll-wrapper::-webkit-scrollbar-thumb { background: var(--input-stroke); border-radius: 2px; }

.bottom-float-area { position: absolute; bottom: 0; left: 0; right: 0; padding: 20px; background: linear-gradient(to bottom, transparent, var(--bg-modal) 30%); z-index: 10; }

.btn-primary {
    background: var(--primary-gradient); 
    color: white; border: none; height: 52px; width: 100%; border-radius: 999px;
    font-size: 16px; font-weight: bold; cursor: pointer; text-transform: uppercase; 
    box-shadow: 0 6px 16px rgba(255, 140, 0, 0.4); 
    transition: transform 0.1s, filter 0.2s;
}
.btn-primary:active { transform: scale(0.97); }
.btn-primary:hover { filter: brightness(1.1); }

/* Floating Stop Button */
.btn-stop-floating {
    background: var(--primary-gradient); 
    color: white; border: none; height: 52px; width: 100%; border-radius: 999px;
    font-size: 16px; font-weight: bold; cursor: pointer; text-transform: uppercase; 
    box-shadow: 0 6px 16px rgba(255, 140, 0, 0.4); 
    transition: all 0.3s ease;
    animation: pulse-orange 2s infinite;
}
.btn-stop-floating:active { transform: scale(0.97); }
.btn-stop-floating:hover { 
    background: linear-gradient(135deg, #ff4d4f 0%, #ff1a1a 50%, #ff7875 100%); 
    box-shadow: 0 6px 20px rgba(255, 77, 79, 0.6);
    animation: pulse-red 1s infinite;
}

@keyframes pulse-orange {
    0% { box-shadow: 0 0 0 0 rgba(255, 140, 0, 0.7); }
    70% { box-shadow: 0 0 0 10px rgba(255, 140, 0, 0); }
    100% { box-shadow: 0 0 0 0 rgba(255, 140, 0, 0); }
}

@keyframes pulse-red {
    0% { box-shadow: 0 0 0 0 rgba(255, 77, 79, 0.8); }
    70% { box-shadow: 0 0 0 15px rgba(255, 77, 79, 0); }
    100% { box-shadow: 0 0 0 0 rgba(255, 77, 79, 0); }
}

.spacer { height: 75px; }
</style>
