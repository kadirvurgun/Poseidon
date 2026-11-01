# IMPORT-PERMISSION-BOUNDARIES-01

DEV-069 / T-S11-001, 18.01 Backlog row70 (SHA
09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674):
controlled import, validation, reject report, provenance and no bypass of domain
validation. Repository security baseline requires current tenant/role isolation,
attributed actions and antiforgery. These security regressions implement that
baseline; they do not invent new operational approvals.

Thirteen authored integration cases use the actual API host and PostgreSQL test
fixture, not mocked persistence. Eight cases cover category write revocation for
vessel, crew, certificate and equipment both before commit and after a durable
receipt exists. They preserve exactly the original session cookie/CSRF and first
prove reads still work, avoiding an unrelated session-mismatch denial. The same
idempotency key must not restore withdrawn write permission. State/version,
receipt count and domain absence are asserted where applicable.

Three cases withdraw category read access while keeping import permission and
the same session. Existing raw batch, preview, reject report, history and template
must remain hidden, including no-store responses and safe errors. Two cases
exercise missing/invalid antiforgery on mapping, preview and commit, checking
persisted version/state, domain absence and no receipt.

These tests depend on the real IMPORT-FOUNDATION-01 API/test partial class. They
are authored in an older isolated support base and must be integrated AFTER
that checkpoint. Inspect final API contracts and preserve the intended security
behavior when adapting a changed contract. No suites, builds, browser journeys,
QA or CI were run. Status: AUTHORED / UNVERIFIED, never accepted SP.
