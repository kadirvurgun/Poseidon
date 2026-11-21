import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { HelpMarkdown, helpAnchorId } from './HelpMarkdown'
import { HelpContentError, parseHelpDocument } from './help-model'

const policy = (routes: string[] = ['/app/fleet']) => ({ allowedRoutes: new Set(routes), onNavigate: vi.fn(), onAnchor: vi.fn() })

describe('safe help Markdown', () => {
  it('renders the supported subset as elements', () => {
    const { container } = render(<HelpMarkdown idPrefix="t" policy={policy()} source={'### Sub\nFirst **bold** and `code`.\n\n1. one\n2. two\n\n- a\n- b'}/>)
    expect(screen.getByRole('heading', { name: 'Sub' })).toHaveAttribute('id', 'help-sub')
    expect(container.querySelector('strong')).toHaveTextContent('bold')
    expect(container.querySelector('code')).toHaveTextContent('code')
    expect(container.querySelectorAll('ol > li')).toHaveLength(2)
    expect(container.querySelectorAll('ul > li')).toHaveLength(2)
  })

  it('never creates raw HTML, script or image elements from content', () => {
    const { container } = render(<HelpMarkdown idPrefix="t" policy={policy()} source={'<script>alert(1)</script> <img src=x onerror=alert(1)> <b>x</b>'}/>)
    expect(container.querySelector('script, img, b')).toBeNull()
    expect(container).toHaveTextContent('<script>alert(1)</script>')
  })

  it('links only to granted internal routes and panel anchors; everything else is plain text', async () => {
    const p = policy()
    const { container } = render(<HelpMarkdown idPrefix="t" policy={p} source={'[Fleet](/app/fleet) [Hidden](/app/user-admin) [Ext](https://example.test) [Js](javascript:alert(1)) [Up](#steps)'}/>)
    const links = [...container.querySelectorAll('a')]
    expect(links.map(link => link.getAttribute('href'))).toEqual(['/app/fleet', '#steps'])
    expect(container).toHaveTextContent('Hidden')
    await userEvent.click(screen.getByRole('link', { name: 'Fleet' }))
    expect(p.onNavigate).toHaveBeenCalledWith('/app/fleet')
    await userEvent.click(screen.getByRole('link', { name: 'Up' }))
    expect(p.onAnchor).toHaveBeenCalledWith('help-steps')
  })

  it('derives ASCII anchors from Turkish headings', () => {
    expect(helpAnchorId('İlgili sayfalar')).toBe('help-ilgili-sayfalar')
    expect(helpAnchorId('Durumlar ne anlama gelir')).toBe('help-durumlar-ne-anlama-gelir')
  })
})

describe('help content model', () => {
  const headings = ['What this page is for', 'Who uses it', 'Before you start', 'Step by step', 'What the states mean', 'What you cannot do / caution', 'If something goes wrong', 'Related pages']
  const doc = (front: string, body = headings.map(h => `## ${h}\nText.`).join('\n')) => `---\n${front}\n---\n${body}\n`
  const front = 'key: today\ntitle: Today\nroles: [Master]\npermissions: [canRead]\nrelated: [fleet]\nscreenshots: []\nreviewed: false'

  it('parses the fixed front matter and 8 sections', () => {
    const parsed = parseHelpDocument(doc(front), 'en')
    expect(parsed.meta).toEqual({ key: 'today', title: 'Today', roles: ['Master'], permissions: ['canRead'], related: ['fleet'], screenshots: [], reviewed: false })
    expect(parsed.sections).toHaveLength(8)
    expect(parseHelpDocument(doc(front.replace('reviewed: false', 'reviewed: { by: Human SME, date: 2026-12-01 }')), 'en').meta.reviewed).toEqual({ by: 'Human SME', date: '2026-12-01' })
  })

  it.each([
    ['missing field', front.replace('screenshots: []\n', '')],
    ['extra field', front + '\nextra: x'],
    ['reviewed true', front.replace('reviewed: false', 'reviewed: true')],
    ['self related', front.replace('related: [fleet]', 'related: [today]')],
    ['non-list roles', front.replace('roles: [Master]', 'roles: Master')],
  ])('rejects %s', (_name, value) => {
    expect(() => parseHelpDocument(doc(value), 'en')).toThrow(HelpContentError)
  })

  it('rejects missing, reordered or wrong-language sections', () => {
    expect(() => parseHelpDocument(doc(front, headings.slice(0, 7).map(h => `## ${h}\nx`).join('\n')), 'en')).toThrow(HelpContentError)
    expect(() => parseHelpDocument(doc(front, [...headings].reverse().map(h => `## ${h}\nx`).join('\n')), 'en')).toThrow(HelpContentError)
    expect(() => parseHelpDocument(doc(front), 'tr')).toThrow(HelpContentError)
  })
})
