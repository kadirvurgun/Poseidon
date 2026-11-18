import { useEffect } from 'react'
import type { RefObject } from 'react'

// DEV-025 / 65.03 paragraph 264-268 (UX-016, paragraph 576): shared modal helpers for custom
// (non-<dialog>) modals. Native <dialog>.showModal() already makes the page inert.

/**
 * Keyboard tab stops inside a container: visible, enabled, not inert/hidden, tabindex >= 0.
 * A named radio group is one tab stop, as in the browser: its checked radio, else its first radio
 * (arrow keys move inside the group).
 */
export function focusableTargets(container: HTMLElement | null): HTMLElement[] {
  const candidates = tabbable(container)
  const radioStop = new Map<string, HTMLInputElement>()
  for (const element of candidates) {
    if (!(element instanceof HTMLInputElement) || element.type !== 'radio' || !element.name) continue
    const key = radioGroupKey(element), current = radioStop.get(key)
    if (!current || (element.checked && !current.checked)) radioStop.set(key, element)
  }
  return candidates.filter(element => !(element instanceof HTMLInputElement) || element.type !== 'radio' || !element.name || radioStop.get(radioGroupKey(element)) === element)
}

/** True when ctive is stop or another radio of the same group (pointer focus on an unchecked radio). */
function sameStop(active: Element | null, stop: HTMLElement): boolean {
  if (active === stop) return true
  return active instanceof HTMLInputElement && stop instanceof HTMLInputElement && active.type === 'radio' && stop.type === 'radio'
    && !!active.name && radioGroupKey(active) === radioGroupKey(stop)
}

function radioGroupKey(radio: HTMLInputElement): string {
  const form = radio.form
  return `${form ? Array.from(document.forms).indexOf(form) : -1}\u0000${radio.name}`
}

function tabbable(container: HTMLElement | null): HTMLElement[] {
  if (!container) return []
  return Array.from(container.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], select:not(:disabled), input:not(:disabled), textarea:not(:disabled), summary, [tabindex]')).filter(element => {
    // A sample/preview control with tabindex=-1 is never part of the trap cycle.
    if (element.tabIndex < 0) return false
    for (let parent: HTMLElement | null = element; parent; parent = parent.parentElement) {
      if (parent.hidden || parent.inert || parent.hasAttribute('inert')) return false
      const style = getComputedStyle(parent)
      if (style.display === 'none' || style.visibility === 'hidden') return false
      if (parent instanceof HTMLDetailsElement && !parent.open) {
        const summary = Array.from(parent.children).find(child => child.tagName === 'SUMMARY')
        if (!summary?.contains(element)) return false
      }
      if (parent === container) break
    }
    return true
  })
}

/**
 * Makes every element outside `modal` inert while `active` (siblings of each ancestor up to
 * <body>), so pointer, Tab and assistive technology cannot reach the background. Elements that
 * were already inert keep their state on restore.
 *
 * Limitation (snapshot): the sibling set is captured once at activation. A node inserted outside
 * the modal afterwards (a later portal, toast or live region appended to <body>) is NOT made inert,
 * and the restore only touches the snapshot. Shell UI must not open background UI while a custom
 * modal is open; prefer native <dialog>.showModal() where the top layer handles this.
 */
export function inertOutside(modal: HTMLElement): () => void {
  const changed: HTMLElement[] = []
  for (let node: HTMLElement | null = modal; node && node !== document.body; node = node.parentElement) {
    const parent: HTMLElement | null = node.parentElement
    if (!parent) break
    for (const sibling of Array.from(parent.children)) {
      if (sibling === node || !(sibling instanceof HTMLElement) || sibling.hasAttribute('inert')) continue
      if (sibling.tagName === 'SCRIPT' || sibling.tagName === 'STYLE') continue
      sibling.setAttribute('inert', '')
      changed.push(sibling)
    }
  }
  return () => { for (const element of changed) element.removeAttribute('inert') }
}

export function useInertBackground(modal: RefObject<HTMLElement | null>, active = true) {
  useEffect(() => {
    if (!active || !modal.current) return
    return inertOutside(modal.current)
  }, [modal, active])
}

/** Keeps Tab / Shift+Tab inside the container. Returns true when the event was handled. */
export function trapTab(event: { key: string; shiftKey: boolean; preventDefault: () => void }, container: HTMLElement | null, extraStart?: HTMLElement | null): boolean {
  if (event.key !== 'Tab' || !container) return false
  const items = focusableTargets(container)
  if (items.length === 0) { event.preventDefault(); return true }
  const first = items[0], last = items[items.length - 1], active = document.activeElement
  const outside = !active || !container.contains(active)
  if (event.shiftKey && (sameStop(active, first) || active === extraStart || outside)) { event.preventDefault(); last.focus(); return true }
  if (!event.shiftKey && (sameStop(active, last) || outside)) { event.preventDefault(); first.focus(); return true }
  return false
}
