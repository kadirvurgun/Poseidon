---
key: requirements
title: Requirement drafts
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## What this page is for
The Requirement drafts page keeps the **requirements** derived from a source revision as versioned drafts and binds each requirement to an exact source revision. It answers: "Which revision of which source does this requirement rest on, and what is its recorded lifecycle?" A requirement is the first link of the chain Requirement → Control → Evidence → Verification. No record in that chain should be left unlinked (orphan) (13.03 §8).

## Who uses it
- **Regulatory / Assurance user:** binds the requirement to its source, evaluates applicability with context inputs and keeps the chain complete (13.03 §8).
- **DPA / HSEQ:** reads the basis of a requirement during finding and audit work (13.03 §4).

## Before you start
- Viewing needs tenant-wide read and regulatory draft read; editing also needs regulatory draft write.
- Record the source first on the **Source drafts** page. A requirement can be bound only to an existing source revision.
- Saving needs a live server connection; offline, edits are kept in the tab.

## Step by step
1. See the requirement drafts in the list; search with **Search drafts** and **Search**.
2. For a new requirement press **New draft**.
3. In **Draft fields** choose a **State** for each field (**Missing**, **Unknown**, **Known (declared)**) and enter the known values: **Instrument**, **Version / amendment declaration**, **Declared effective date**, **Declaration / provenance**, **Source observed at (with UTC offset)**.
4. In **Immutable source citation** type in **Find a source** and press **Search sources**. Choose the source in **Source draft**, then the exact revision in **Exact revision**. Check the chosen revision's content with **Selected immutable source content**.
5. Press **Save draft**. When the record is open, the **Recorded lifecycle** and **Immutable history** sections appear.
6. Use **Lifecycle and applicability** for applicability and **Controls and evidence** for control and evidence links. The **Critical orphan report** inside **Controls and evidence** lists critical declarations without requirement links; link those records to a requirement.

## What the states mean
- **Recorded lifecycle:** the state the server derives from bound rules: **Draft**, **Adopted**, **Future effective**, **In force**, **Superseded**. If it cannot be read, **Unavailable** is shown; the system does not assume draft or in force.
- "Bound rules record different lifecycle states. The shown value is a software precedence; expert review is required." The shown value is then not a decision; an expert review is needed.
- **Unknown:** the information is not known. When critical data is unknown the applicability result stays **Review required** (REVIEW_REQUIRED); it is never counted as "not applicable" or "no" (13.03 §8).
- **Applicability review required / Requirement link missing / Verification not performed:** open items in the control-evidence chain.
- **Completion unknown · Unverified · Effectiveness unknown:** completion, verification and effectiveness are separate states. A completed record is not verified, and a verified record is not effective.

## What you cannot do / caution
- You cannot bind a requirement to "the latest" source; an **Exact revision** is always chosen. When the source gets a new revision, the old binding is kept in history.
- An applicability preview establishes no compliance, readiness, permit or legal approval: "PREVIEW / DRAFT. Predictions do not establish compliance, readiness, permits or legal approval."
- A lifecycle change goes through **Request human review**; source admission and a qualified independent reviewer decide. The system never closes it on its own.
- Do not assume class, flag, company and local rule sources stand in for each other (13.03 §8).

## If something goes wrong
- **Source selection unavailable.** Press **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Open the current revision with **Reload saved draft**.
- **Check field states, bounds, dates and source revision.** If you chose a source, choose its exact revision too.
- **History unavailable. Editor retained.** Press **Retry history**.
- If the lifecycle is unexpected, review the bound rules and source revisions; contact the control owner or subject-matter expert (SME) if needed (13.03 §10).
- Without permission, check your role and tenant scope and ask the Tenant Admin (13.03 §10).

## Related pages
