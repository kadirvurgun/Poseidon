import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { OfflineConflictReview } from './OfflineConflictReview'
import type { OfflineConflict } from './OfflineConflictReview'

const conflict: OfflineConflict = {
  id: 'synthetic-conflict', revision: 'revision-1', contextKey: 'actor/tenant/vessel/device/policy',
  fields: [
    { key: 'watchGroup', label: 'Vardiya grubu', captured: { kind: 'known', text: 'Sentetik A' }, server: { kind: 'known', text: 'Sentetik B' }, choices: ['keepServer', 'useCaptured'] },
    { key: 'note', label: 'Not', captured: { kind: 'unknown' }, server: { kind: 'known', text: '' }, choices: ['keepServer', 'useCaptured'] },
  ],
}
const base = {
  language: 'tr' as const, contextKey: conflict.contextKey, state: 'ready' as const,
  conflict, canResolve: true, online: true, operation: 'idle' as const, reasonLimit: 200,
  onResolve: vi.fn(), onCheckOutcome: vi.fn(),
}

describe('15.01 material conflict review (authored; execution deferred)', () => {
  it('DEV-035 displays before/local/server context while submitting only explicit choices and the human reason', async () => {
    const resolve = vi.fn(), user = userEvent.setup()
    const withBase: OfflineConflict = { ...conflict, fields: [{ ...conflict.fields[0], before: { kind: 'known', text: 'Retained base A' } }] }
    render(<OfflineConflictReview {...base} language="en" conflict={withBase} onResolve={resolve} />)
    expect(screen.getByText('Before capture').nextElementSibling).toHaveTextContent('Retained base A')
    expect(screen.getByText('Captured').nextElementSibling).toHaveTextContent('Sentetik A')
    expect(screen.getByText('Server').nextElementSibling).toHaveTextContent('Sentetik B')
    expect(screen.getByRole('button', { name: 'Submit resolution' })).toBeDisabled()
    await user.click(screen.getByRole('radio', { name: 'Keep server value' }))
    await user.type(screen.getByRole('textbox', { name: 'Resolution reason (required)' }), 'Compare exact retained versions')
    await user.click(screen.getByRole('button', { name: 'Submit resolution' }))
    expect(resolve).toHaveBeenCalledWith({ conflictId: conflict.id, expectedRevision: conflict.revision, reason: 'Compare exact retained versions', decisions: [{ field: 'watchGroup', choice: 'keepServer' }] })
    expect(JSON.stringify(resolve.mock.calls[0][0])).not.toContain('Retained base A')
  })
  it('shows captured/server/resolved values without silently preselecting a resolution', () => {
    render(<OfflineConflictReview {...base} />)
    expect(screen.getByText('Sentetik A')).toBeInTheDocument()
    expect(screen.getByText('Sentetik B')).toBeInTheDocument()
    expect(screen.getByText('Bilinmiyor')).toBeInTheDocument()
    expect(screen.getByText('Boş metin')).toBeInTheDocument()
    expect(screen.getAllByText('Seçim yapılmadı')).toHaveLength(2)
    expect(screen.getAllByRole('radio').every(radio => !(radio as HTMLInputElement).checked)).toBe(true)
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
  })

  it('requires an explicit choice for every field and a nonblank reason before submission', async () => {
    const user = userEvent.setup(), resolve = vi.fn()
    render(<OfflineConflictReview {...base} onResolve={resolve} />)
    const watch = screen.getByRole('group', { name: 'Vardiya grubu' })
    const note = screen.getByRole('group', { name: 'Not' })
    await user.click(within(watch).getByRole('radio', { name: 'Yakalanan değeri kullan' }))
    expect(within(watch).getAllByText('Sentetik A')).toHaveLength(2)
    await user.type(screen.getByRole('textbox', { name: 'Çözüm gerekçesi (zorunlu)' }), '   ')
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
    await user.click(within(note).getByRole('radio', { name: 'Sunucu değerini koru' }))
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
    await user.clear(screen.getByRole('textbox'))
    await user.type(screen.getByRole('textbox'), 'Sentetik inceleme gerekçesi')
    await user.click(screen.getByRole('button', { name: 'Çözümü gönder' }))
    expect(resolve).toHaveBeenCalledWith({ conflictId: conflict.id, expectedRevision: 'revision-1',
      reason: 'Sentetik inceleme gerekçesi', decisions: [
        { field: 'watchGroup', choice: 'useCaptured' }, { field: 'note', choice: 'keepServer' },
      ] })
    const submitted = resolve.mock.calls[0][0]
    expect(submitted).not.toHaveProperty('actorId')
    expect(submitted).not.toHaveProperty('tenantId')
    expect(submitted.decisions[0]).not.toHaveProperty('value')
  })

  it('keeps unknown distinct from false or missing when explicitly selecting the captured value', async () => {
    const user = userEvent.setup()
    render(<OfflineConflictReview {...base} />)
    const field = screen.getByRole('group', { name: 'Not' })
    await user.click(within(field).getByRole('radio', { name: 'Yakalanan değeri kullan' }))
    expect(within(field).getAllByText('Bilinmiyor')).toHaveLength(2)
    expect(within(field).queryByText('Hayır')).not.toBeInTheDocument()
    expect(within(field).queryByText('Bildirilmedi')).not.toBeInTheDocument()
  })

  it('honors each field choice allowlist and does not enable an unsupported captured override', () => {
    const restricted = { ...conflict, fields: [{ ...conflict.fields[0], choices: ['keepServer'] as const }] }
    render(<OfflineConflictReview {...base} conflict={restricted} />)
    expect(screen.getByRole('radio', { name: 'Yakalanan değeri kullan' })).toBeDisabled()
    expect(screen.getByRole('radio', { name: 'Sunucu değerini koru' })).toBeEnabled()
  })

  it('blocks resolution of withheld values even if stale capability flags advertise a choice', () => {
    const withheld: OfflineConflict = { ...conflict, fields: [{ ...conflict.fields[0], captured: { kind: 'withheld' } }] }
    render(<OfflineConflictReview {...base} conflict={withheld} />)
    expect(screen.getByText('Gösterilmiyor')).toBeInTheDocument()
    expect(screen.queryByText('Sentetik A')).not.toBeInTheDocument()
    expect(screen.getAllByRole('radio').every(radio => (radio as HTMLInputElement).disabled)).toBe(true)
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
  })

  it('does not treat an unconfigured field policy as permission to keep or override values', () => {
    const unavailable = { ...conflict, fields: [{ ...conflict.fields[0], choices: [] }] }
    render(<OfflineConflictReview {...base} conflict={unavailable} />)
    expect(screen.getByText('Geçerli alan politikasıyla çözüm yapılamıyor.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
  })

  it('allows a current read-only view without enabling mutation', () => {
    render(<OfflineConflictReview {...base} canResolve={false} />)
    expect(screen.getByRole('status')).toHaveTextContent('çözüm yetkiniz yok.')
    expect(screen.getByText('Sentetik A')).toBeInTheDocument()
    expect(screen.getByRole('textbox')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
  })

  it('blocks submission while disconnected instead of pretending the resolution was accepted locally', () => {
    render(<OfflineConflictReview {...base} online={false} />)
    expect(screen.getByRole('status')).toHaveTextContent('bağlanın ve oturumunuzu doğrulayın')
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
  })

  it('does not retry an uncertain mutation with a new command; it offers outcome lookup instead', async () => {
    const user = userEvent.setup(), resolve = vi.fn(), check = vi.fn()
    render(<OfflineConflictReview {...base} operation="outcomeUnknown" onResolve={resolve} onCheckOutcome={check} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Gönderimin sonucu bilinmiyor.')
    await user.click(screen.getByRole('button', { name: 'Çözümü gönder' }))
    await user.click(screen.getByRole('button', { name: 'Gönderim sonucunu sorgula' }))
    expect(resolve).not.toHaveBeenCalled()
    expect(check).toHaveBeenCalledOnce()
  })

  it('retains entered reason and explicit choices when the server rejects a submission', async () => {
    const user = userEvent.setup()
    const { rerender } = render(<OfflineConflictReview {...base} />)
    await user.click(within(screen.getByRole('group', { name: 'Vardiya grubu' })).getByRole('radio', { name: 'Sunucu değerini koru' }))
    await user.type(screen.getByRole('textbox'), 'Sentetik gerekçe')
    rerender(<OfflineConflictReview {...base} operation="failed" />)
    expect(screen.getByRole('alert')).toHaveTextContent('Çözüm kabul edilmedi.')
    expect(screen.getByRole('textbox')).toHaveValue('Sentetik gerekçe')
    expect(within(screen.getByRole('group', { name: 'Vardiya grubu' })).getByRole('radio', { name: 'Sunucu değerini koru' })).toBeChecked()
  })

  it('drops old choices and reason when the server conflict revision changes', async () => {
    const user = userEvent.setup()
    const { rerender } = render(<OfflineConflictReview {...base} />)
    await user.click(within(screen.getByRole('group', { name: 'Vardiya grubu' })).getByRole('radio', { name: 'Sunucu değerini koru' }))
    await user.type(screen.getByRole('textbox'), 'Old revision reason')
    rerender(<OfflineConflictReview {...base} conflict={{ ...conflict, revision: 'revision-2' }} />)
    expect(screen.getByRole('textbox')).toHaveValue('')
    expect(screen.getAllByRole('radio').every(radio => !(radio as HTMLInputElement).checked)).toBe(true)
  })

  it('hides all prior values and reason immediately when session/scope/policy context changes', async () => {
    const user = userEvent.setup()
    const { rerender } = render(<OfflineConflictReview {...base} />)
    await user.type(screen.getByRole('textbox'), 'Private draft reason')
    rerender(<OfflineConflictReview {...base} contextKey="other-context" />)
    expect(screen.getByRole('status')).toHaveTextContent('Geçerli oturum ve seçim')
    expect(screen.queryByText('Sentetik A')).not.toBeInTheDocument()
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it.each(['loading', 'denied', 'unavailable', 'stale'] as const)('withholds private conflict data in %s state', state => {
    render(<OfflineConflictReview {...base} state={state} />)
    expect(screen.queryByText('Sentetik A')).not.toBeInTheDocument()
    expect(screen.queryByRole('radio')).not.toBeInTheDocument()
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
  })

  it('renders uploaded or captured text as inert text', () => {
    const inert: OfflineConflict = { ...conflict, fields: [{ ...conflict.fields[0], captured: { kind: 'known', text: '<img src=x onerror=alert(1)>' } }] }
    const { container } = render(<OfflineConflictReview {...base} conflict={inert} />)
    expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeInTheDocument()
    expect(container.querySelector('img')).toBeNull()
  })

  it('fails closed for duplicate field keys rather than applying one decision to different fields', () => {
    render(<OfflineConflictReview {...base} conflict={{ ...conflict, fields: [conflict.fields[0], conflict.fields[0]] }} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Çakışma ayrıntıları yüklenemedi.')
    expect(screen.queryByRole('radio')).not.toBeInTheDocument()
  })

  it('does not enable resolution without a configured reason input limit', () => {
    render(<OfflineConflictReview {...base} reasonLimit={0} />)
    expect(screen.getByRole('alert')).toHaveTextContent('Çözüm politikası kullanılamıyor.')
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
  })

  it('locks an in-flight submission against duplicate clicks and edits', () => {
    render(<OfflineConflictReview {...base} operation="submitting" />)
    expect(screen.getByRole('region')).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByRole('textbox')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Çözümü gönder' })).toBeDisabled()
  })

  it('offers an explicit details reload without exposing old fields on failure', async () => {
    const user = userEvent.setup(), retry = vi.fn()
    render(<OfflineConflictReview {...base} state="unavailable" onRetry={retry} />)
    await user.click(screen.getByRole('button', { name: 'Tekrar dene' }))
    expect(retry).toHaveBeenCalledOnce()
    expect(screen.queryByRole('radio')).not.toBeInTheDocument()
  })

  it('renders English unknown/missing values without inventing a resolved default', () => {
    const missing: OfflineConflict = { ...conflict, fields: [{ ...conflict.fields[1], server: { kind: 'missing' } }] }
    render(<OfflineConflictReview {...base} language="en" conflict={missing} />)
    expect(screen.getByText('Unknown')).toBeInTheDocument()
    expect(screen.getByText('Not provided')).toBeInTheDocument()
    expect(screen.getByText('No choice made')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Submit resolution' })).toBeDisabled()
  })
})
