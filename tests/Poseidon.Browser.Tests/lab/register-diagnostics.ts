// Closed diagnostics for the genuine register gate. Never return Error.message,
// arbitrary property values, raw stack frames, URLs, identifiers, headers or bodies.
import { sep } from 'node:path';
import { pathToFileURL } from 'node:url';
const matchers = new Set(['toBe', 'toEqual', 'toStrictEqual', 'toBeTruthy', 'toBeFalsy',
  'toBeVisible', 'toBeHidden', 'toBeEnabled', 'toBeDisabled', 'toBeChecked', 'toHaveCount',
  'toHaveText', 'toContainText', 'toHaveValue', 'toHaveValues', 'toHaveAttribute',
  'toHaveURL', 'toHaveTitle', 'toBeAttached', 'toBeFocused', 'toHaveCSS', 'toHaveJSProperty']);
const kinds = new Set(['Error', 'TypeError', 'RangeError', 'SyntaxError', 'TimeoutError', 'AssertionError']);
const methods = new Set(['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS']);
const escapeRegex = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

export type PublicFailure = {
  kind: 'EXPECTATION' | 'Error' | 'TypeError' | 'RangeError' | 'SyntaxError' | 'TimeoutError' | 'AssertionError' | 'OTHER';
  matcher: string | null;
  source: { file: string; line: number; column: number; evidence: 'INFERRED_ERROR_STACK_FRAME' } | null;
};
export type PublicSourceCatalog = { root: string; files: Readonly<Record<string, readonly number[]>> };

export function publicFailure(error: unknown, sources: PublicSourceCatalog): PublicFailure {
  const result: PublicFailure = { kind: 'OTHER', matcher: null, source: null };
  if (!error || typeof error !== 'object') return result;
  try {
    const value = error as { name?: unknown; message?: unknown; stack?: unknown; matcherResult?: { name?: unknown } };
    const name = value.name;
    if (typeof name === 'string' && kinds.has(name)) result.kind = name as PublicFailure['kind'];
    const matcher = value.matcherResult?.name;
    if (typeof matcher === 'string' && matchers.has(matcher)) {
      result.kind = 'EXPECTATION'; result.matcher = matcher;
    }
    const stack = value.stack;
    if (typeof stack !== 'string' || !stack || stack.length > 65536) return result;
    const message = value.message;
    if (typeof name !== 'string' || name.length > 128 || typeof message !== 'string' || message.length > 65536) return result;
    // Error.message can contain valid-shaped indented frames. Remove the entire
    // exact message header, not merely line0; an uncertain header has no location.
    const header = name + (message ? ': ' + message : '');
    if (!stack.startsWith(header + '\n')) return result;
    const roots = [sources.root.replaceAll('\\', '/').replace(/\/$/, '') + '/',
      pathToFileURL(sources.root + sep).href];
    const allowed = Object.keys(sources.files).filter(file => /^[a-z][a-z0-9-]*\.ts$/.test(file));
    for (const frame of stack.slice(header.length + 1).replaceAll('\\', '/').split('\n')) {
      if (!/^\s+at\s/.test(frame)) continue;
      for (const file of allowed) {
        const found = frame.match(new RegExp(`(?:\\(|\\s)(?:${roots.map(escapeRegex).join('|')})${escapeRegex(file)}:(\\d{1,6}):(\\d{1,6})\\)?$`));
        if (!found) continue;
        const line = Number(found[1]), column = Number(found[2]);
        const lengths = sources.files[file];
        if (line < 1 || line > lengths.length || column < 1 || column > lengths[line - 1] + 1) continue;
        result.source = { file: `tests/Poseidon.Browser.Tests/register-specs/${file}`, line, column,
          evidence: 'INFERRED_ERROR_STACK_FRAME' };
        return result;
      }
    }
  } catch {
    // A hostile/custom error getter is not permission to serialize the object.
    // Already closed fields remain useful; no fallback stringification occurs.
  }
  return result;
}

export type PublicResponse = { route: string; method: string; status: number; count: number };

export function responseLedger(origin: string, paths: Readonly<Record<string, unknown>>, limit = 64) {
  if (!Number.isSafeInteger(limit) || limit < 1 || limit > 256) throw new Error('invalid_diagnostic_limit');
  const selected = new URL(origin);
  if (selected.protocol !== 'https:' || selected.origin !== origin || selected.username || selected.password)
    throw new Error('invalid_diagnostic_origin');
  const routes = Object.entries(paths).flatMap(([route, operations]) => {
    if (!/^\/[A-Za-z0-9_{}./-]*$/.test(route) || !operations || typeof operations !== 'object' || Array.isArray(operations))
      throw new Error('invalid_diagnostic_contract');
    // The real OpenAPI also contains /health/live. It is a valid contract entry,
    // but outside this API-only ledger; do not reject the entire register gate.
    if (!route.startsWith('/api/v1/')) return [];
    const segments = route.split('/');
    return [{ route, pattern: new RegExp('^' + segments.map(segment => /^\{[A-Za-z][A-Za-z0-9_]*\}$/.test(segment) ? '[^/]+' : escapeRegex(segment)).join('/') + '$'),
      specificity: segments.filter(segment => !segment.startsWith('{')).length,
      methods: new Set(Object.keys(operations).map(method => method.toUpperCase()).filter(method => methods.has(method))) }];
  }).sort((a, b) => b.specificity - a.specificity || a.route.localeCompare(b.route));
  const rows = new Map<string, PublicResponse>();
  let ignored = 0, overflow = 0;
  const increment = (value: number) => Math.min(1_000_000, value + 1);
  return {
    observe(url: string, method: string, status: number) {
      try {
        const target = new URL(url);
        if (target.origin !== origin || target.username || target.password || !methods.has(method) ||
          !Number.isInteger(status) || status < 100 || status > 599) { ignored = increment(ignored); return; }
        const route = routes.find(candidate => candidate.pattern.test(target.pathname) && candidate.methods.has(method));
        if (!route) { ignored = increment(ignored); return; }
        const key = `${route.route}:${method}:${status}`;
        const existing = rows.get(key);
        if (existing) { existing.count = increment(existing.count); return; }
        if (rows.size >= limit) { overflow = increment(overflow); return; }
        rows.set(key, { route: route.route, method, status, count: 1 });
      } catch { ignored = increment(ignored); }
    },
    snapshot() {
      return { scope: 'MAIN_CASE_CONTEXT_ONLY' as const,
        responses: [...rows.values()].map(row => ({ ...row })).sort((a, b) =>
          a.route.localeCompare(b.route) || a.method.localeCompare(b.method) || a.status - b.status), ignored, overflow };
    },
  };
}
