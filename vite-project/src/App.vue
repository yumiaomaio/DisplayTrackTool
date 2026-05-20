<script setup>
import { ref, onMounted, watch } from 'vue'
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

// Validation State
const propertyErrors = ref({})

// Modal State
const modal = ref({
  show: false,
  title: '',
  message: '',
  buttonText: '',
  secondaryButtonText: '',
  showCheckbox: false,
  checkboxLabel: '',
  allowClose: true,
  type: 'info',
  onAction: () => {},
  onSecondaryAction: () => {}
})

// Watch for countdown to show modal
watch(waitingCountdown, (newVal) => {
  if (newVal > 0) {
    modal.value = {
      show: true,
      title: i18n.t.waitingTitle,
      message: `${i18n.t.waitingMessage} (${newVal}${i18n.t.seconds})`,
      buttonText: i18n.t.protocolCancel,
      allowClose: false,
      type: 'info',
      onAction: () => {
        bridge.StopMonitoring();
        modal.value.show = false;
      }
    };
  } else if (newVal === 0 && modal.value.title === i18n.t.waitingTitle) {
    modal.value.show = false;
  } else if (newVal === -1) {
    modal.value = {
      show: true,
      title: 'TIMEOUT',
      message: i18n.t.processNotFound,
      buttonText: 'OK',
      allowClose: true,
      type: 'warning',
      onAction: () => { modal.value.show = false; }
    };
  }
});

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

const onCleanAssociation = () => {
  bridge.CleanAssociation();
}

const showProtocolModal = () => {
  modal.value = {
    show: true,
    title: i18n.t.protocolModalTitle,
    message: i18n.t.protocolModalMessage,
    buttonText: i18n.t.protocolConfirm,
    secondaryButtonText: i18n.t.protocolCancel,
    allowClose: true,
    type: 'info',
    onAction: async () => {
      const success = await bridge.RegisterProtocol();
      if (success) {
        bridge.SetAutoStartFromThirdParty(true);
        autoStartFromThirdParty.value = true;
        modal.value.show = false;
      } else {
        modal.value = {
          show: true,
          title: i18n.t.errorTitle,
          message: i18n.t.protocolRegisterError,
          buttonText: 'OK',
          allowClose: true,
          type: 'warning',
          onAction: () => {
            modal.value.show = false;
          }
        }
      }
    },
    onSecondaryAction: () => {
      modal.value.show = false;
    }
  }
}

const onAbout = () => {
  bridge.ShowAbout();
}

const checkUAC = async () => {
  const shouldShow = await bridge.ShouldShowUacPrompt;
  if (shouldShow) {
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
  associatedLaunchPath.value = await bridge.AssociatedLaunchPath;
  launchOnAppStartup.value = await bridge.LaunchOnAppStartup;
  launchOnTaskStart.value = await bridge.LaunchOnTaskStart;
  autoStartFromThirdParty.value = await bridge.AutoStartFromThirdParty;
  autoStartMonitoringOnProtocolLaunch.value = await bridge.AutoStartMonitoringOnProtocolLaunch;
  isRunning.value = await bridge.IsRunning;
  shouldShowExitTip.value = await bridge.ShouldShowExitTip;
  waitingCountdown.value = await bridge.WaitingCountdown;

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
    if (state.isRunning !== undefined) {
      isRunning.value = state.isRunning;
      if (state.isRunning && processName.value) {
        bridge.GetProcessIconBase64(processName.value).then(icon => {
           if (icon) processIcon.value = icon;
        });
      }
    }
    if (state.logs !== undefined) logs.value = state.logs;
    if (state.targetProcessName !== undefined) {
      processName.value = state.targetProcessName;
      bridge.GetProcessIconBase64(state.targetProcessName).then(icon => {
        processIcon.value = icon;
      });
    }
    if (state.backgroundMode !== undefined) {
      bgMode.value = state.backgroundMode;
    }
    if (state.propertyErrors !== undefined) {
      propertyErrors.value = { ...propertyErrors.value, ...state.propertyErrors };
    }
    if (state.currentImageFileName !== undefined) {
      if (state.currentImageFileName) {
        bridge.GetImageBase64(state.currentImageFileName).then(b64 => {
          bgImage.value = b64;
        });
      } else {
        bgImage.value = '';
      }
    }
    if (state.associatedLaunchPath !== undefined) {
        associatedLaunchPath.value = state.associatedLaunchPath;
    }
    if (state.launchOnAppStartup !== undefined) {
        launchOnAppStartup.value = state.launchOnAppStartup;
    }
    if (state.launchOnTaskStart !== undefined) {
        launchOnTaskStart.value = state.launchOnTaskStart;
    }
    if (state.autoStartFromThirdParty !== undefined) {
        autoStartFromThirdParty.value = state.autoStartFromThirdParty;
    }
    if (state.autoStartMonitoringOnProtocolLaunch !== undefined) {
        autoStartMonitoringOnProtocolLaunch.value = state.autoStartMonitoringOnProtocolLaunch;
    }
    if (state.waitingCountdown !== undefined) {
        waitingCountdown.value = state.waitingCountdown;
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
        :is-running="isRunning" 
        :is-light-mode="isLightMode"
        @toggle-theme="toggleTheme"
        @show-about="onAbout"
        @clean-association="onCleanAssociation"
      />


      <div class="scroll-wrapper">
        <SetupView 
          v-if="!isRunning"
          v-model:processName="processName"
          v-model:autoHideTaskbar="autoHideTaskbar"
          v-model:enableDisplaySync="enableDisplaySync"
          v-model:enableOverlay="enableOverlay"
          v-model:associatedLaunchPath="associatedLaunchPath"
          v-model:launchOnAppStartup="launchOnAppStartup"
          v-model:launchOnTaskStart="launchOnTaskStart"
          v-model:autoStartFromThirdParty="autoStartFromThirdParty"
          v-model:autoStartMonitoringOnProtocolLaunch="autoStartMonitoringOnProtocolLaunch"
          v-model:bgMode="bgMode"
          v-model:selectedColor="selectedColor"
          v-model:bgImage="bgImage"
          :propertyErrors="propertyErrors"
          @show-protocol-modal="showProtocolModal"
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
