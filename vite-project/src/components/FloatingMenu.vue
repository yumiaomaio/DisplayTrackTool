<script setup>
import { i18n, setLanguage } from '../i18n'

defineProps({ isLightMode: Boolean, show: Boolean })
const emit = defineEmits([
  'toggleTheme', 'switchLanguage', 'registerAssociation', 'cleanAssociation', 'about', 'close'
])

const onSwitchLang = () => {
  setLanguage(i18n.lang === 'en' ? 'zh' : 'en')
  emit('switchLanguage')
  emit('close')
}
</script>

<template>
  <Transition name="fade">
    <div v-if="show" class="floating-menu" @click.stop>
      <div class="menu-item" @click="emit('toggleTheme'); emit('close')">
        <span>{{ isLightMode ? i18n.t.menu.dark : i18n.t.menu.light }}</span>
        <span class="menu-icon">
          <svg v-if="isLightMode" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path></svg>
          <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"></circle><line x1="12" y1="1" x2="12" y2="3"></line><line x1="12" y1="21" x2="12" y2="23"></line><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line><line x1="1" y1="12" x2="3" y2="12"></line><line x1="21" y1="12" x2="23" y2="12"></line><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line></svg>
        </span>
      </div>
      <div class="menu-item" @click="onSwitchLang">
        <span>{{ i18n.lang === 'en' ? '中文' : 'English' }}</span>
        <span class="menu-icon">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="2" y1="12" x2="22" y2="12"></line><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path></svg>
        </span>
      </div>
      <div class="menu-item" @click="emit('registerAssociation'); emit('close')">
        <span>{{ i18n.t.menu.registerAssociation }}</span>
        <span class="menu-icon">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"></path><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"></path></svg>
        </span>
      </div>
      <div class="menu-item" @click="emit('cleanAssociation'); emit('close')">
        <span>{{ i18n.t.menu.cleanAssociation }}</span>
        <span class="menu-icon">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18"></path><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"></path><path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"></path></svg>
        </span>
      </div>
      <div class="menu-item" @click="emit('about'); emit('close')">
        <span>{{ i18n.t.menu.about }}</span>
        <span class="menu-icon">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
        </span>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
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
</style>
