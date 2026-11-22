---
key: data-trust-center
title: Data trust center
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## What this page is for
The data trust center shows **data quality signals** for controls, evidence and vessel profile fields of the selected vessel. It answers: "Which records on this vessel are missing, stale, conflicting, unverified or orphan, and what source and freshness information says so?" A trust signal is not verification of the underlying record. The page never marks anything ready.

## Who uses it
- **Regulatory / Assurance user:** to avoid leaving orphan or incomplete records in the Requirement → Control → Evidence → Verification chain (13.03 §8).
- **DPA:** to follow evidence and verification gaps (13.03 §4).
- **Technical Superintendent / Master:** to see the signals that hold a readiness assessment at review required (13.03 §2, §5).

## Before you start
- In the top context bar, check that the **right company** is selected (13.03 §1).
- The page needs control, evidence, file, applicability and tenant regulatory draft read access, all together.
- If no vessel is selected, the page first shows the "Choose a vessel" list.

## Step by step
1. Pick the vessel from the "Choose a vessel" list.
2. In **Summary**, read the overall state ("Review required" or "No material signal") and the **Subjects per signal** counts.
3. In the **Controls and evidence** table, check each subject's "Source / provenance", "Freshness", "Verification" and "Signals" columns.
4. Note the reason code next to a signal badge and review the record on its own page.
5. See the signals of profile fields in the **Vessel profile fields** table.
6. Use **Previous** / **Next** for other subjects.
7. If "These signals may be out of date." appears, press **Reload**.

## What the states mean
- **Missing / Stale / Conflicting / Unverified / Orphan:** the kind of signal on a subject. A signal is a quality marker; it does not prove the record right or wrong.
- **No signal:** no signal was found on this subject. This does not mean the record is verified.
- **Review required:** a material signal exists. Readiness is held at review required while material signals exist.
- **No material signal:** this is not a readiness statement.
- **Human verified:** an authorised person verified the evidence. This comes from the evidence's own record; the center does not verify anything.
- **Not verified / Verification expired / Verification rejected:** the verification state of the evidence.
- **Not applicable to controls:** shown on control rows; the verification state is for evidence rows.
- **Observation time unknown / No freshness policy / Future-dated:** freshness could not be established or the date is in the future. None of these mean "current".

## What you cannot do / caution
- Records are not corrected, verified or closed from this page; that happens on the record's own page by an authorised person.
- If only the first subjects were assessed, the summary stays review required.
- Subjects you cannot read are counted but not shown. A record you cannot see does not mean there is no issue.
- Offline, data trust signals cannot be refreshed.

## If something goes wrong
- **You are not authorised to read data trust signals:** check your role and vessel context; contact your tenant administrator if needed (13.03 §10).
- **Record can't be verified:** check the evidence and its provenance; raise it with the authorised verifier.
- **Unexpected signal:** raise the reason code with the control owner or subject matter expert.
- **Data trust signals are unavailable:** use "Retry"; if it persists, report it to support with a screenshot, the time and your steps.

## Related pages
