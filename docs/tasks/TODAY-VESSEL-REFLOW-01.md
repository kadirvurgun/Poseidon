# TODAY-VESSEL-REFLOW-01

Source: user-reported Today-page overflow screenshot,13.11.2026; active13.04
design-system responsive layout/accessibility contract, with17 locale directions.
Parent phase: S2 workspace; source baseline002a931. Acceptance and added SP:0.

The Today vessel grid used a fixed240px minimum and horizontal icon/body rows.
The text body's automatic minimum width let long, unbroken vessel codes/names
escape their cards. The selector's ad hoc pagination also spread controls over
the page. Do not fix this by hiding horizontal overflow or dropping identities.

- **TVR01:** Responsive grid tracks may shrink to their container; shared Card
  headers and identity flex items have an explicit zero minimum. Full codes and
  names wrap inside each card, with the original strings retained as accessible
  names and titles. No clipping, ellipsis, fixture filtering or value mutation.
- **TVR02:** Each card uses the shared Button with a consistent full-width footer;
  actions align at the bottom while text remains readable. Previous/next and
  refresh use EnterprisePagination and EnterpriseActions. Same disabled/offline,
  pagination limits and authorized target URL; no business writes or approvals.
- **TVR03:** Preserve keyboard activation, logical RTL alignment, all17 translations
  and full accessible identities. The two authored component variants cover TR
  and Arabic identities at the existing wire length limits.
- **TVR04:** The authored Playwright spec in the normal specs test directory covers
  six long-identity records at1365/580/390/320px, RTL, full text, page/card/button
  bounds, keyboard navigation and zero writes. It uses synthetic transport and
  no screenshots/traces/video; it is not real-backend authority evidence.

New tests and the larger lint/typecheck/component/browser gate are **DEFERRED**
under the user's phase-delivery cadence. A user-requested preview build may update
the open application without implying regression PASS. Independent source/QA
review is pending the reported agent usage limit; no retry/account rotation,
new PR, merge, canonical sprint completion or release acceptance is claimed.
