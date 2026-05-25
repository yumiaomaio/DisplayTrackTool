<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { i18n, setLanguage } from './i18n'
import { bridge, onStateChanged } from './services/bridge'
import AppHeader from './components/AppHeader.vue'
import SetupView from './components/SetupView.vue'
import RunningView from './components/RunningView.vue'
import LogsView from './components/LogsView.vue'
import OverlayModal from './components/OverlayModal.vue'
import IconRegistration from './components/IconRegistration.vue'

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

// Registration page state
const showRegistration = ref(false)
const showMenu = ref(false)

const toggleMenu = (e) => {
  if (e) e.stopPropagation()
  showMenu.value = !showMenu.value
}

const closeMenu = () => { showMenu.value = false }

const switchLanguage = () => {
  setLanguage(i18n.lang === 'en' ? 'zh' : 'en')
}

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
  const val = Number(newVal);
  if (isRunning.value) {
    if (modal.value.show && modal.value.title === i18n.t.waitingTitle) {
      modal.value.show = false;
    }
    return;
  }

  if (val > 0) {
    modal.value = {
      show: true,
      title: i18n.t.waitingTitle,
      message: `${i18n.t.waitingMessage} (${val}${i18n.t.seconds})`,
      buttonText: i18n.t.protocolCancel,
      allowClose: false,
      type: 'info',
      onAction: () => {
        bridge.StopMonitoring();
        modal.value.show = false;
      }
    };
  } else if (val === 0 && modal.value.title === i18n.t.waitingTitle) {
    modal.value.show = false;
  } else if (val === -1) {
    modal.value = {
      show: true,
      title: i18n.t.timeoutTitle,
      message: i18n.t.processNotFound,
      buttonText: i18n.t.menu.close,
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

const tryStart = async () => {
  if (!processName.value) return;

  // Skip frontend check when associated launch is enabled — let backend handle it
  if (!launchOnTaskStart.value) {
    const exists = await bridge.CheckProcessExists(processName.value);
    if (exists === false) {
      modal.value = {
        show: true,
        title: i18n.t.timeoutTitle,
        message: i18n.t.processNotFound,
        buttonText: i18n.t.menu.close,
        allowClose: true,
        type: 'warning',
        onAction: () => { modal.value.show = false; }
      };
      return;
    }
  }
  bridge.StartMonitoring(processName.value);
};

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
          tryStart();
          modal.value.show = false;
        }
      }
    } else {
      tryStart();
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

const onRegisterAssociation = () => {
  modal.value = {
    show: true,
    title: i18n.t.registerModal.title,
    message: i18n.t.registerModal.message,
    buttonText: i18n.t.registerModal.yes,
    secondaryButtonText: i18n.t.registerModal.no,
    allowClose: true,
    type: 'info',
    onAction: () => {
      showRegistration.value = true
      modal.value.show = false
    },
    onSecondaryAction: async () => {
      await bridge.QuickRegisterAssociation()
      modal.value.show = false
    }
  }
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
  const shouldShow = await bridge.ShouldShowUacPrompt();
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
  // Call the C# backend to get the full state object via one RPC call
  const state = await bridge.GetInitialState();
  if (!state) return;

  // Map state to reactive refs
  processName.value = state.targetProcessName || '';
  if (processName.value) {
    bridge.GetProcessIconBase64(processName.value).then(icon => {
      if (icon) processIcon.value = icon;
    });
  }
  
  autoHideTaskbar.value = state.enableTaskbarAutoHide;
  enableDisplaySync.value = state.enableDisplaySync;
  enableOverlay.value = state.enableBackgroundOverlay;
  associatedLaunchPath.value = state.associatedLaunchPath;
  launchOnAppStartup.value = state.launchOnAppStartup;
  launchOnTaskStart.value = state.launchOnTaskStart;
  autoStartFromThirdParty.value = state.autoStartFromThirdParty;
  autoStartMonitoringOnProtocolLaunch.value = state.autoStartMonitoringOnProtocolLaunch;
  isRunning.value = state.isRunning;
  shouldShowExitTip.value = state.shouldShowExitTip;
  waitingCountdown.value = state.waitingCountdown;

  // Handle Background Mode
  bgMode.value = state.backgroundMode === 'image' ? 'image' : 'color';
  
  // Handle Logs
  logs.value = state.logs || [];

  // Handle Color (C# #AARRGGBB to CSS #RRGGBB)
  if (state.backgroundColor && state.backgroundColor.startsWith('#')) {
    selectedColor.value = '#' + state.backgroundColor.substring(state.backgroundColor.length - 6);
  }

  // Handle Image
  if (state.backgroundImageFileName) {
    bridge.GetImageBase64(state.backgroundImageFileName).then(b64 => {
      if (b64) bgImage.value = b64;
    });
  }

  checkUAC();
}

onMounted(() => {
  document.body.classList.toggle('light', isLightMode.value)
  
  init();

  onStateChanged((state) => {
    if (state.isRunning !== undefined) {
      isRunning.value = state.isRunning;
      // If we transition to running, ensure the waiting modal is dismissed
      if (state.isRunning && modal.value.title === i18n.t.waitingTitle) {
          modal.value.show = false;
      }
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
      bgMode.value = String(state.backgroundMode).toLowerCase();
    }
    if (state.propertyErrors !== undefined) {
      propertyErrors.value = { ...propertyErrors.value, ...state.propertyErrors };
    }
    if (state.backgroundImageFileName !== undefined) {
      if (state.backgroundImageFileName) {
        bridge.GetImageBase64(state.backgroundImageFileName).then(b64 => {
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

  window.addEventListener('click', closeMenu)
})

onUnmounted(() => {
  window.removeEventListener('click', closeMenu)
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
        @register-association="onRegisterAssociation"
        @clean-association="onCleanAssociation"
      />


      <div class="scroll-wrapper">
        <IconRegistration
          v-if="showRegistration"
          @back="showRegistration = false"
          @complete="showRegistration = false"
        />
        <template v-else>
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
        </template>
      </div>

      <div class="bottom-float-area" v-if="!showRegistration">
        <button v-if="!isRunning" class="btn-primary" @click="toggleRunState">
          {{ i18n.t.initialize }}
        </button>
        <button v-else class="btn-stop-floating" @click="toggleRunState">
          {{ i18n.t.stop }}
        </button>
        <button class="menu-fab" @click="toggleMenu" title="Menu">
          <img src="./star.webp" class="fab-icon" alt="Menu">
        </button>

        <Transition name="fade">
          <div v-if="showMenu" class="floating-menu" @click.stop>
            <div class="menu-item" @click="toggleTheme(); closeMenu()">
              <span>{{ isLightMode ? i18n.t.menu.dark : i18n.t.menu.light }}</span>
              <span class="menu-icon">
                <svg v-if="isLightMode" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path></svg>
                <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"></circle><line x1="12" y1="1" x2="12" y2="3"></line><line x1="12" y1="21" x2="12" y2="23"></line><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line><line x1="1" y1="12" x2="3" y2="12"></line><line x1="21" y1="12" x2="23" y2="12"></line><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line></svg>
              </span>
            </div>
            <div class="menu-item" @click="switchLanguage(); closeMenu()">
              <span>{{ i18n.lang === 'en' ? '中文' : 'English' }}</span>
              <span class="menu-icon">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="2" y1="12" x2="22" y2="12"></line><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path></svg>
              </span>
            </div>
            <div class="menu-item" @click="onRegisterAssociation(); closeMenu()">
              <span>{{ i18n.t.menu.registerAssociation }}</span>
              <span class="menu-icon">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path></svg>
              </span>
            </div>
            <div class="menu-item" @click="onCleanAssociation(); closeMenu()">
              <span>{{ i18n.t.menu.cleanAssociation }}</span>
              <span class="menu-icon">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"></path><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"></path><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"></path></svg>
              </span>
            </div>
            <div class="menu-item" @click="onAbout(); closeMenu()">
              <span>{{ i18n.t.menu.about }}</span>
              <span class="menu-icon">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
              </span>
            </div>
          </div>
        </Transition>
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
    width: 100%; max-width: 450px; height: 100%;
    background: var(--bg-modal); border-radius: 0; 
    overflow: hidden; display: flex; flex-direction: column; position: relative;
    box-shadow: 0 0 100px rgba(0,0,0,0.5);
}

.scroll-wrapper { flex: 1; overflow-y: auto; padding: 24px; display: flex; flex-direction: column; }
.scroll-wrapper::-webkit-scrollbar { width: 4px; }
.scroll-wrapper::-webkit-scrollbar-thumb { background: var(--input-stroke); border-radius: 2px; }

.bottom-float-area { position: absolute; bottom: 0; left: 0; right: 0; padding: 20px; background: linear-gradient(to bottom, transparent, var(--bg-modal) 30%); z-index: 10; display: flex; align-items: center; gap: 10px; }

.btn-primary {
    flex: 1;
    background: var(--primary-gradient);
    color: white; border: none; height: 52px; border-radius: 999px;
    font-size: 16px; font-weight: bold; cursor: pointer; text-transform: uppercase;
    box-shadow: 0 6px 16px rgba(255, 140, 0, 0.4);
    transition: transform 0.1s, filter 0.2s;
}
.btn-primary:active { transform: scale(0.97); }
.btn-primary:hover { filter: brightness(1.1); }

.menu-fab {
    flex-shrink: 0;
    width: 52px; height: 52px;
    border-radius: 50%;
    background: var(--bg-modal);
    border: 2.5px solid rgba(255,140,0,0.5);
    color: rgba(255,140,0,0.8);
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 6px 16px rgba(255,140,0,0.4);
    transition: all 0.2s;
}
.menu-fab:hover {
    border-color: rgba(255,140,0,0.75);
    box-shadow: 0 6px 20px rgba(255,140,0,0.5);
    transform: scale(1.05);
}
.menu-fab:active { transform: scale(0.95); }

.fab-icon { width: 100%; height: 100%; border-radius: 50%; object-fit: cover; }

/* Floating Menu */
.floating-menu {
    position: absolute; bottom: 76px; right: 20px; width: 170px;
    background: var(--bg-modal); border: 2px solid rgba(255,140,0,0.3);
    border-radius: 16px; box-shadow: 0 4px 20px rgba(255,140,0,0.3);
    padding: 6px; z-index: 101; overflow: hidden;
}

.menu-item {
    padding: 12px 14px; display: flex; justify-content: space-between; align-items: center;
    cursor: pointer; font-size: 13px; font-weight: 700; color: var(--text-main);
    transition: all 0.2s; border-radius: 6px;
}
.menu-item:hover { background: var(--btn-bg); color: #EBA832; }
.menu-icon { display: flex; align-items: center; justify-content: center; opacity: 0.8; }
.menu-item:hover .menu-icon { opacity: 1; }

.fade-enter-active, .fade-leave-active { transition: opacity 0.2s, transform 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; transform: translateY(10px); }

/* Floating Stop Button */
.btn-stop-floating {
    flex: 1;
    background: var(--primary-gradient);
    color: white; border: none; height: 52px; border-radius: 999px;
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
