---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## What this page is for
The Users and permissions page manages the **memberships** in your workspace. It answers: "What can this person see and do, and on which vessels?" The account itself (user name, password, MFA) lives in the central identity provider (IdP), not in POSEIDON. This page **invites** that account to a membership and grants role bundles, permissions and vessel scope (13.03 §9). A new member is invited: the invitation grants nothing until the person signs in and accepts it.

## Who uses it
- **Tenant administrator:** assigns user, role, vessel and object scope with least privilege, invites new members and manages delegations and suspension (13.03 §9).
- Other users with membership read access can view the list but not change it.
- The **invited person** only opens the invitation link; they never see this page.

## Before you start
- Viewing needs membership read; changing needs membership manage plus tenant-wide manage. With read access only you see "You can view but not change memberships."
- The person needs an account in the identity provider. The **Expected IdP subject (optional)** limits the invitation to that one account; without it, whoever signs in with the link can accept it.
- Plan a trusted channel to hand over the invitation link (for example your organisation's own messaging). POSEIDON does not send e-mail.
- You can grant only permissions you hold yourself. Another administrator edits your own membership.
- Membership administration needs a live server connection; nothing can be changed offline.

## Step by step
1. See members in the **Members** list. Filter by **Status** (for example **Invited**) and **Role bundle**, then press **Apply**. To search, enter a membership or account id prefix or the exact IdP subject and press **Search**.
2. Open a member with **Open**. Review **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** and **Delegations**.
3. To invite a member press **Add member**. Check **IdP issuer**, optionally enter the **Expected IdP subject (optional)**, choose at least one **Role bundle**, add **Individual permissions** if needed and set the scope (tenant-wide or at least one vessel). Then press **Create membership**.
4. The member opens as **Invited** with the **Invitation link**. Press **Copy invitation link** and hand the link over. The link is shown only now; it works once and expires after 24 hours.
5. If the link was lost or expired, press **Create a new invitation link**; the previous link stops working. To withdraw the invitation press **Revoke invitation**.
6. The invited person opens the link, signs in, checks what the invitation grants and presses **Accept invitation**. After signing in again they work with the grant you set; the member then shows **Active** and **Invitation accepted**.
7. To change an existing member's grant press **Edit grant**, then **Save grant**. Each save creates a new grant revision; older revisions stay in **Grant history**.
8. For a temporary hand-over use **Delegate from this member** and fill in the delegate, **Valid from**, **Valid until**, **Reason** and **Delegated permissions**.
9. To stop access use **Suspend** in **Lifecycle**; **Resume** reopens it. **Revoke membership** is final.

## What the states mean
- **Invited:** the membership waits for the invited person. It grants nothing and reserves the account nowhere, also not in any other organisation.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** the state of the latest invitation link. An expired, replaced or revoked link can never be accepted.
- **Active / Suspended / Revoked:** the membership lifecycle. A suspended or revoked member's next request is denied.
- **Grant source — Managed here:** the grant was given on this page and can be changed here.
- **Grant source — Operator configuration:** the grant comes from the server configuration (bootstrap/break-glass) and cannot be edited here.
- **Grant source — No grant:** a record exists but grants nothing.
- **Not held by you / You do not hold every permission of this bundle:** you cannot grant this permission or bundle because you do not hold it.
- **Saved. The change takes effect on the member's next request.** An invited member's access starts at the next login after accepting.

## What you cannot do / caution
- You cannot grant a permission, vessel or tenant-wide access you do not hold.
- You cannot change your own membership; the last active administrator cannot be removed.
- An account can hold an accepted membership in one organisation only. An invitation never moves an account that already belongs elsewhere; the invited person then sees only that the invitation cannot be accepted.
- Treat the invitation link like a key: anyone who holds it and signs in can accept it unless you entered an expected subject. Do not post it in shared channels. Always pass on the whole link: the secret part after **#** is read only by the browser and never reaches the server or its logs.
- Decision, verification and approval permissions are never in a bundle. Only an administrator who holds them can grant them, one by one. Appointment-gated actions (for example tug or departure decisions) also need an operator appointment. Granting a role bundle does not appoint anyone as Master or verifier.
- You cannot approve a delegation where you are source or delegate. A delegation never exceeds the source's current authority and is never tenant-wide.
- POSEIDON stores no passwords, keeps only a fingerprint of the invitation link and never shows IdP subjects back in the list.

## If something goes wrong
- **This identity is configured by the operator, already a member here or already invited here.** Open the existing member instead, or revoke the old invitation first.
- **The invited person sees "This invitation cannot be accepted":** the link expired, was used, replaced or revoked, belongs to another account, or their account already belongs to an organisation. Check the **Invitation** state and press **Create a new invitation link** if needed.
- **The link was not copied:** select the link field and copy it manually.
- **Exceeds your own authority:** choose a narrower bundle or scope, or ask an administrator who holds the permission.
- **The record changed meanwhile:** press **Reload current state** and make your change again.
- **The outcome is unknown. Retry with the same request or reload.** You can safely resend the same command.
- **Not the trusted identity provider:** check the issuer. If the account comes from another IdP, contact the system administrator (13.03 §10).

## Related pages
