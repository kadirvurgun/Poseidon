import { act, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { ScopedDistribution } from './ScopedDistribution'

const engine = vi.hoisted(() => ({ load: vi.fn(), render: vi.fn(), resize: vi.fn(), dispose: vi.fn() }))
vi.mock('./scope-chart-loader', () => ({ loadScopeChartRenderer: engine.load }))

beforeEach(() => {
  engine.load.mockResolvedValue({ renderScopeChart: engine.render })
  engine.render.mockReturnValue({ resize: engine.resize, dispose: engine.dispose })
})
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); engine.load.mockReset(); engine.render.mockReset(); engine.dispose.mockReset(); engine.resize.mockReset() })

it('provides source counts as accessible text even when SVG initialization is unavailable', () => {
  render(<ScopedDistribution label="This page only" data={[{ name: 'Ships', value: 2, color: '#4381da' }, { name: 'Tugs', value: 1, color: '#74a4c4' }]} />)
  expect(screen.getByRole('figure', { name: 'This page only' })).toHaveTextContent('Ships: 2 · Tugs: 1')
  expect(engine.load).not.toHaveBeenCalled()
})

it('disposes the old scope chart before a new data scope is mounted and on unmount', async () => {
  engine.render.mockReturnValue({ resize: engine.resize, dispose: engine.dispose })
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({ width: 600, height: 200, x: 0, y: 0, top: 0, left: 0, bottom: 200, right: 600, toJSON: () => ({}) })
  vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} })
  const first = [{ name: 'Ships', value: 2, color: '#4381da' }]
  const second = [{ name: 'Tugs', value: 1, color: '#74a4c4' }]
  const mounted = render(<ScopedDistribution label="Authorized page" data={first} />)
  await waitFor(() => expect(engine.render).toHaveBeenCalledTimes(1))
  mounted.rerender(<ScopedDistribution label="Authorized page" data={second} />)
  await waitFor(() => expect(engine.render).toHaveBeenCalledTimes(2))
  expect(engine.dispose).toHaveBeenCalledTimes(1)
  expect(screen.getByRole('figure')).not.toHaveTextContent('Ships: 2')
  mounted.unmount(); expect(engine.dispose).toHaveBeenCalledTimes(2)
})

it('loads no engine while hidden and shares one pending import until a visible chart can initialize', async () => {
  let width = 0; let notify!: ResizeObserverCallback
  let finish!: (value: { renderScopeChart: typeof engine.render }) => void
  engine.load.mockReturnValue(new Promise(resolve => { finish = resolve }))
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(() => ({ width, height: 200, x: 0, y: 0, top: 0, left: 0, bottom: 200, right: width, toJSON: () => ({}) }))
  vi.stubGlobal('ResizeObserver', class { constructor(callback: ResizeObserverCallback) { notify = callback } observe() {} disconnect() {} })
  render(<ScopedDistribution label="Authorized page" data={[{ name: 'Ships', value: 2, color: '#4381da' }]} />)
  await act(async () => { await Promise.resolve() })
  expect(engine.load).not.toHaveBeenCalled()
  expect(engine.render).not.toHaveBeenCalled()
  width = 600
  await act(async () => { notify([], {} as ResizeObserver); notify([], {} as ResizeObserver) })
  expect(engine.load).toHaveBeenCalledTimes(1)
  expect(engine.render).not.toHaveBeenCalled()
  width = 0
  await act(async () => finish({ renderScopeChart: engine.render }))
  expect(engine.render).not.toHaveBeenCalled()
  width = 600; await act(async () => notify([], {} as ResizeObserver))
  await waitFor(() => expect(engine.render).toHaveBeenCalledTimes(1))
  expect(engine.load).toHaveBeenCalledTimes(1)
})

it('ignores an engine import resolving after unmount and disconnects its observer', async () => {
  let finish!: (value: { renderScopeChart: typeof engine.render }) => void
  const disconnect = vi.fn()
  engine.load.mockReturnValue(new Promise(resolve => { finish = resolve }))
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({ width: 600, height: 200, x: 0, y: 0, top: 0, left: 0, bottom: 200, right: 600, toJSON: () => ({}) })
  vi.stubGlobal('ResizeObserver', class { observe() {} disconnect = disconnect })
  const mounted = render(<ScopedDistribution label="Authorized page" data={[{ name: 'Ships', value: 2, color: '#4381da' }]} />)
  expect(engine.load).toHaveBeenCalledTimes(1)
  mounted.unmount(); expect(disconnect).toHaveBeenCalledTimes(1)
  await act(async () => finish({ renderScopeChart: engine.render }))
  expect(engine.render).not.toHaveBeenCalled()
})

it('retains authoritative text when a visible chart engine fails without retrying every resize', async () => {
  let notify!: ResizeObserverCallback
  engine.load.mockRejectedValue(new Error('Synthetic optional engine unavailable'))
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({ width: 600, height: 200, x: 0, y: 0, top: 0, left: 0, bottom: 200, right: 600, toJSON: () => ({}) })
  vi.stubGlobal('ResizeObserver', class { constructor(callback: ResizeObserverCallback) { notify = callback } observe() {} disconnect() {} })
  render(<ScopedDistribution label="Authorized page" data={[{ name: 'Ships', value: 2, color: '#4381da' }]} />)
  await act(async () => { await Promise.resolve() })
  await act(async () => { notify([], {} as ResizeObserver); notify([], {} as ResizeObserver) })
  expect(engine.load).toHaveBeenCalledTimes(1)
  expect(engine.render).not.toHaveBeenCalled()
  expect(screen.getByRole('figure', { name: 'Authorized page' })).toHaveTextContent('Ships: 2')
})
