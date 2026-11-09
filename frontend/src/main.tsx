import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import './styles.css'
import './design-system.css'
import { initializeAppLanguage } from './i18n'

// Product modules register their local catalog loaders while App is imported.
// Resolve a stored locale before first paint; a failed catalog keeps Turkish.
void initializeAppLanguage().then(() => {
  createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>)
})
