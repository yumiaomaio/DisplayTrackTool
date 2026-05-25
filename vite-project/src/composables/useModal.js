import { ref } from 'vue'
import { i18n } from '../i18n'
import { bridge } from '../services/bridge'

export function useModal() {
  const modal = ref({
    show: false, title: '', message: '', buttonText: '', secondaryButtonText: '',
    showCheckbox: false, checkboxLabel: '', allowClose: true, type: 'info',
    onAction: () => {}, onSecondaryAction: () => {}
  })

  function hide() { modal.value.show = false }

  function showProcessNotFound() {
    modal.value = { show: true, title: i18n.t.timeoutTitle, message: i18n.t.processNotFound, buttonText: i18n.t.menu.close, allowClose: true, type: 'warning', onAction: hide }
  }

  function showWaiting(count) {
    modal.value = { show: true, title: i18n.t.waitingTitle, message: `${i18n.t.waitingMessage} (${count}${i18n.t.seconds})`, buttonText: i18n.t.protocolCancel, allowClose: false, type: 'info', onAction: () => { bridge.StopMonitoring(); hide() } }
  }

  function showExitTip(onConfirmFn) {
    modal.value = { show: true, title: i18n.t.exitTip.title, message: i18n.t.exitTip.message, buttonText: i18n.t.exitTip.gotIt, showCheckbox: true, checkboxLabel: i18n.t.exitTip.dontShowAgain, allowClose: true, type: 'info', onAction: onConfirmFn }
  }

  function showRegisterAssociation(onYes, onNo) {
    modal.value = { show: true, title: i18n.t.registerModal.title, message: i18n.t.registerModal.message, buttonText: i18n.t.registerModal.yes, secondaryButtonText: i18n.t.registerModal.no, allowClose: true, type: 'info', onAction: onYes, onSecondaryAction: onNo }
  }

  function showProtocol(onConfirm, onCancel) {
    modal.value = { show: true, title: i18n.t.protocolModalTitle, message: i18n.t.protocolModalMessage, buttonText: i18n.t.protocolConfirm, secondaryButtonText: i18n.t.protocolCancel, allowClose: true, type: 'info', onAction: onConfirm, onSecondaryAction: onCancel }
  }

  function showUac(onRestart, onContinue) {
    modal.value = { show: true, title: i18n.t.uac.title, message: i18n.t.uac.message, buttonText: i18n.t.uac.button, secondaryButtonText: i18n.t.uac.secondaryButton, allowClose: false, type: 'warning', onAction: onRestart, onSecondaryAction: onContinue }
  }

  function showError(message) {
    modal.value = { show: true, title: i18n.t.errorTitle, message, buttonText: 'OK', allowClose: true, type: 'warning', onAction: hide }
  }

  return { modal, hide, showProcessNotFound, showWaiting, showExitTip, showRegisterAssociation, showProtocol, showUac, showError }
}
