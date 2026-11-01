import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { EquipmentHierarchyTree } from './EquipmentHierarchyTree'
import type { EquipmentHierarchyItem } from './EquipmentHierarchyTree'

const forest: EquipmentHierarchyItem[] = [
  { id: 'system', parentId: null, label: 'Ana makine' },
  { id: 'pump', parentId: 'system', label: 'Pompa' },
  { id: 'valve', parentId: 'pump', label: 'Valf' },
  { id: 'winch', parentId: null, label: 'Vinç' },
]
const base = { scopeKey: 'synthetic-actor/vessel-A', language: 'tr' as const, nodes: forest, selectedId: null, onSelect: vi.fn() }

describe('T-S8-001 hierarchy presentation (authored; real API/browser coverage remains separate)', () => {
  it('reveals the initial selected ancestry and declares explicit tree/group ownership', () => {
    render(<EquipmentHierarchyTree {...base} selectedId="valve" />)
    const root = screen.getByRole('treeitem', { name: 'Ana makine' })
    const pump = screen.getByRole('treeitem', { name: 'Pompa' })
    const valve = screen.getByRole('treeitem', { name: 'Valf' })
    expect(root).toHaveAttribute('aria-expanded', 'true')
    expect(valve).not.toHaveAttribute('aria-expanded')
    expect(valve).toHaveAttribute('aria-level', '3')
    expect(valve).toHaveAttribute('aria-selected', 'true')
    expect(valve).toHaveAttribute('tabindex', '0')
    const group = document.getElementById(root.getAttribute('aria-owns')!)!
    expect(group).toHaveAttribute('role', 'group')
    expect(group.getAttribute('aria-owns')!.split(' ')).toContain(pump.id)
    expect(screen.getAllByRole('treeitem').filter(item => item.tabIndex === 0)).toEqual([valve])
  })

  it('uses arrow navigation without selecting until Enter is pressed', async () => {
    const user = userEvent.setup(), select = vi.fn()
    render(<EquipmentHierarchyTree {...base} onSelect={select} />)
    const root = screen.getByRole('treeitem', { name: 'Ana makine' })
    await user.tab()
    expect(root).toHaveFocus()
    await user.keyboard('{ArrowRight}')
    expect(root).toHaveFocus()
    expect(root).toHaveAttribute('aria-expanded', 'true')
    await user.keyboard('{ArrowRight}')
    const pump = screen.getByRole('treeitem', { name: 'Pompa' })
    expect(pump).toHaveFocus()
    expect(select).not.toHaveBeenCalled()
    await user.keyboard('{Enter}')
    expect(select).toHaveBeenCalledTimes(1)
    expect(select).toHaveBeenCalledWith('pump')
  })

  it('moves left to the parent and collapses without leaving focus in removed descendants', async () => {
    const user = userEvent.setup()
    render(<EquipmentHierarchyTree {...base} selectedId="valve" />)
    await user.tab()
    await user.keyboard('{ArrowLeft}')
    const pump = screen.getByRole('treeitem', { name: 'Pompa' })
    expect(pump).toHaveFocus()
    await user.keyboard('{ArrowLeft}')
    expect(pump).toHaveFocus()
    expect(pump).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('treeitem', { name: 'Valf' })).not.toBeInTheDocument()
    expect(pump).toHaveAttribute('tabindex', '0')
  })

  it('Home/End and up/down visit only currently visible nodes with a single tab stop', async () => {
    const user = userEvent.setup(), select = vi.fn()
    render(<EquipmentHierarchyTree {...base} onSelect={select} />)
    await user.tab(); await user.keyboard('{End}')
    expect(screen.getByRole('treeitem', { name: 'Vinç' })).toHaveFocus()
    await user.keyboard('{ArrowUp}')
    expect(screen.getByRole('treeitem', { name: 'Ana makine' })).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(screen.getByRole('treeitem', { name: 'Vinç' })).toHaveFocus()
    await user.keyboard('{Home}')
    expect(screen.getByRole('treeitem', { name: 'Ana makine' })).toHaveFocus()
    expect(screen.getAllByRole('treeitem').filter(item => item.tabIndex === 0)).toHaveLength(1)
    expect(select).not.toHaveBeenCalled()
  })

  it('supports Turkish type-ahead and repeated-character cycling without navigation writes', async () => {
    const user = userEvent.setup(), select = vi.fn()
    const nodes = [forest[0], { id: 'port', parentId: null, label: 'İskele pompa' }, { id: 'forward', parentId: null, label: 'İleri vinç' }]
    render(<EquipmentHierarchyTree {...base} nodes={nodes} onSelect={select} />)
    await user.tab(); await user.keyboard('İ')
    expect(screen.getByRole('treeitem', { name: 'İskele pompa' })).toHaveFocus()
    await user.keyboard('İ')
    expect(screen.getByRole('treeitem', { name: 'İleri vinç' })).toHaveFocus()
    expect(select).not.toHaveBeenCalled()
  })

  it('keeps pointer expansion separate from selecting an equipment record', async () => {
    const user = userEvent.setup(), select = vi.fn()
    render(<EquipmentHierarchyTree {...base} onSelect={select} />)
    await user.click(screen.getByRole('button', { name: 'Genişlet: Ana makine' }))
    expect(screen.getByRole('treeitem', { name: 'Pompa' })).toBeVisible()
    expect(screen.getByRole('treeitem', { name: 'Ana makine' })).toHaveFocus()
    expect(select).not.toHaveBeenCalled()
    await user.click(screen.getByRole('treeitem', { name: 'Pompa' }))
    expect(select).toHaveBeenCalledTimes(1)
    expect(select).toHaveBeenCalledWith('pump')
  })

  it.each([
    { nodes: [{ id: 'a', parentId: 'missing', label: 'Withheld orphan label' }] },
    { nodes: [{ id: 'a', parentId: 'a', label: 'Withheld cycle label' }] },
    { nodes: [{ id: 'a', parentId: null, label: 'Withheld duplicate label' }, { id: 'a', parentId: null, label: 'Other withheld label' }] },
    { nodes: [{ id: 'root', parentId: null, label: 'Root' }, { id: 'a', parentId: 'b', label: 'Withheld A' }, { id: 'b', parentId: 'a', label: 'Withheld B' }] },
  ])('does not render a partial graph or its labels for malformed structure', ({ nodes }) => {
    const select = vi.fn()
    render(<EquipmentHierarchyTree {...base} nodes={nodes} onSelect={select} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Hiyerarşi doğrulanamadı')
    expect(screen.queryByRole('tree')).not.toBeInTheDocument()
    expect(document.body).not.toHaveTextContent('Withheld')
    expect(select).not.toHaveBeenCalled()
  })

  it('distinguishes loading and denial from an empty or healthy equipment list', () => {
    const view = render(<EquipmentHierarchyTree {...base} loading />)
    expect(screen.getByRole('status')).toHaveTextContent('Ekipman yükleniyor')
    expect(screen.queryByRole('tree')).not.toBeInTheDocument()
    view.rerender(<EquipmentHierarchyTree {...base} error="denied" />)
    expect(screen.getByRole('alert')).toHaveTextContent('erişim yetkiniz yok')
    expect(document.body).not.toHaveTextContent('Ana makine')
    expect(document.body).not.toHaveTextContent('kaydı bulunamadı')
  })

  it('offers a real retry callback for unavailable data and labels genuine empty data separately', async () => {
    const user = userEvent.setup(), retry = vi.fn()
    const view = render(<EquipmentHierarchyTree {...base} language="en" nodes={null} onRetry={retry} />)
    expect(screen.getByRole('alert')).toHaveTextContent('unavailable')
    await user.click(screen.getByRole('button', { name: 'Retry' }))
    expect(retry).toHaveBeenCalledTimes(1)
    view.rerender(<EquipmentHierarchyTree {...base} language="en" nodes={[]} />)
    expect(screen.getByText('No equipment records.')).toBeVisible()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('resets expansion and labels across actor/vessel scope changes even when identifiers are reused', async () => {
    const user = userEvent.setup()
    const view = render(<EquipmentHierarchyTree {...base} />)
    await user.click(screen.getByRole('button', { name: 'Genişlet: Ana makine' }))
    view.rerender(<EquipmentHierarchyTree {...base} scopeKey="synthetic-other-actor/vessel-B"
      nodes={[{ id: 'system', parentId: null, label: 'Other system' }, { id: 'pump', parentId: 'system', label: 'Other pump' }]} />)
    expect(screen.getByRole('treeitem', { name: 'Other system' })).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('treeitem', { name: 'Other pump' })).not.toBeInTheDocument()
    expect(document.body).not.toHaveTextContent('Ana makine')
  })

  it('reveals an externally selected descendant after its ancestor was collapsed for another selection', async () => {
    const user = userEvent.setup()
    const view = render(<EquipmentHierarchyTree {...base} selectedId="pump" />)
    await user.click(screen.getByRole('button', { name: 'Daralt: Ana makine' }))
    expect(screen.queryByRole('treeitem', { name: 'Pompa' })).not.toBeInTheDocument()
    view.rerender(<EquipmentHierarchyTree {...base} selectedId="valve" />)
    expect(screen.getByRole('treeitem', { name: 'Valf' })).toHaveAttribute('aria-selected', 'true')
  })

  it('renders names as text and gives incomplete drafts a localized name without inventing readiness', () => {
    render(<EquipmentHierarchyTree {...base} language="en" nodes={[
      { id: 'unnamed', parentId: null, label: null },
      { id: 'literal', parentId: null, label: '<img src="invalid" />' },
    ]} />)
    expect(screen.getByRole('treeitem', { name: 'Untitled equipment' })).toBeVisible()
    expect(screen.getByText('<img src="invalid" />')).toBeVisible()
    expect(document.querySelector('img')).toBeNull()
    expect(document.body).not.toHaveTextContent('Ready')
  })
})
