import { useId, useMemo, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import './equipment-hierarchy-tree.css'

// Presentation projection of the complete authorized vessel snapshot. The containing
// workspace maps its generated API DTOs here; this component grants no permissions.
export type EquipmentHierarchyItem = Readonly<{ id: string; parentId: string | null; label: string | null }>
type Props = {
  scopeKey: string
  language: 'tr' | 'en'
  nodes: readonly EquipmentHierarchyItem[] | null
  selectedId: string | null
  onSelect: (id: string) => void
  loading?: boolean
  error?: 'unavailable' | 'denied' | null
  onRetry?: () => void
}
type Index = { items: Map<string, EquipmentHierarchyItem>; children: Map<string | null, string[]> }
type Visible = { id: string; level: number; position: number; size: number }

function indexForest(nodes: readonly EquipmentHierarchyItem[]): Index | null {
  const items = new Map<string, EquipmentHierarchyItem>()
  const children = new Map<string | null, string[]>()
  for (const node of nodes) {
    if (!node || typeof node.id !== 'string' || !node.id || items.has(node.id) ||
      !(node.parentId === null || typeof node.parentId === 'string' && node.parentId.length > 0) ||
      !(node.label === null || typeof node.label === 'string')) return null
    items.set(node.id, node)
    const siblings = children.get(node.parentId) ?? []
    siblings.push(node.id); children.set(node.parentId, siblings)
  }
  for (const item of items.values()) if (item.parentId !== null && !items.has(item.parentId)) return null
  const colors = new Map<string, number>()
  for (const start of items.keys()) {
    if (colors.get(start) === 2) continue
    const chain: string[] = []; let current: string | null = start
    while (current !== null) {
      if (colors.get(current) === 2) break
      if (colors.get(current) === 1) return null
      colors.set(current, 1); chain.push(current); current = items.get(current)!.parentId
    }
    for (const id of chain) colors.set(id, 2)
  }
  return { items, children }
}

export function EquipmentHierarchyTree(props: Props) {
  const en = props.language === 'en'
  if (props.loading) return <p role="status">{en ? 'Loading equipment…' : 'Ekipman yükleniyor…'}</p>
  if (props.error === 'denied') return <p role="alert">{en ? 'Equipment access is not permitted.' : 'Ekipman erişim yetkiniz yok.'}</p>
  if (props.error || props.nodes === null) return <div role="alert">
    <p>{en ? 'Equipment could not be loaded.' : 'Ekipman yüklenemedi.'}</p>
    {props.onRetry && <button type="button" onClick={props.onRetry}>{en ? 'Retry' : 'Tekrar dene'}</button>}
  </div>
  return <ScopedTree key={props.scopeKey} {...props} nodes={props.nodes} />
}

function ScopedTree({ language, nodes, selectedId, onSelect, onRetry }: Props & { nodes: readonly EquipmentHierarchyItem[] }) {
  const en = language === 'en'
  const prefix = useId()
  const index = useMemo(() => indexForest(nodes), [nodes])
  const refs = useRef(new Map<string, HTMLDivElement>())
  const buffer = useRef({ text: '', at: 0 })
  const [focused, setFocused] = useState<{ id: string; selection: string | null } | null>(null)
  const [branches, setBranches] = useState(new Map<string, { open: boolean; selection: string | null }>())

  const ancestors = new Set<string>()
  let ancestor = index?.items.get(selectedId ?? '')?.parentId ?? null
  while (ancestor !== null) { ancestors.add(ancestor); ancestor = index!.items.get(ancestor)!.parentId }
  const isOpen = (id: string) => {
    const state = branches.get(id)
    return state?.open === true || ancestors.has(id) && (!state || state.selection !== selectedId)
  }
  const visible: Visible[] = []
  const roots = index?.children.get(null) ?? []
  const pending = roots.map((id, i) => ({ id, level: 1, position: i + 1, size: roots.length })).reverse()
  while (pending.length) {
    const row = pending.pop()!; visible.push(row)
    const children = index!.children.get(row.id) ?? []
    if (isOpen(row.id)) for (let i = children.length - 1; i >= 0; i--)
      pending.push({ id: children[i], level: row.level + 1, position: i + 1, size: children.length })
  }
  const visibleIds = new Set(visible.map(row => row.id))
  const focusId = focused && focused.selection === selectedId && visibleIds.has(focused.id) ? focused.id
    : selectedId && visibleIds.has(selectedId) ? selectedId : visible[0]?.id
  const ids = new Map(visible.map((row, i) => [row.id, `${prefix}-item-${i}`]))
  const label = (id: string) => index!.items.get(id)!.label?.trim() || (en ? 'Untitled equipment' : 'Adı girilmemiş ekipman')
  const focus = (id: string) => { setFocused({ id, selection: selectedId }); refs.current.get(id)?.focus() }
  const expand = (id: string, open: boolean) => setBranches(previous => new Map(previous).set(id, { open, selection: selectedId }))
  const key = (event: KeyboardEvent<HTMLDivElement>, row: Visible, position: number) => {
    if (event.target !== event.currentTarget) return
    if (event.altKey || event.ctrlKey || event.metaKey) return
    const children = index!.children.get(row.id) ?? []
    switch (event.key) {
      case 'ArrowDown': event.preventDefault(); focus(visible[Math.min(position + 1, visible.length - 1)].id); return
      case 'ArrowUp': event.preventDefault(); focus(visible[Math.max(position - 1, 0)].id); return
      case 'Home': event.preventDefault(); focus(visible[0].id); return
      case 'End': event.preventDefault(); focus(visible[visible.length - 1].id); return
      case 'ArrowRight':
        event.preventDefault()
        if (children.length) { if (isOpen(row.id)) focus(children[0]); else expand(row.id, true) }
        return
      case 'ArrowLeft': {
        event.preventDefault()
        if (children.length && isOpen(row.id)) expand(row.id, false)
        else { const parent = index!.items.get(row.id)!.parentId; if (parent !== null) focus(parent) }
        return
      }
      case 'Enter': case ' ': event.preventDefault(); onSelect(row.id); return
    }
    if (event.key.length !== 1) return
    event.preventDefault()
    const normalize = (value: string) => value.normalize('NFKC').toLocaleLowerCase(en ? 'en-US' : 'tr-TR')
    const now = event.timeStamp, character = normalize(event.key)
    const text = now - buffer.current.at < 750 ? buffer.current.text + character : character
    buffer.current = { text, at: now }
    const query = [...text].every(c => c === character) ? character : text
    for (let offset = 1; offset <= visible.length; offset++) {
      const candidate = visible[(position + offset) % visible.length]
      if (normalize(label(candidate.id)).startsWith(query)) { focus(candidate.id); break }
    }
  }
  if (!index) return <div role="alert"><p>{en ? 'The hierarchy could not be verified. Refresh before editing.' : 'Hiyerarşi doğrulanamadı. Düzenlemeden önce yenileyin.'}</p>
    {onRetry && <button type="button" onClick={onRetry}>{en ? 'Retry' : 'Tekrar dene'}</button>}
  </div>
  if (!nodes.length) return <p>{en ? 'No equipment records.' : 'Ekipman kaydı bulunamadı.'}</p>
  return <div className="equipment-hierarchy-tree" role="tree" aria-label={en ? 'Equipment hierarchy' : 'Ekipman hiyerarşisi'} aria-multiselectable={false}>
    {visible.map((row, position) => {
      const children = index.children.get(row.id) ?? []
      const open = children.length > 0 && isOpen(row.id)
      const groupId = `${ids.get(row.id)}-children`
      return <div role="presentation" key={row.id}>
        <div role="treeitem" id={ids.get(row.id)} aria-label={label(row.id)}
          aria-level={row.level} aria-setsize={row.size} aria-posinset={row.position}
          aria-selected={selectedId === row.id} aria-expanded={children.length ? open : undefined}
          aria-owns={open ? groupId : undefined} tabIndex={row.id === focusId ? 0 : -1}
          className="equipment-hierarchy-row" style={{ paddingInlineStart: `min(${Math.min(row.level - 1, 12) * 1.25}rem, 20vw)` }}
          ref={element => { if (element) refs.current.set(row.id, element); else refs.current.delete(row.id) }}
          onFocus={event => { if (event.target === event.currentTarget) setFocused({ id: row.id, selection: selectedId }) }}
          onClick={() => { focus(row.id); onSelect(row.id) }} onKeyDown={event => key(event, row, position)}>
          {children.length ? <button type="button" tabIndex={-1} className="equipment-hierarchy-toggle"
            aria-label={`${open ? (en ? 'Collapse' : 'Daralt') : (en ? 'Expand' : 'Genişlet')}: ${label(row.id)}`}
            onMouseDown={event => event.preventDefault()}
            onClick={event => { event.stopPropagation(); focus(row.id); expand(row.id, !open) }}>
            <span aria-hidden="true">{open ? '▾' : '▸'}</span>
          </button> : <span className="equipment-hierarchy-marker" aria-hidden="true">·</span>}
          <span>{label(row.id)}</span>
        </div>
        {open && <div role="group" id={groupId} aria-owns={children.map(id => ids.get(id)!).join(' ')} />}
      </div>
    })}
  </div>
}
