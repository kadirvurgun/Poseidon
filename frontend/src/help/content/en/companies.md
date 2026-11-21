---
key: companies
title: Companies
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: []
reviewed: false
---
## What this page is for
The Companies page lists the **company records** of your workspace that you are authorized to see. It answers: "Which companies are defined in this workspace, and what are their code and name?" Ship and tug records belong to a company, so the right company is the first step of choosing your context.

## Who uses it
- **Tenant Admin:** to create and edit company records under least privilege (13.03 §9).
- **Shore operations:** to find the right company context.

## Before you start
- You need permission to read companies to see this page. Without it the message "You do not have access to this workspace." is shown.
- Creating a record needs create permission. Whether you can edit a record depends on the permission the server grants for that record.

## Step by step
1. See each company's code and name in the list. Use **Previous** / **Next** for other pages.
2. To search, type in **Search by code or name** and press **Search**.
3. Select a company name to open the **Details** section.
4. For a new company, press **New record**, enter the code and name and press **Save**. All fields are required.
5. To change an existing record, use **Edit** in the details.
6. Open **Show legal entity record** to see legal entity information; this section is read-only.

## What the states mean
- **No records found:** no record was returned for your search or permission scope.
- **Legal entity: Draft – incomplete, not active / Active / Inactive:** the legal entity record's state. Draft means values are still missing before activation.
- **Unknown:** the value is not in the record. Unknown does not mean "No" or "valid".
- **Saved:** the record was written to the server. This record creates no operational approval.

## What you cannot do / caution
- Creating or editing a company record is not an operational, compliance or safety approval.
- Legal entity values are not changed from this page; they change only through audited HR commands. Registry identifiers are shown masked.
- The same code cannot be used by two companies.
- If you leave the page with an unsaved draft, you are asked whether to discard it or keep editing.

## If something goes wrong
- **This record changed in another operation:** your draft is kept. Discard the draft to load the current record, then make your change again.
- **This code is already in use:** enter a different code.
- **The outcome could not be confirmed:** fields stay locked until the outcome is known; safely retry the same command.
- **Your permission may have changed / No access:** check your role and workspace context; contact your tenant administrator if needed (13.03 §10).
- **The service is unavailable:** use "Retry"; if the problem continues, report it with the support reference.

## Related pages
