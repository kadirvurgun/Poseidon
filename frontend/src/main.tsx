import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import { PilotTelemetryHost } from './product/PilotTelemetryHost'
import './styles.css'
import './design-system.css'
import './theme/theme-builder.css'
import { initializeAppLanguage } from './i18n'
import { initializeAppearance } from './theme/appearance-store'

const disposeAppearance = initializeAppearance()
if (import.meta.hot) import.meta.hot.dispose(disposeAppearance)

// Product modules register their local catalog loaders while App is imported.
// Resolve a stored locale before first paint; a failed catalog keeps Turkish.
void initializeAppLanguage().then(() => {
  createRoot(document.getElementById('root')!).render(<StrictMode><App /><PilotTelemetryHost /></StrictMode>)
})
