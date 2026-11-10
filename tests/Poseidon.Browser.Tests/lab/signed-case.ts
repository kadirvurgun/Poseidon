// Authentication is a genuine per-case precondition, never a cached ticket or
// a retry of a failed journey. Provider lifetime and application policy remain unchanged.
export type SignedAccount = 'lab-manager' | 'lab-restricted'
export type SignedCasePhase = 'LOGIN' | 'JOURNEY'
export async function runSignedCase<Page>(page: Page,
  entry: { readonly restricted: boolean; readonly run: (page: Page) => Promise<void> },
  authenticate: (page: Page, account: SignedAccount, destination: string) => Promise<void>,
  phase?: (phase: SignedCasePhase) => void): Promise<void> {
  phase?.('LOGIN')
  await authenticate(page, entry.restricted ? 'lab-restricted' : 'lab-manager', '/app/today')
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
