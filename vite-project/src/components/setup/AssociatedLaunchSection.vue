<script setup>
import { ref } from 'vue'
import { i18n } from '../../i18n'
import { bridge } from '../../services/bridge'

const props = defineProps({
  associatedLaunchPath: String, processName: String,
  launchOnAppStartup: Boolean, launchOnTaskStart: Boolean,
  autoStartFromThirdParty: Boolean, autoStartMonitoringOnProtocolLaunch: Boolean
})
const emit = defineEmits([
  'update:associatedLaunchPath', 'update:launchOnAppStartup', 'update:launchOnTaskStart',
  'update:autoStartFromThirdParty', 'update:autoStartMonitoringOnProtocolLaunch', 'showProtocolModal'
])

const hoveredLaunchTab = ref(null)

const detectCommandLine = async () => {
  if (!props.processName) return
  const cmd = await bridge.GetProcessCommandLine(props.processName)
  if (cmd) { emit('update:associatedLaunchPath', cmd); bridge.SetAssociatedLaunchPath(cmd) }
}

const toggleTab = (key, bridgeFn) => () => {
  emit('update:' + key, !props[key]); bridgeFn(!props[key])
}

const onAutoStartFromThirdPartyChange = async () => {
  if (!props.autoStartFromThirdParty) {
    if (await bridge.IsProtocolRegistered()) {
      bridge.SetAutoStartFromThirdParty(true); emit('update:autoStartFromThirdParty', true)
    } else {
      emit('showProtocolModal')
    }
  } else {
    bridge.SetAutoStartFromThirdParty(false); emit('update:autoStartFromThirdParty', false)
  }
}
</script>

<template>
  <div>
    <div class="section-title">{{ i18n.t.associatedLaunch }}</div>
    <label class="input-label">{{ i18n.t.launchPath }}</label>
    <div class="input-wrapper" style="margin-bottom: 12px;">
      <input type="text"
        :value="associatedLaunchPath"
        @input="e => emit('update:associatedLaunchPath', e.target.value)"
        @change="e => bridge.SetAssociatedLaunchPath(e.target.value)"
        placeholder="steam://... or C:\Path\To\Game.exe"
        class="path-input" style="padding-right: 135px;">
      <div class="input-actions">
        <button class="action-btn detect-btn" @click="detectCommandLine" :title="i18n.t.detectCommandLine">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
        </button>
        <button class="action-btn browse-btn" @click="bridge.SelectAssociatedProgram()" :title="i18n.t.browse">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path></svg>
        </button>
        <button class="action-btn share-btn" @click="bridge.CreateDesktopShortcut()" :title="i18n.t.share">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="18" cy="5" r="3"></circle><circle cx="6" cy="12" r="3"></circle><circle cx="18" cy="19" r="3"></circle><line x1="8.59" y1="13.51" x2="15.42" y2="17.49"></line><line x1="15.41" y1="6.51" x2="8.59" y2="10.49"></line></svg>
        </button>
      </div>
    </div>
    <p class="section-desc" style="margin-bottom: 20px;margin-top: 8px;">{{ i18n.t.associatedLaunchDesc }}</p>

    <label class="input-label">{{ i18n.t.launchTiming }}</label>
    <div class="launch-tabs-grid">
      <div class="tab-item" :class="{ checked: autoStartFromThirdParty }"
        @mouseenter="hoveredLaunchTab = 'invoked'" @mouseleave="hoveredLaunchTab = null"
        @click="onAutoStartFromThirdPartyChange">
        <div class="checkbox-circle"></div>{{ i18n.t.tabInvoked }}
      </div>
      <div class="tab-item" :class="{ checked: launchOnAppStartup }"
        @mouseenter="hoveredLaunchTab = 'startup'" @mouseleave="hoveredLaunchTab = null"
        @click="toggleTab('launchOnAppStartup', bridge.SetLaunchOnAppStartup)()">
        <div class="checkbox-circle"></div>{{ i18n.t.tabStartup }}
      </div>
      <div class="tab-item" :class="{ checked: launchOnTaskStart }"
        @mouseenter="hoveredLaunchTab = 'task'" @mouseleave="hoveredLaunchTab = null"
        @click="toggleTab('launchOnTaskStart', bridge.SetLaunchOnTaskStart)()">
        <div class="checkbox-circle"></div>{{ i18n.t.tabTask }}
      </div>
    </div>

    <div style="margin-top: 12px; display: flex; align-items: center;">
      <p v-if="hoveredLaunchTab === 'invoked'" class="section-desc" style="margin: 0;">{{ i18n.t.autoStartFromThirdPartyDesc }}</p>
      <p v-else-if="hoveredLaunchTab === 'startup'" class="section-desc" style="margin: 0;">{{ i18n.t.launchOnAppStartupDesc }}</p>
      <p v-else-if="hoveredLaunchTab === 'task'" class="section-desc" style="margin: 0;">{{ i18n.t.launchOnTaskStartDesc }}</p>
      <p v-else class="section-desc" style="margin: 0;">{{ i18n.t.associatedLaunchDefaultDesc }}</p>
    </div>

    <template v-if="autoStartFromThirdParty">
      <div class="row-setting">
        <span style="border-bottom: 2px dashed var(--primary-color);padding-bottom: 4px;">{{ i18n.t.autoStartMonitoringOnProtocolLaunch }}</span>
        <label class="switch-label">
          <input type="checkbox" :checked="autoStartMonitoringOnProtocolLaunch"
            @change="e => { emit('update:autoStartMonitoringOnProtocolLaunch', e.target.checked); bridge.SetAutoStartMonitoringOnProtocolLaunch(e.target.checked) }">
          <span class="slider"></span>
        </label>
      </div>
      <p class="section-desc" style="margin-top: 8px;">{{ i18n.t.autoStartMonitoringOnProtocolLaunchDesc }}</p>
    </template>
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
.path-input { direction: rtl; text-align: left; text-overflow: ellipsis; }
.path-input:focus { direction: ltr; }
.input-actions { position: absolute; right: 6px; top: 6px; bottom: 6px; display: flex; gap: 2px; background: rgba(128,128,128,0.15); padding: 2px; border-radius: 999px; }
.action-btn { background: transparent; color: var(--text-main); border: none; border-radius: 999px; width: 30px; height: 100%; display: flex; justify-content: center; align-items: center; cursor: pointer; transition: all 0.2s; }
.action-btn:hover { background: var(--primary-color); color: white; }
.detect-btn svg { color: var(--text-main); }
.detect-btn:hover svg { color: white; }
.launch-tabs-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
.tab-item { display: flex; align-items: center; justify-content: center; background: var(--input-bg); padding: 14px 10px; border: none; border-radius: 2px 12px 2px 12px; cursor: pointer; transition: all 0.2s; font-size: 12px; font-weight: 900; color: var(--text-muted); position: relative; user-select: none; }
.tab-item:hover { background: rgba(255,140,0,0.1); }
.tab-item.checked { background: rgba(255,140,0,0.15); color: var(--primary-color); }
.tab-item.checked:hover { background: rgba(255,140,0,0.25); }
.checkbox-circle { width: 14px; height: 14px; border-radius: 50%; border: 2px solid var(--input-stroke); margin-right: 8px; background: var(--modal-bg); display: flex; align-items: center; justify-content: center; transition: all 0.2s; }
.tab-item.checked .checkbox-circle::after { content: ''; width: 6px; height: 6px; background: var(--primary-color); border-radius: 50%; }
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
