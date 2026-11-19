import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'
import { changeAppLanguage, localeStorageKey } from './i18n'

// Each mounted application starts with its own browser location, as a fresh page would.
beforeEach(async () => {
  window.history.replaceState(null, '', '/')
  // A test is a fresh browser page; production keeps this preference on navigation.
  window.localStorage.removeItem(localeStorageKey)
  // A fresh page is also a fresh tab: no retained per-tab command envelopes.
  window.sessionStorage.clear()
  await changeAppLanguage('tr', false)
})

afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.useRealTimers() })

// Native dialog semantics absent from jsdom; real browser tests keep the platform implementation.
if (!HTMLDialogElement.prototype.showModal) HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
if (!HTMLDialogElement.prototype.close) HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
