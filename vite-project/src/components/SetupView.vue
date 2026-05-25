<script setup>
import TargetCoreSection from './setup/TargetCoreSection.vue'
import AssociatedLaunchSection from './setup/AssociatedLaunchSection.vue'
import OverlaySection from './setup/OverlaySection.vue'

defineProps({
  processName: String, autoHideTaskbar: Boolean, enableDisplaySync: Boolean, enableOverlay: Boolean,
  associatedLaunchPath: String, launchOnAppStartup: Boolean, launchOnTaskStart: Boolean,
  autoStartFromThirdParty: Boolean, autoStartMonitoringOnProtocolLaunch: Boolean,
  bgMode: String, selectedColor: String, bgImage: String, propertyErrors: Object
})

const emit = defineEmits([
  'update:processName', 'update:autoHideTaskbar', 'update:enableDisplaySync', 'update:enableOverlay',
  'update:associatedLaunchPath', 'update:launchOnAppStartup', 'update:launchOnTaskStart',
  'update:autoStartFromThirdParty', 'update:autoStartMonitoringOnProtocolLaunch',
  'update:bgMode', 'update:selectedColor', 'update:bgImage', 'showProtocolModal'
])
</script>

<template>
  <div id="setup-view">
    <TargetCoreSection
      :processName="processName" :autoHideTaskbar="autoHideTaskbar" :enableDisplaySync="enableDisplaySync"
      @update:processName="v => emit('update:processName', v)"
      @update:autoHideTaskbar="v => emit('update:autoHideTaskbar', v)"
      @update:enableDisplaySync="v => emit('update:enableDisplaySync', v)"
    />

    <div class="thick-divider"></div>

    <AssociatedLaunchSection
      :associatedLaunchPath="associatedLaunchPath" :processName="processName"
      :launchOnAppStartup="launchOnAppStartup" :launchOnTaskStart="launchOnTaskStart"
      :autoStartFromThirdParty="autoStartFromThirdParty" :autoStartMonitoringOnProtocolLaunch="autoStartMonitoringOnProtocolLaunch"
      @update:associatedLaunchPath="v => emit('update:associatedLaunchPath', v)"
      @update:launchOnAppStartup="v => emit('update:launchOnAppStartup', v)"
      @update:launchOnTaskStart="v => emit('update:launchOnTaskStart', v)"
      @update:autoStartFromThirdParty="v => emit('update:autoStartFromThirdParty', v)"
      @update:autoStartMonitoringOnProtocolLaunch="v => emit('update:autoStartMonitoringOnProtocolLaunch', v)"
      @showProtocolModal="emit('showProtocolModal')"
    />

    <div class="thick-divider"></div>

    <OverlaySection
      :enableOverlay="enableOverlay" :bgMode="bgMode" :selectedColor="selectedColor" :bgImage="bgImage"
      @update:enableOverlay="v => emit('update:enableOverlay', v)"
      @update:bgMode="v => emit('update:bgMode', v)"
      @update:selectedColor="v => emit('update:selectedColor', v)"
      @update:bgImage="v => emit('update:bgImage', v)"
    />
  </div>
</template>

<style scoped>
.thick-divider { height: 4px; background: var(--divider-color); margin: 24px 0; border-radius: 2px; position: relative; overflow: hidden; }
.thick-divider::after { content: ''; position: absolute; left: -10px; top: 0; width: 60px; height: 100%; background: var(--primary-color); transform: skewX(-30deg); }
</style>
