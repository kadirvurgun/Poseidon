# CERTIFICATE-CANDIDATE-LOADING-01 — await the authorized candidate page

Status: **AUTHORED / execution DEFERRED / acceptance false**. This corrects the
genuine test's observation ordering; product and server authority are unchanged.

Active `poseidon-v46.3-20261030`: 18.01 `Backlog` row45 DEV-044 / T-S6-002,
dependency DEV-043 (SHA256
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`).
Renewal retains exact original and replacement revision/version lineage;
unverified draft planning cannot supersede the original certificate.

The `faf7f91` genuine diagnostic failed on the More candidates enabled assertion
at [certificate-cases.ts](../../tests/Poseidon.Browser.Tests/register-specs/certificate-cases.ts):96.
Status-only telemetry did not establish the live candidate contents. Source
inspection confirms an initial-page race: the loop can check for an option while
[CertificateReplacementPicker](../../frontend/src/product/CertificateReplacementPicker.tsx)
is still loading. When the candidate arrives on page1 with `hasMore=false`, the
test is already waiting for the correctly disabled next-page button.

The journey now waits for the actual replacement selector to become enabled
before its first option check and after each page transition. The existing
32-page bound, authorized current Vessel candidate filters, exact selected pin,
renewal payload, history and unchanged original-certificate assertions remain.
There is no fixed delay, retry restart, alternative accepted state or timeout
extension. Missing/failed candidates still fail the gate.

The new [component regression](../../frontend/src/product/CertificateReplacementPicker.test.tsx)
holds the initial read pending: select and pagination are disabled, no pin emits.
Resolving a real component's mocked first-page response enables the selector,
keeps Next disabled and emits the exact chosen candidate revision/version.
This is UI evidence only; no tests/build/browser were executed during authoring.
The next larger combined gate and independent final-target QA remain mandatory.
