import { ref, onMounted, onUnmounted } from 'vue'

export function useMenu() {
  const showMenu = ref(false)

  const toggleMenu = (e) => {
    if (e) e.stopPropagation()
    showMenu.value = !showMenu.value
  }

  const closeMenu = () => { showMenu.value = false }

  onMounted(() => window.addEventListener('click', closeMenu))
  onUnmounted(() => window.removeEventListener('click', closeMenu))

  return { showMenu, toggleMenu, closeMenu }
}
