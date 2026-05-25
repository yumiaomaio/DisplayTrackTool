<script setup>
import { i18n } from '../i18n'

const props = defineProps(['isRunning', 'isLightMode'])
const emit = defineEmits(['toggleTheme', 'showAbout', 'registerAssociation', 'cleanAssociation'])
</script>

<template>
  <header class="app-header">
    <div class="brand">
      <h1>{{ i18n.t.appTitle }}</h1>
    </div>
  </header>
</template>

<style scoped>
.app-header {
    height: 42px; padding: 0 20px; display: flex; justify-content: space-between; align-items: center;
    color: #fff; background: var(--header-gradient);
    flex-shrink: 0; position: relative;
    overflow: visible;
}

.app-header::before {
    content: '';
    position: absolute;
    right: 15px; top: 8px; bottom: 8px;
    aspect-ratio: 1;
    background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none'%3E%3Cline x1='4' y1='4' x2='0' y2='0' stroke='rgb(255,199,123)' stroke-width='2.5' stroke-linecap='round'/%3E%3Cline x1='20' y1='4' x2='24' y2='0' stroke='rgb(255,199,123)' stroke-width='2.5' stroke-linecap='round'/%3E%3Cline x1='4' y1='20' x2='0' y2='24' stroke='rgb(255,199,123)' stroke-width='2.5' stroke-linecap='round'/%3E%3Cline x1='20' y1='20' x2='24' y2='24' stroke='rgb(255,199,123)' stroke-width='2.5' stroke-linecap='round'/%3E%3Cpath d='M12 5 L14 10 L20 12 L14 14 L12 19 L10 14 L4 12 L10 10 Z' fill='rgb(255,199,123)'/%3E%3C/svg%3E");
    background-size: contain;
    background-repeat: no-repeat;
    pointer-events: none;
}

.brand { display: flex; align-items: center; gap: 8px; }
.brand h1 { font-size: 17px; font-weight: 900; color: #fff; margin: 0; }
.brand h1::before { content: '✦'; margin-right: 6px; }
</style>
