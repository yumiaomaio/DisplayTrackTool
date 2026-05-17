<script setup>
import { i18n } from '../i18n'

defineProps({
  processName: String,
  processIcon: String
})

defineEmits(['stop'])
</script>

<template>
  <div id="running-view" class="run-container">
    <!-- Hardcore Status Icon: Use theme-gold for status elements -->
    <div class="status-icon-container">
      <svg class="ripple-svg" viewBox="0 0 200 200">
        <circle class="ripple-circle" cx="100" cy="100" r="30" />
        <circle class="ripple-circle delay-1" cx="100" cy="100" r="30" />
      </svg>
      <div class="status-halo"></div>
      <div class="status-square-inner">
        <img :src="processIcon || '/favicon.svg'" class="status-favicon" alt="Status Icon">
      </div>
    </div>

    <!-- Title moved below Icon and above Section Divider -->
    <div class="run-title-row">
      <div class="run-title">{{ i18n.t.activeOverride }}</div>
    </div>

    <div class="run-info-section">
      <div class="info-grid">
        <div class="info-item">
          <label>{{ i18n.t.targetProcess }}</label>
          <span>{{ processName || 'TargetApp.exe' }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.run-container { 
    width: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    position: relative;
    padding-top: 20px;
}

/* Atmospheric Status Icon using Theme Orange */
.status-icon-container {
    position: relative;
    width: 160px;
    height: 160px;
    margin-bottom: 20px;
    display: flex;
    justify-content: center;
    align-items: center;
}

.ripple-svg {
    position: absolute;
    width: 100%;
    height: 100%;
    pointer-events: none;
    z-index: 2;
    overflow: visible;
}

.ripple-circle {
    fill: none;
    stroke: var(--primary-color);
    stroke-width: 4;
    transform-origin: center;
    opacity: 0;
    animation: ripple-svg-anim var(--debug-duration) infinite linear;
}

.ripple-circle.delay-1 {
    animation-delay: calc(var(--debug-duration) / 2);
}

@keyframes ripple-svg-anim {
    0% { 
        transform: scale(1); 
        stroke-width: 4;
        opacity: 0.8;
    }
    100% { 
        transform: scale(var(--debug-ripple-scale)); 
        stroke-width: 0.1;
        opacity: 0;
    }
}

.status-halo {
    position: absolute;
    width: 90px;
    height: 90px;
    border-radius: 50%;
    background: radial-gradient(circle, rgba(255, 140, 0, var(--debug-glow-opacity)) 0%, rgba(255, 140, 0, 0) 70%);
    animation: breathe-halo var(--debug-duration) infinite ease-in-out;
    z-index: 1;
}

.status-square-inner {
    width: 52px;
    height: 52px;
    border: none;
    border-radius: 8px;
    display: flex;
    justify-content: center;
    align-items: center;
    overflow: visible;
    background: transparent;
    z-index: 3;
    animation: breathe-linear var(--debug-duration) infinite linear;
}

.status-favicon {
    width: 42px;
    height: 42px;
    object-fit: contain;
    filter: drop-shadow(0 0 10px rgba(255, 140, 0, var(--debug-glow-opacity)));
}

@keyframes breathe-halo {
    0%, 100% { transform: scale(1.2); opacity: 0.3; }
    50% { transform: scale(1.8); opacity: 0.5; }
}

@keyframes breathe-linear {
    0%, 100% { transform: scale(var(--debug-min-scale)); opacity: 0.7; }
    40%, 60% { transform: scale(var(--debug-max-scale)); opacity: 1; }
}

.run-title-row { 
    display: flex; 
    justify-content: center; 
    align-items: center; 
    width: 100%;
    margin-bottom: 24px; 
}

.run-title { 
    font-weight: 900; 
    color: var(--primary-color); 
    font-size: 15px; 
    letter-spacing: 3px;
    text-transform: uppercase;
    position: relative;
    padding-bottom: 4px;
}
.run-title::after {
    content: ''; position: absolute; bottom: 0; left: 50%; width: 40px; height: 2px;
    background: var(--primary-color); transform: translateX(-50%);
}

.run-info-section {
    width: 100%;
    border-top: 2px solid var(--divider-color);
    padding-top: 24px;
}

.info-grid { 
    display: grid; 
    grid-template-columns: 1fr; 
    gap: 20px; 
    width: 100%;
}
.info-item label { font-size: 11px; color: var(--text-muted); display: block; margin-bottom: 6px; font-weight: bold;}
.info-item span { font-size: 15px; font-weight: 900; color: var(--text-main); }
</style>
