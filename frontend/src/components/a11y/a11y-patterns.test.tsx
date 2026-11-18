import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { FormErrorSummary } from './FormErrorSummary'
import { focusableTargets, inertOutside, trapTab } from './modal'

describe('DEV-025 shared modal helpers', () => {
  it('focusableTargets skips tabindex=-1, disabled, hidden, inert and closed-disclosure content', () => {
    render(<div data-testid="box">
      <button>One</button><button tabIndex={-1}>Sample</button><button disabled>Off</button>
      <div hidden><button>Hidden</button></div><div inert><button>Inert</button></div>
      <details><summary>More</summary><button>Inside closed</button></details><a href="#x">Link</a>
    </div>)
    expect(focusableTargets(screen.getByTestId('box')).map(el => el.textContent)).toEqual(['One', 'More', 'Link'])
  })
  it('inertOutside marks every branch outside the modal and restores only what it changed', () => {
    render(<><nav data-testid="nav" /><div><p data-testid="sibling" /><section data-testid="modal"><button>In</button></section></div><aside data-testid="pre" inert /></>)
    const restore = inertOutside(screen.getByTestId('modal'))
    expect(screen.getByTestId('nav').closest('[inert]')).not.toBeNull()
    expect(screen.getByTestId('sibling')).toHaveAttribute('inert')
    expect(screen.getByTestId('modal').closest('[inert]')).toBeNull()
    restore()
    expect(screen.getByTestId('nav').closest('[inert]')).toBeNull()
    expect(screen.getByTestId('sibling')).not.toHaveAttribute('inert')
    expect(screen.getByTestId('pre')).toHaveAttribute('inert')
  })
  it('trapTab wraps at both ends and pulls focus back in from outside', () => {
    render(<><button>Outside</button><div data-testid="trap"><button>First</button><button>Last</button></div></>)
    const trap = screen.getByTestId('trap'), event = (shiftKey: boolean) => ({ key: 'Tab', shiftKey, preventDefault() {} })
    screen.getByRole('button', { name: 'Last' }).focus()
    expect(trapTab(event(false), trap)).toBe(true)
    expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()
    expect(trapTab(event(true), trap)).toBe(true)
    expect(screen.getByRole('button', { name: 'Last' })).toHaveFocus()
    screen.getByRole('button', { name: 'Outside' }).focus()
    trapTab(event(false), trap)
    expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()
  })
  it('a named radio group is one tab stop: the checked radio, else the first', () => {
    render(<div data-testid="box">
      <button>Before</button>
      <input type="radio" name="base" aria-label="stone" /><input type="radio" name="base" aria-label="zinc" defaultChecked /><input type="radio" name="base" aria-label="gray" />
      <input type="radio" name="accent" aria-label="emerald" /><input type="radio" name="accent" aria-label="teal" />
      <button>After</button>
    </div>)
    expect(focusableTargets(screen.getByTestId('box')).map(el => el.getAttribute('aria-label') ?? el.textContent)).toEqual(['Before', 'zinc', 'emerald', 'After'])
  })
  it('trapTab wraps from the last stop even when the last control is a radio group', () => {
    render(<div data-testid="trap"><button>First</button><input type="radio" name="r" aria-label="a" /><input type="radio" name="r" aria-label="b" defaultChecked /><input type="radio" name="r" aria-label="c" /></div>)
    const trap = screen.getByTestId('trap'), event = (shiftKey: boolean) => ({ key: 'Tab', shiftKey, preventDefault() {} })
    screen.getByRole('button', { name: 'First' }).focus()
    expect(trapTab(event(true), trap)).toBe(true)
    expect(screen.getByRole('radio', { name: 'b' })).toHaveFocus()
    expect(trapTab(event(false), trap)).toBe(true)
    expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()
  })
})

function Form() {
  const [attempt, setAttempt] = useState(0)
  const [value, setValue] = useState('')
  const invalid = attempt > 0 && !value
  return <form noValidate onSubmit={event => { event.preventDefault(); setAttempt(n => n + 1) }}>
    <FormErrorSummary title="Check these fields before saving:" attempt={attempt} errors={invalid ? [{ fieldId: 'f-code', label: 'Code', message: 'This field is required.' }] : []} />
    <label htmlFor="f-code">Code *</label>
    <input id="f-code" value={value} aria-invalid={invalid} aria-describedby={invalid ? 'f-code-error' : undefined} onChange={event => setValue(event.target.value)} />
    {invalid && <span id="f-code-error">This field is required.</span>}
    <button type="submit">Save</button>
  </form>
}

describe('DEV-025 form error summary (65.03 paragraph 167)', () => {
  it('DEV025-AC-08 focuses the summary on each submit, links to the field and is not a live region', async () => {
    const user = userEvent.setup()
    render(<Form />)
    expect(screen.queryByRole('group')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Save' }))
    const summary = screen.getByRole('group', { name: 'Check these fields before saving:' })
    expect(summary).toHaveFocus()
    expect(summary).not.toHaveAttribute('aria-live')
    expect(summary).not.toHaveAttribute('role', 'alert')
    const field = screen.getByLabelText('Code *')
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(field).toHaveAccessibleDescription('This field is required.')
    await user.click(within(summary).getByRole('link', { name: 'Code: This field is required.' }))
    expect(field).toHaveFocus()
    // Typing a correction neither steals focus nor re-announces.
    await user.type(field, 'A')
    expect(field).toHaveFocus()
    expect(screen.queryByRole('group')).not.toBeInTheDocument()
    expect(field).toHaveAttribute('aria-invalid', 'false')
  })
})
