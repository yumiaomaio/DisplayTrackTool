<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { i18n, setLanguage } from '../i18n'

const props = defineProps(['isRunning', 'isLightMode'])
const emit = defineEmits(['toggleTheme', 'showAbout'])

const showMenu = ref(false)

const toggleMenu = (e) => {
  e.stopPropagation()
  showMenu.value = !showMenu.value
}

const closeMenu = () => {
  showMenu.value = false
}

const switchLang = (lang) => {
  setLanguage(lang)
  closeMenu()
}

const handleAboutClick = () => {
  emit('showAbout')
  closeMenu()
}

onMounted(() => {
  window.addEventListener('click', closeMenu)
})

onUnmounted(() => {
  window.removeEventListener('click', closeMenu)
})
</script>

<template>
  <header class="app-header">
    <div class="brand">
      <h1>{{ i18n.t.appTitle }}</h1>
    </div>
    <div class="header-controls">
      <!-- Professional SVG Menu Trigger -->
      <button class="control-btn" @click="toggleMenu" title="Menu">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M4 6H20M4 12H20M4 18H20" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"/>
        </svg>
      </button>
      
      <!-- Floating Menu (Clean, SVG icons) -->
      <Transition name="fade">
        <div v-if="showMenu" class="floating-menu" @click.stop>
          
          <div class="menu-item" @click="emit('toggleTheme')">
            <span>{{ isLightMode ? i18n.t.menu.dark : i18n.t.menu.light }}</span>
            <span class="menu-icon">
              <svg v-if="isLightMode" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path></svg>
              <svg v-else width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"></circle><line x1="12" y1="1" x2="12" y2="3"></line><line x1="12" y1="21" x2="12" y2="23"></line><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line><line x1="1" y1="12" x2="3" y2="12"></line><line x1="21" y1="12" x2="23" y2="12"></line><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line></svg>
            </span>
          </div>

          <div class="menu-item" @click="switchLang(i18n.lang === 'en' ? 'zh' : 'en')">
            <span>{{ i18n.lang === 'en' ? '中文' : 'English' }}</span>
            <span class="menu-icon">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="2" y1="12" x2="22" y2="12"></line><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path></svg>
            </span>
          </div>

          <div class="menu-item" @click="handleAboutClick">
            <span>{{ i18n.t.menu.about }}</span>
            <span class="menu-icon">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
            </span>
          </div>

        </div>
      </Transition>
    </div>
  </header>
</template>

<style scoped>
.app-header {
    height: 52px; padding: 0 20px; display: flex; justify-content: space-between; align-items: center;
    color: #fff; background: var(--header-gradient); 
    flex-shrink: 0; position: relative;
}
.app-header::after { 
    content: '✨'; 
    position: absolute; 
    right: -15px; 
    top: -10px; 
    font-size: 48px; 
    opacity: 0.6; 
    pointer-events: none;
    transform: rotate(15deg);
}

.brand { display: flex; align-items: center; gap: 8px; }
.brand h1 { font-size: 17px; font-weight: 900; color: #fff; margin: 0; }
.brand h1::before { content: '✦'; margin-right: 6px; }

.header-controls { display: flex; gap: 6px; z-index: 100; position: relative; }
.control-btn {
    background: rgba(0,0,0,0.15); border: none; color: #fff; width: 34px; height: 34px; border-radius: 8px;
    cursor: pointer; transition: 0.2s; display: flex; align-items: center; justify-content: center;
}
.control-btn:hover { background: rgba(0,0,0,0.3); transform: scale(1.05); }

/* Floating Menu */
.floating-menu {
    position: absolute; top: 44px; right: 0; width: 170px;
    background: var(--bg-modal); border: 2px solid var(--input-stroke);
    border-radius: var(--shape-radius-sm); box-shadow: 0 10px 30px rgba(0,0,0,0.4);
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
.fade-enter-from, .fade-leave-to { opacity: 0; transform: translateY(-10px); }
</style>
