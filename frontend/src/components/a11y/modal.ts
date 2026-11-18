import { useEffect } from 'react'
import type { RefObject } from 'react'

// DEV-025 / 65.03 paragraph 264-268 (UX-016, paragraph 576): shared modal helpers for custom
// (non-<dialog>) modals. Native <dialog>.showModal() already makes the page inert.

/** Keyboard tab stops inside a container: visible, enabled, not inert/hidden, tabindex >= 0. */
export function focusableTargets(container: HTMLElement | null): HTMLElement[] {
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
  if (event.shiftKey && (active === first || active === extraStart || outside)) { event.preventDefault(); last.focus(); return true }
  if (!event.shiftKey && (active === last || outside)) { event.preventDefault(); first.focus(); return true }
  return false
}
