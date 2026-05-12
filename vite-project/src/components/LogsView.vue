<script setup>
import { ref, onUpdated } from 'vue'
import { i18n } from '../i18n'

defineProps({
  logs: Array,
  isRunning: Boolean
})

const logsContainer = ref(null)

onUpdated(() => {
  if (logsContainer.value) {
    logsContainer.value.scrollTop = logsContainer.value.scrollHeight
  }
})
</script>

<template>
  <div class="logs-container-wrapper">
    <div class="thick-divider" style="margin: 30px 0 16px;"></div>
    <div class="logs-box">
      <div class="logs-header">{{ i18n.t.sysLog }}</div>
      <div :class="['logs-body', { 'is-running': isRunning }]" ref="logsContainer">
        <div v-for="(log, index) in logs" :key="index">
          {{ log }}
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.thick-divider { height: 4px; background: var(--divider-color); margin: 24px 0; border-radius: 2px; position: relative; overflow: hidden; }
.thick-divider::after {
    content: ''; position: absolute; left: -10px; top: 0; width: 60px; height: 100%;
    background: var(--primary-color); transform: skewX(-30deg);
}

.logs-box { background: #000; border-radius: var(--shape-radius); overflow: hidden; border: 2px solid var(--input-stroke); margin-top: auto; }
.logs-header { padding: 8px 12px; background: #1a1a1a; font-size: 11px; color: #999; font-weight: bold; border-bottom: 1px solid #333; }
.logs-body {
    padding: 12px; font-family: "Courier New", Courier, monospace; font-size: 11px; color: #39ff14;
    min-height: 80px; height: 120px; max-height: 200px; overflow-y: auto; line-height: 1.5;
    transition: height 0.3s ease;
}

.logs-body.is-running {
    height: 320px;
    max-height: 400px;
}

/* Custom Scrollbar for Logs Area */
.logs-body::-webkit-scrollbar {
    width: 6px;
}
.logs-body::-webkit-scrollbar-track {
    background: #000;
}
.logs-body::-webkit-scrollbar-thumb {
    background: #333;
    border-radius: 3px;
    transition: background 0.2s;
}
.logs-body::-webkit-scrollbar-thumb:hover {
    background: var(--primary-color);
}
</style>
