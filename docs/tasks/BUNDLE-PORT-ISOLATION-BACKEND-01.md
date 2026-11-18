# BUNDLE-PORT-ISOLATION-01 — Development OIDC transport slice

State: SOURCE AUTHORED; acceptance=false; all NEW test/build/provider/browser execution DEFERRED until the complete port-isolation bundle gate and independent QA. Parent baseline8a9462d; branch foundation/owned-oidc-transport-20261113.

The earlier backend/API evidence executed5e17850 and reused identical backend inputs at8a9462d remains historical. It does not validate the new transport sources.

## Authority and integration contract

Active poseidon-v46.3-20261030. Existing DEV-OIDC-LAB-01 LAB-04/LAB-05 and DEV-013/T-S1-001:18.01 Backlog row14;15.01 API specification paragraphs9,64–67,287–295; security baseline Development-only private CA/hostname/expiry/EKU boundary. This is an engineering prerequisite for isolated genuine-provider gates, not an enterprise IdP decision, operational catalog, human appointment or pilot sign-off. There is no new persistence entity, migration, credential or seed.

The lab/provider/browser author and backend author agreed on the trusted configuration key `DevelopmentLab:OidcPort` before implementation. It is an optional canonical integer1024–65535. Omission preserves8443; explicit null, empty, padded, signed, nonnumeric, leading-zero and out-of-range values fail closed. A selected port requires Development and explicit `DevelopmentLab:OidcCaPath`. The server's configured Authority and Issuer must be identical. The transport requires the exact canonical authority `https://localhost:{selectedPort}/realms/poseidon-lab`.

The driver separately owns the run-bound port manifest, private immutable configuration, distinct app/provider/database ports, provider client callback URLs and process/container ownership. No request header, query, actor, role or redirect supplies this transport selection. The backend does not read or modify an existing private profile or running preview.

## Delivered source behavior

- `DevelopmentOidcTransport.ConfiguredPort` reads only trusted IConfiguration and distinguishes an absent key from a supplied null. The existing three-argument Create call preserves8443; explicit calls can pin a selected nonprivileged port.
- `FoundationIdentity` passes the trusted selected port, refuses selected-port configurations lacking explicit Development CA trust and rejects local authority/issuer mismatches. Code+PKCE, HTTPS metadata, token issuer/audience/lifetime/signature validation, nonce/state/correlation, secure cookies, bounded session and relative product return URL behavior remain authoritative.
- Each backchannel request is pinned to HTTPS, localhost, selected port and the same realm. Userinfo, query, fragment, encoded-path aliases and a Host override differing from the selected authority are rejected. The socket connects directly to IPv4 loopback with proxies disabled, independently of DNS or ambient proxy configuration. Redirect following and transport cookies are disabled.
- The existing custom-root chain validation is retained: platform hostname/presence errors, expiry, server EKU and unrelated roots cannot be bypassed. The root stays scoped to the handler and is disposed with it; no global certificate store or production TLS policy is changed.

## Authored acceptance cases and future gate

The existing DevelopmentOidcTransportTests are extended with absent/default and canonical port selection; explicit malformed/null values; mismatched/default/selected ports and noncanonical or remote authority rejection before CA loading; realm/query/fragment/userinfo/encoded-path request rejection before connecting; actual ephemeral-loopback TLS for valid, redirect, wrong name, unrelated root, client-only EKU and expired certificates; factory Development/CA/issuer boundaries; and selected/default OIDC options retaining exact issuer, code+PKCE and full token validation. The old default8443 tests and shared Validate behavior remain covered by their existing sources.

All new cases are AUTHOR-ONLY, NOT RUN. The next complete bundle gate must compile with warnings/analyzers enforced, run the relevant transport/API unit and existing OIDC/security regressions, independently review the exact combined target, and only then apply an owned isolated runtime and execute actual signed-provider/browser journeys. There is no skip, authority substitution, unqualified PASS or canonical task/SP/screen acceptance from this source checkpoint.
