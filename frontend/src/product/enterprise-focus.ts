// Closed native disclosures and inert/hidden content are outside the tab order.
// Share the same visible targets between standalone and directory-owned drawers.
export function enterpriseFocusTargets(container: HTMLElement | null): HTMLElement[] {
  if (!container) return []
  return Array.from(container.querySelectorAll<HTMLElement>('button:not(:disabled), a[href], select:not(:disabled), input:not(:disabled), textarea:not(:disabled), summary, [tabindex]')).filter(element => {
    if (element.tabIndex < 0) return false
    for (let parent: HTMLElement | null = element; parent; parent = parent.parentElement) {
      if (parent.hidden || parent.inert) return false
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
