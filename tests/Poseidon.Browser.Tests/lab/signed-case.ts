// Authentication is a genuine per-case precondition, never a cached ticket or
// a retry of a failed journey. Provider lifetime and application policy remain unchanged.
export type SignedAccount = 'lab-manager' | 'lab-restricted' | 'lab-crew-boundary'
export type SignedCasePhase = 'LOGIN' | 'JOURNEY'
export async function runSignedCase<Page>(page: Page,
  entry: { readonly restricted: boolean; readonly account?: 'lab-crew-boundary'; readonly run: (page: Page) => Promise<void> },
  authenticate: (page: Page, account: SignedAccount, destination: string) => Promise<void>,
  phase?: (phase: SignedCasePhase) => void): Promise<void> {
  // Account metadata selects a genuine provider login, never a permission grant.
  // Validate at runtime too: native manifest imports are not a trusted type cast.
  if (!entry || typeof entry !== 'object' || typeof entry.restricted !== 'boolean' ||
      'account' in entry && (!Object.hasOwn(entry, 'account') || entry.restricted || entry.account !== 'lab-crew-boundary'))
    throw new Error('invalid_signed_account')
  phase?.('LOGIN')
  await authenticate(page, entry.account ?? (entry.restricted ? 'lab-restricted' : 'lab-manager'), '/app/today')
  phase?.('JOURNEY')
  await entry.run(page)
}

export function signedDestination(value: string): string {
  // Match the server's local-return boundary and never navigate a credentialed
  // browser to a caller-provided origin, encoded separator or traversing path.
  if (typeof value !== 'string' || value.length > 512 ||
      !(value === '/app' || value.startsWith('/app/')) || /[%\\\p{Cc}]/u.test(value) ||
      value.split(/[?#]/, 1)[0].split('/').some(part => part === '.' || part === '..'))
    throw new Error('invalid_signed_destination')
  return value
}
