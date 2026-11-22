---
key: getting-started
title: Getting started
reviewed: false
---
## How to use this manual
This manual explains, step by step, how to use POSEIDON. It is generated from the same page guides that open inside the application with the **?** button, so the two never say different things.
- **Getting started** (this chapter) shows how to sign in, choose the right company and vessel, find a page and open its help.
- **The common working model** explains the one way of working that every page follows.
- **Role chapters** list, for each role in the role-based handbook (13.03), the tasks of that role and the pages it uses.
- **Page guides** describe every page with numbered steps. Where a step has a picture, the picture shows the same number as a green marker.
- **Glossary of states** and **If something goes wrong** close the manual.

The pictures were taken automatically from a test environment that contains only synthetic (invented) data. Names such as "Synthetic Fleet A" or "DEMO-SHIP" are not real companies or vessels. Some fields are greyed out in the pictures because they change on every run.

This manual is a **draft**. A maritime subject-matter expert and a UX writer have not reviewed it yet. It explains how the screens work; it never decides whether a ship or tug is ready, safe or compliant. That decision always belongs to the authorized person, for example the Master or Tug Master.

## Sign in
1. Open the POSEIDON address your organisation gave you in a browser.
2. Press **Sign in**. You are sent to your organisation's sign-in page; POSEIDON never asks for your password itself.
3. After signing in you return to POSEIDON. If an administrator invited you, you first see the invitation page; read what it grants and accept it, then sign in again.
4. To finish work, use **Sign out** at the top of the page. A session that has expired asks you to sign in again; unsaved input on the screen is not sent automatically.

## Check the context first
Every record belongs to a company (tenant) and usually to a vessel: a ship or a tug. Before you read or record anything, look at the context bar at the top of the page.
1. Check the **company** name.
2. Check the **vessel** and the **vessel mode** (Ship or Tug). Use **Change vessel context** or **Switch vessel** to change it.
3. Check the **role context**. What you see and what you may do depends on the role the server has given you for this vessel.

If the context is wrong, change it first. A record saved in the wrong context is a wrong record, even if every field is correct.

## Find a page
1. Open the menu on the left. On a narrow screen, open it with the menu button at the top.
2. Pages are grouped: Overview, Ship operations, Technical, Crew, Tug operations, Compliance and assurance, Workspace.
3. Type in **Filter menu** to filter the menu. Only pages you are authorized to open are searched; a page you cannot open is never shown, named or counted.
4. Use **Search workspace** (or `Ctrl K`) to search records you may read.

## Open the help of a page
1. On any page, press the **?** button next to the page title, or press `Shift` + `?` when you are not typing in a field.
2. The help panel opens with three tabs: **Guide** (the page guide in this manual), **Why** (where to find the reason behind a record) and **AI** (turned off in this release).
3. Move between tabs with the arrow keys. Press `Escape` or **Close help** to close the panel; the focus returns to the **?** button.
4. Opening the panel sends nothing to the server: it does not start a command, a record read or an AI call.
5. "Related pages" in the panel only shows pages you are authorized to open.

## Appearance, language and offline work
- **Appearance** changes the colour theme. It never changes the meaning of a state: an unknown or unverified value is never shown as a green "ready".
- **Choose language** changes the screen language. Turkish and English guides are written by the product team; other languages show the English guide and say so.
- When the connection drops, the page says **Offline**. Data shown is the last copy and may be old. Critical work can continue on pages that support it; queued changes are sent when the connection returns. A conflict is never merged automatically: you resolve it in the **Material conflict center**.
