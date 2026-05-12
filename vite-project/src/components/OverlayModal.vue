<script setup>
import { i18n } from '../i18n'

const props = defineProps({
  show: Boolean,
  title: String,
  message: String,
  buttonText: String,
  secondaryButtonText: String,
  showCheckbox: Boolean,
  checkboxLabel: String,
  checkboxValue: Boolean,
  allowClose: {
    type: Boolean,
    default: true
  },
  type: {
    type: String,
    default: 'info' // 'info' or 'warning'
  }
})

const emit = defineEmits(['close', 'action', 'secondaryAction', 'update:checkboxValue'])

const handleBackdropClick = () => {
  if (props.allowClose) emit('close')
}

const onCheckboxChange = (e) => {
  emit('update:checkboxValue', e.target.checked)
}
</script>

<template>
  <Transition name="modal-fade">
    <div v-if="show" class="modal-overlay" @click.self="handleBackdropClick">
      <div :class="['modal-content', type]">
        
        <!-- Header with tech accent -->
        <div class="modal-header">
          <div class="header-accent"></div>
          <h2>{{ title }}</h2>
          <div class="header-accent right"></div>
        </div>

        <!-- Body -->
        <div class="modal-body">
          <div class="message-container">
            <p v-for="(line, idx) in message.split('\n')" :key="idx">{{ line }}</p>
          </div>

          <!-- Optional Checkbox -->
          <div v-if="showCheckbox" class="modal-checkbox-row">
            <label class="checkbox-container">
              <input type="checkbox" :checked="checkboxValue" @change="onCheckboxChange">
              <span class="checkmark"></span>
              <span class="label-text">{{ checkboxLabel }}</span>
            </label>
          </div>
          
          <!-- Subtle background decorative elements -->
          <div class="deco-hex">⌬</div>
        </div>

        <!-- Footer -->
        <div class="modal-footer">
          <div class="button-group">
            <button class="modal-btn" @click="emit('action')">
              {{ buttonText || i18n.t.menu.close }}
            </button>
            <button v-if="secondaryButtonText" class="modal-btn secondary" @click="emit('secondaryAction')">
              {{ secondaryButtonText }}
            </button>
          </div>
        </div>

      </div>
    </div>
  </Transition>
</template>

<style scoped>
.modal-overlay {
    position: fixed;
    top: 0; left: 0; right: 0; bottom: 0;
    background: rgba(0, 0, 0, 0.85);
    backdrop-filter: blur(10px);
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 2000;
    padding: 20px;
}

.modal-content {
    width: 100%;
    max-width: 460px;
    background: var(--bg-modal);
    border: 2px solid var(--input-stroke);
    border-radius: var(--shape-radius);
    overflow: hidden;
    position: relative;
    box-shadow: 0 0 50px rgba(0,0,0,0.8);
}

.modal-content.warning {
    border-color: var(--danger-color);
}
.modal-content.warning .header-accent {
    background: var(--danger-color);
}
.modal-content.warning .modal-btn {
    background: var(--danger-color);
}
.modal-content.warning .modal-btn:hover {
    background: var(--success-color);
    transform: translateY(-2px);
}
.modal-content.warning .modal-btn.secondary {
    background: var(--btn-bg);
    color: var(--text-main);
    border: 2px solid var(--input-stroke);
}
.modal-content.warning .modal-btn.secondary:hover {
    background: var(--theme-gold);
    border-color: var(--theme-gold);
    color: white;
}

/* Header */
.modal-header {
    padding: 24px 20px 16px;
    display: flex;
    align-items: center;
    gap: 15px;
    background: linear-gradient(to bottom, rgba(255,140,0,0.05), transparent);
}

.header-accent {
    flex: 1;
    height: 4px;
    background: var(--primary-color);
    border-radius: 2px;
}
.header-accent.right {
    flex: 0 0 30px;
}

.modal-header h2 {
    font-size: 18px;
    font-weight: 900;
    color: var(--text-main);
    letter-spacing: 2px;
    margin: 0;
    text-transform: uppercase;
}

/* Body */
.modal-body {
    padding: 30px 40px;
    text-align: center;
    position: relative;
}

.message-container {
    position: relative;
    z-index: 2;
}

.modal-body p {
    font-size: 14px;
    line-height: 1.8;
    color: var(--text-muted);
    margin-bottom: 8px;
    font-weight: 600;
}

.deco-hex {
    position: absolute;
    top: 50%; left: 50%;
    transform: translate(-50%, -50%);
    font-size: 120px;
    color: var(--primary-color);
    opacity: 0.03;
    pointer-events: none;
}

/* Footer */
.modal-footer {
    padding: 0 40px 40px;
}

.button-group {
    display: flex;
    flex-direction: column;
    gap: 12px;
}

.modal-btn {
    width: 100%;
    height: 54px;
    background: var(--primary-gradient);
    color: white;
    border: none;
    border-radius: 999px;
    font-size: 14px;
    font-weight: 900;
    cursor: pointer;
    text-transform: uppercase;
    letter-spacing: 1px;
    transition: all 0.2s;
    box-shadow: 0 10px 20px rgba(0,0,0,0.3);
}
.modal-btn:hover {
    filter: brightness(1.1);
    transform: translateY(-2px);
}
.modal-btn:active {
    transform: translateY(0);
}

.modal-btn.secondary {
    background: var(--btn-bg);
    color: var(--text-main);
    border: 2px solid var(--input-stroke);
    box-shadow: none;
}
.modal-btn.secondary:hover {
    background: var(--input-stroke);
}

/* Checkbox Styles */
.modal-checkbox-row {
    margin-top: 20px;
    display: flex;
    justify-content: center;
    z-index: 5;
    position: relative;
}

.checkbox-container {
    display: flex;
    align-items: center;
    cursor: pointer;
    font-size: 13px;
    color: var(--text-muted);
    font-weight: bold;
    user-select: none;
    gap: 10px;
}

.checkbox-container input {
    position: absolute;
    opacity: 0;
    cursor: pointer;
    height: 0; width: 0;
}

.checkmark {
    height: 20px;
    width: 20px;
    background-color: var(--input-bg);
    border: 2px solid var(--input-stroke);
    border-radius: 4px;
    position: relative;
    transition: all 0.2s;
}

.checkbox-container:hover input ~ .checkmark {
    border-color: var(--primary-color);
}

.checkbox-container input:checked ~ .checkmark {
    background-color: var(--primary-color);
    border-color: var(--primary-color);
}

.checkmark:after {
    content: "";
    position: absolute;
    display: none;
}

.checkbox-container input:checked ~ .checkmark:after {
    display: block;
}

.checkbox-container .checkmark:after {
    left: 6px;
    top: 2px;
    width: 5px;
    height: 10px;
    border: solid white;
    border-width: 0 2px 2px 0;
    transform: rotate(45deg);
}

/* Transitions */
.modal-fade-enter-active, .modal-fade-leave-active {
    transition: all 0.4s cubic-bezier(0.165, 0.84, 0.44, 1);
}
.modal-fade-enter-from, .modal-fade-leave-to {
    opacity: 0;
}
.modal-fade-enter-active .modal-content {
    animation: scale-in 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

@keyframes scale-in {
    from { transform: scale(0.8) translateY(20px); opacity: 0; }
    to { transform: scale(1) translateY(0); opacity: 1; }
}
</style>
