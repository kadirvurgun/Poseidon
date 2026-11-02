import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ImportMappingEditor } from './ImportMappingEditor'
import type { ImportMappingBinding, ImportMappingTemplate } from './ImportMappingEditor'

const template: ImportMappingTemplate = {
  version: 'synthetic-v1', fingerprint: 'synthetic-schema-fingerprint', allowSourceColumnReuse: false,
  fields: [
    { key: 'name', label: 'Ad', required: true, writable: true },
    { key: 'reference', label: 'Referans', required: false, writable: true },
    { key: 'tenant_id', label: 'Kiracı', required: false, writable: false },
  ],
}
const base = {
  language: 'tr' as const, currentContextKey: 'session-A/vessel-A/upload-A/schema-A',
  loadedContextKey: 'session-A/vessel-A/upload-A/schema-A', state: 'ready' as const,
  template, headers: ['Adı', 'Referansı'], bindings: [] as readonly ImportMappingBinding[],
  onChange: vi.fn(), onPreview: vi.fn(), onSaveDraft: vi.fn(),
}

describe('DEV-069 controlled import mapping editor (authored; execution deferred)', () => {
  it('permits saving incomplete drafts while blocking preview and identifying the missing input accessibly', async () => {
    const user = userEvent.setup(), save = vi.fn()
    render(<ImportMappingEditor {...base} onSaveDraft={save} />)
    const field = screen.getByRole('combobox', { name: 'Ad (zorunlu)' })
    expect(field).toHaveAttribute('aria-required', 'true')
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(document.getElementById(field.getAttribute('aria-describedby')!)).toHaveTextContent('Bir kaynak kolonu seçin.')
    expect(screen.getByRole('button', { name: 'Doğrula ve önizle' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Eşleme taslağını kaydet' }))
    expect(save).toHaveBeenCalledOnce()
    expect(screen.getAllByRole('combobox')).toHaveLength(2)
    expect(screen.getByText('Kiracı — Sistem tarafından yönetilir')).toBeInTheDocument()
  })

  it('updates an explicit mapping without guessing from similar headers and previews without a commit', async () => {
    const user = userEvent.setup(), preview = vi.fn(), changed = vi.fn()
    function Harness() {
      const [bindings, setBindings] = useState<readonly ImportMappingBinding[]>([])
      return <ImportMappingEditor {...base} bindings={bindings} onPreview={preview}
        onChange={value => { changed(value); setBindings(value) }} />
    }
    render(<Harness />)
    const field = screen.getByRole('combobox', { name: 'Ad (zorunlu)' })
    expect(field).toHaveValue('')
    await user.selectOptions(field, '0')
    expect(changed).toHaveBeenLastCalledWith([{ targetField: 'name', sourceColumn: 0 }])
    expect(field).not.toHaveAttribute('aria-invalid')
    await user.click(screen.getByRole('button', { name: 'Doğrula ve önizle' }))
    expect(preview).toHaveBeenCalledOnce()
    expect(screen.queryByRole('button', { name: /^import|^içe aktar$/i })).not.toBeInTheDocument()
  })

  it('preserves physical column ordinals for duplicate and blank headers', () => {
    render(<ImportMappingEditor {...base} headers={['', 'Ad', 'Ad']} />)
    const fields = screen.getAllByRole('combobox')
    expect(fields[0].querySelector('option[value="0"]')).toHaveTextContent('1 — Başlıksız kolon')
    expect(fields[0].querySelector('option[value="1"]')).toHaveTextContent('2 — Ad')
    expect(fields[0].querySelector('option[value="2"]')).toHaveTextContent('3 — Ad')
  })

  it('blocks a reused source column until the server template explicitly permits reuse', () => {
    const bindings = [{ targetField: 'name', sourceColumn: 0 }, { targetField: 'reference', sourceColumn: 0 }]
    const { rerender } = render(<ImportMappingEditor {...base} bindings={bindings} />)
    expect(screen.getAllByText('Bu kaynak kolonu başka bir alana da atanmış.')).toHaveLength(2)
    expect(screen.getByRole('button', { name: 'Doğrula ve önizle' })).toBeDisabled()
    rerender(<ImportMappingEditor {...base} template={{ ...template, allowSourceColumnReuse: true }} bindings={bindings} />)
    expect(screen.getByRole('button', { name: 'Doğrula ve önizle' })).toBeEnabled()
  })

  it('repairs duplicate target choices by replacing all choices for that field', async () => {
    const user = userEvent.setup(), changed = vi.fn()
    const bindings = [{ targetField: 'name', sourceColumn: 0 }, { targetField: 'name', sourceColumn: 1 }]
    render(<ImportMappingEditor {...base} bindings={bindings} onChange={changed} />)
    expect(screen.getByText('Bu alana birden fazla kaynak atanmış.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Doğrula ve önizle' })).toBeDisabled()
    await user.selectOptions(screen.getByRole('combobox', { name: 'Ad (zorunlu)' }), '1')
    expect(changed).toHaveBeenCalledWith([{ targetField: 'name', sourceColumn: 1 }])
    expect(bindings).toHaveLength(2)
  })

  it.each([-1, 2, 0.5, Number.NaN])('rejects an unavailable or nonintegral saved column %s', sourceColumn => {
    render(<ImportMappingEditor {...base} bindings={[{ targetField: 'name', sourceColumn }]} />)
    expect(screen.getByRole('combobox', { name: 'Ad (zorunlu)' })).toHaveValue('')
    expect(screen.getByText('Mevcut bir kaynak kolonu seçin.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Doğrula ve önizle' })).toBeDisabled()
  })

  it.each(['tenant_id', 'private-foreign-column'])('blocks unknown or system mapping without exposing its raw key (%s)', targetField => {
    render(<ImportMappingEditor {...base} bindings={[{ targetField: 'name', sourceColumn: 0 }, { targetField, sourceColumn: 1 }]} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Kayıtlı eşleme bu şablonun dışında alanlar içeriyor.')
    expect(screen.getByRole('button', { name: 'Doğrula ve önizle' })).toBeDisabled()
    expect(screen.queryByText(targetField)).not.toBeInTheDocument()
  })

  it('clears a field explicitly without assigning another column', async () => {
    const user = userEvent.setup(), changed = vi.fn()
    render(<ImportMappingEditor {...base} bindings={[{ targetField: 'name', sourceColumn: 0 }]} onChange={changed} />)
    await user.selectOptions(screen.getByRole('combobox', { name: 'Ad (zorunlu)' }), '')
    expect(changed).toHaveBeenCalledWith([])
  })

  it('withholds old headers, mappings and actions after a session or vessel context change', () => {
    const { rerender } = render(<ImportMappingEditor {...base} headers={['Synthetic private header']} />)
    expect(screen.getAllByRole('option', { name: '1 — Synthetic private header' })).toHaveLength(2)
    rerender(<ImportMappingEditor {...base} headers={['Synthetic private header']} currentContextKey="new-session/new-vessel" />)
    expect(screen.getByRole('status')).toHaveTextContent('Geçerli oturum ve seçim')
    expect(screen.queryAllByRole('combobox')).toHaveLength(0)
    expect(screen.queryAllByRole('option')).toHaveLength(0)
    expect(screen.queryAllByRole('button')).toHaveLength(0)
  })

  it.each(['loading', 'denied', 'unavailable'] as const)('withholds previously loaded private data while state is %s', state => {
    render(<ImportMappingEditor {...base} state={state} headers={['Synthetic private header']} />)
    expect(screen.queryAllByRole('option')).toHaveLength(0)
    expect(screen.queryAllByRole('combobox')).toHaveLength(0)
    expect(screen.queryByText('synthetic-v1')).not.toBeInTheDocument()
  })

  it('offers an explicit retry only for unavailable template data', async () => {
    const user = userEvent.setup(), retry = vi.fn()
    render(<ImportMappingEditor {...base} state="unavailable" onRetry={retry} />)
    await user.click(screen.getByRole('button', { name: 'Tekrar dene' }))
    expect(retry).toHaveBeenCalledOnce()
  })

  it('disables edits and duplicate preview/draft requests while an operation is in flight', async () => {
    const user = userEvent.setup(), preview = vi.fn(), save = vi.fn()
    render(<ImportMappingEditor {...base} busy bindings={[{ targetField: 'name', sourceColumn: 0 }]} onPreview={preview} onSaveDraft={save} />)
    expect(screen.getByRole('region')).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByRole('combobox', { name: 'Ad (zorunlu)' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Doğrula ve önizle' }))
    await user.click(screen.getByRole('button', { name: 'Eşleme taslağını kaydet' }))
    expect(preview).not.toHaveBeenCalled()
    expect(save).not.toHaveBeenCalled()
  })

  it('retains the current controlled selection on a failed preview and renders English operator text', () => {
    render(<ImportMappingEditor {...base} language="en" failure="preview" bindings={[{ targetField: 'name', sourceColumn: 0 }]} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Preview could not be prepared. Your current mapping is retained.')
    expect(screen.getByRole('combobox', { name: 'Ad (required)' })).toHaveValue('0')
    expect(screen.getByRole('button', { name: 'Save mapping draft' })).toBeEnabled()
  })

  it('displays uploaded header text as text without creating uploaded elements', () => {
    const { container } = render(<ImportMappingEditor {...base} headers={['<img src=x onerror=alert(1)>']} />)
    expect(container.querySelector('img')).toBeNull()
    expect(screen.getAllByRole('option', { name: '1 — <img src=x onerror=alert(1)>' })).toHaveLength(2)
  })

  it('refuses malformed templates instead of presenting system fields as required user input', () => {
    render(<ImportMappingEditor {...base} template={{ ...template, fields: [{ key: 'tenant_id', label: 'Kiracı', writable: false, required: true }] }} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Geçerli bir içe aktarma şablonu gerekiyor.')
    expect(screen.queryAllByRole('button')).toHaveLength(0)
  })

  it('keeps an empty-source draft savable and does not offer a successful preview', () => {
    render(<ImportMappingEditor {...base} headers={[]} />)
    expect(screen.getByRole('status')).toHaveTextContent('Kaynak kolonu bulunamadı.')
    expect(screen.getByRole('button', { name: 'Eşleme taslağını kaydet' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Doğrula ve önizle' })).toBeDisabled()
  })
})
