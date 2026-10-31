# ADR: draft required-data boundary

Task: POL-DEFAULT-01-DOMAIN. Status: implemented boundary, independent review pending.
Source locators and hashes: [task contract](../tasks/POL-DEFAULT-01-DOMAIN.md).

DraftValue privately constructs Missing, Unknown or Known typed values. Null nullable booleans/numbers and whitespace text become Missing; explicit false and decimal zero remain Known. Unknown carries no payload. No generic object factory or inferred default is exposed.

Trusted domain code defines field keys, types and required flags. Keys are exact ordinal identifiers with no surrounding whitespace. Invalid enum values, blank keys, duplicate definitions/inputs, undefined input fields, null entries and mismatched types fail explicitly. Field-specific mandatory lists are not invented here. Future request adapters must never bind the policy definitions from request data.

Assessment snapshots input values for the call, preserves draft state and returns immutable blocking keys/reasons in ordinal order. It reports only RequiredDataGateSatisfied. Consumers must separately enforce authorization, semantic validity, evidence and human decisions before submission/processing. No full permission/readiness property, authorization grant, safety approval, production lookup catalog or permit activation is provided. Optional unknown is retained without failing this narrow gate; applicability review is a separate domain concern.

No persistence dependency or entity exists, so migration, audit writes and seed fixtures do not apply. API/tenant OIDC and permit lifecycle remain open work. This decision does not close a canonical sprint, phase or release gate.
