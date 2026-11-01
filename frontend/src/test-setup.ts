import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

// Each mounted application starts with its own browser location, as a fresh page would.
beforeEach(() => window.history.replaceState(null, '', '/'))

afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.useRealTimers() })

// Native dialog semantics absent from jsdom; real browser tests keep the platform implementation.
if (!HTMLDialogElement.prototype.showModal) HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
if (!HTMLDialogElement.prototype.close) HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
