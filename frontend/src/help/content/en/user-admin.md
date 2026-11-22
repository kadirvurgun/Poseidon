---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 4]
reviewed: false
---
## What this page is for
The Users and permissions page manages the **memberships** in your workspace. It answers: "What can this person see and do, and on which vessels?" The account itself (user name, password, MFA) lives in the central identity provider (IdP), not in POSEIDON. This page only binds that account to a membership and grants role bundles, permissions and vessel scope (13.03 §9).

## Who uses it
- **Tenant administrator:** assigns user, role, vessel and object scope with least privilege, and manages delegations and suspension (13.03 §9).
- Other users with membership read access can view the list but not change it.

## Before you start
- Viewing needs membership read; changing needs membership manage plus tenant-wide manage. With read access only you see "You can view but not change memberships."
- Before adding a member, create the person's account in the identity provider and obtain its **IdP subject**.
- You can grant only permissions you hold yourself. Another administrator edits your own membership.
- Membership administration needs a live server connection; nothing can be changed offline.

## Step by step
1. See members in the **Members** list. Filter by **Status** and **Role bundle**, then press **Apply**. To search, enter a membership or account id prefix or the exact IdP subject and press **Search**.
2. Open a member with **Open**. Review **Effective permissions**, **Vessel scope**, **Grant history** and **Delegations**.
3. To add a member press **Add member**. Enter **IdP issuer** and **IdP subject**, choose at least one **Role bundle**, add **Individual permissions** if needed and set the scope (tenant-wide or at least one vessel). Then press **Create membership**.
4. To change an existing member's grant press **Edit grant**, then **Save grant**. Each save creates a new grant revision; older revisions stay in **Grant history**.
5. For a temporary hand-over use **Delegate from this member** and fill in the delegate, **Valid from**, **Valid until**, **Reason** and **Delegated permissions**.
6. To stop access use **Suspend** in **Lifecycle**; **Resume** reopens it. **Revoke membership** is final.

## What the states mean
- **Active / Suspended / Revoked:** the membership lifecycle. A suspended or revoked member's next request is denied.
- **Grant source — Managed here:** the grant was given on this page and can be changed here.
- **Grant source — Operator configuration:** the grant comes from the server configuration (bootstrap/break-glass) and cannot be edited here.
- **Grant source — No grant:** a record exists but grants nothing.
- **Not held by you / You do not hold every permission of this bundle:** you cannot grant this permission or bundle because you do not hold it.
- **Saved. The change takes effect on the member's next request.** A new member's access starts at the next login.

## What you cannot do / caution
- You cannot grant a permission, vessel or tenant-wide access you do not hold.
- You cannot change your own membership; the last active administrator cannot be removed.
- Decision, verification and approval permissions are never in a bundle. Only an administrator who holds them can grant them, one by one. Appointment-gated actions (for example tug or departure decisions) also need an operator appointment. Granting a role bundle does not appoint anyone as Master or verifier.
- You cannot approve a delegation where you are source or delegate. A delegation never exceeds the source's current authority and is never tenant-wide.
- POSEIDON stores no passwords and never shows IdP subjects back in the list.

## If something goes wrong
- **This identity is already provisioned or configured:** the person is already a member or is managed by the operator configuration.
- **Exceeds your own authority:** choose a narrower bundle or scope, or ask an administrator who holds the permission.
- **The record changed meanwhile:** press **Reload current state** and make your change again.
- **The outcome is unknown. Retry with the same request or reload.** You can safely resend the same command.
- **Not the trusted identity provider:** check the issuer. If the account comes from another IdP, contact the system administrator (13.03 §10).

## Related pages
