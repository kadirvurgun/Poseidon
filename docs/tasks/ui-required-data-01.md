# UI-REQUIRED-DATA-01

Source: 14.02 sheet:Yonetici Ozeti row19 POL-DEFAULT-01; 13.06 executive row18; backend/Poseidon.Domain/RequiredDataPolicy.cs

Implement a reusable React required-data input component and behavior tests in frontend/src/required-data/ for the approved POL-DEFAULT-01 preparation library. Read poseidon-ui skill, backend RequiredDataPolicy and tests, current frontend test conventions. Read targeted relevant design-system 13.04 text using source query. Values must be explicitly Missing/Unknown/Known with typed Text/Boolean/Number payloads; blank text is missing, false and zero known. Do not duplicate backend authority or use a green Ready/Approved label. Provide labeled keyboard accessible controls with explicit unknown selection, required-field messages and TR/EN strings. Parent callback receives typed draft value without mutations. Validation can explain missing/unknown required input but confers no submit authorization. Trusted field contract is code-owned; do not invent operational mandatory fields. Component tests cover clear/input/unknown/false/zero, required messages, optional behavior, language and accessible labels. Do not wire a fake operational form into App or invent API/persistence. Document this as reusable UI preparation, canonical operational screens still zero. No dependencies or source originals changed.

Acceptance:
- Reusable accessible TR/EN component distinguishes missing/unknown/known without implicit false
- Meaningful component tests cover false, zero, whitespace, unknown, language and keyboard behavior
- No business submit endpoint or operational readiness claim introduced
