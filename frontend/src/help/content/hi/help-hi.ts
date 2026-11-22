// One lazily loaded chunk per locale (USER-GUIDE-01 D1). The raw Markdown of every guide in this
// folder is bundled into that single chunk, so the public offline shell precaches it with the rest
// of the application assets and the inventory stays one entry per locale, not one per page.
const guides = import.meta.glob<string>('./*.md', { eager: true, query: '?raw', import: 'default' })
export default guides
