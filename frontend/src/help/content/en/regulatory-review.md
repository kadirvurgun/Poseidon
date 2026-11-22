---
key: regulatory-review
title: Regulatory SME review
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## What this page is for
Regulatory SME review records campaigns in which a human expert reviews the source and applicability items of a ship or tug. It answers: "Which source and requirement items for this vessel were confirmed by an authorised reviewer, and which are still open and tracked?" It is a human review record only: the system approves no requirement, activates no rule or permit and claims no class or type approval (13.03 §8).

## Who uses it
- **Regulatory / Assurance user:** open review campaigns, record verdicts on items and close the campaign (13.03 §8).
- **Appointed reviewer (SME):** confirm or track items within their own source authority.
- **DPA / HSEQ:** watch open and untracked items (13.03 §4).

## Before you start
- Viewing needs regulatory review read. Opening and closing campaigns needs manage; recording verdicts needs the verdict permission and a current reviewer appointment for this scope.
- Choose **Ship** or **Tug** in the **Review scope** tabs, then open the right vessel in **Choose a vessel** (13.03 §1).
- Sources should be registered as official and versioned on the [Source drafts](/app/sources) page (13.03 §8).
- Offline, data may be out of date and commands are disabled.

## Step by step
1. Choose the scope and vessel. If there is no campaign press **Open review campaign**.
2. In **Source ownership matrix** review each source authority's category, open and confirmed counts and owner roles.
3. Select an item in **Review items**.
4. In the **Record review verdict** form fill **Reviewer appointment**, **Verdict**, **Source authority** and **Owner role**. If the item has no registered source, enter **Authoritative source reference** and **Source version / edition**. For an item that stays open, enter a **Tracked issue ID**.
5. Write a 10–2000 character reason in **Reviewer note** and press **Record**.
6. When all items are settled, choose a **Closure outcome** in **Close campaign**, write the **Closure reason** and press **Close campaign**.

## What the states mean
- Campaign: **Open**, **In review**, **Closed with open items**, **Closed**.
- Verdict: **Confirmed**, **Not applicable (confirmed)**, **Review required**, **Ambiguous**. When critical data is unknown the item stays **Review required**; an unknown item is never counted as "Not applicable".
- **Candidate (unverified):** the source is not yet tied to a verified official source.
- **Stale — pin changed** and **Vessel profile changed:** the source or the vessel profile changed after the verdict; the item must be reviewed again.
- **Untracked open items:** open items without a tracked issue. While above zero the campaign cannot be closed.
- The categories **Statutory law**, **Class rule**, **Company SMS** and **Guidance — not law** never substitute for one another (13.03 §8).

## What you cannot do / caution
- The person who opened a campaign cannot record verdicts in it; without a reviewer appointment verdicts are read-only.
- A reviewer whose qualification is unverified may track open items but cannot confirm. A stale item cannot be confirmed.
- Confirming needs a registered source or an authoritative source reference with its edition. Leave no orphan or unsourced record in the Requirement → Control → Evidence → Verification chain (13.03 §8).
- **Closed** requires every item confirmed; otherwise close with open items, each one tracked.
- Do not state class or type approval in the note. In the tug scope, no towage limit without a controlled source is a legal default.
- Verdicts are recorded only by human users; AI and automation accounts cannot record them.

## If something goes wrong
- **The record changed. Reload and review before continuing.** Press **Reload**.
- **The outcome is uncertain. Retry sends the same command.** Use **Retry**.
- **The review rules refused this command.** Check whether the item is stale, the source reference and the tracked issue.
- **You do not have access to regulatory review for this vessel.** or **You are not authorized for this action.** Check your role and scope; contact the Tenant Admin if needed (13.03 §10).
- If applicability looks unexpected, check the source and context inputs and talk to the control owner or SME (13.03 §10).

## Related pages
