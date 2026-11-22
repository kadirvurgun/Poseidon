---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Users and permissions“ verwaltet die **Mitgliedschaften** in Ihrem Arbeitsbereich. Sie beantwortet: „Was darf diese Person sehen und tun, und auf welchen Fahrzeugen?“ Das Konto selbst (Benutzername, Passwort, MFA) liegt beim zentralen Identitätsanbieter (IdP), nicht in POSEIDON. Diese Seite **lädt** dieses Konto zu einer Mitgliedschaft **ein** und vergibt Rollenpakete, Berechtigungen und Fahrzeugbereich (13.03 §9). Ein neues Mitglied wird eingeladen: Die Einladung gewährt nichts, bis sich die Person anmeldet und sie annimmt.

## Wer sie verwendet
- **Tenant administrator:** vergibt Benutzer-, Rollen-, Fahrzeug- und Objektbereich nach dem Prinzip der geringsten Berechtigung, lädt neue Mitglieder ein und verwaltet Delegationen und Sperrungen (13.03 §9).
- Andere Benutzer mit Lesezugriff auf Mitgliedschaften können die Liste sehen, aber nicht ändern.
- Die **eingeladene Person** öffnet nur den Einladungslink; sie sieht diese Seite nie.

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie Lesezugriff auf Mitgliedschaften; zum Ändern Verwaltungsrechte für Mitgliedschaften sowie mandantenweite Verwaltungsrechte. Mit reinem Lesezugriff sehen Sie **You can view but not change memberships.**
- Die Person benötigt ein Konto beim Identitätsanbieter. **Expected IdP subject (optional)** beschränkt die Einladung auf genau dieses Konto; ohne diese Angabe kann jeder, der sich mit dem Link anmeldet, sie annehmen.
- Planen Sie einen vertrauenswürdigen Kanal für die Übergabe des Einladungslinks (zum Beispiel den eigenen Messaging-Dienst Ihrer Organisation). POSEIDON versendet keine E-Mails.
- Sie können nur Berechtigungen vergeben, die Sie selbst besitzen. Ihre eigene Mitgliedschaft bearbeitet ein anderer Administrator.
- Die Verwaltung von Mitgliedschaften erfordert eine aktive Serververbindung; offline kann nichts geändert werden.

## Schritt für Schritt
1. Sehen Sie die Mitglieder in der Liste **Members**. Filtern Sie nach **Status** (zum Beispiel **Invited**) und **Role bundle** und drücken Sie dann **Apply**. Zum Suchen geben Sie ein Präfix der Mitgliedschafts- oder Konto-ID oder das exakte IdP-Subject ein und drücken Sie **Suchen**.
2. Öffnen Sie ein Mitglied mit **Öffnen**. Prüfen Sie **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** und **Delegations**.
3. Um ein Mitglied einzuladen, drücken Sie **Add member**. Prüfen Sie **IdP issuer**, geben Sie optional **Expected IdP subject (optional)** ein, wählen Sie mindestens ein **Role bundle**, fügen Sie bei Bedarf **Individual permissions** hinzu und legen Sie den Bereich fest (mandantenweit oder mindestens ein Fahrzeug). Drücken Sie dann **Create membership**.
4. Das Mitglied öffnet sich als **Invited** mit dem **Invitation link**. Drücken Sie **Copy invitation link** und übergeben Sie den Link. Der Link wird nur jetzt angezeigt; er funktioniert einmal und läuft nach 24 Stunden ab.
5. Ist der Link verloren gegangen oder abgelaufen, drücken Sie **Create a new invitation link**; der bisherige Link funktioniert dann nicht mehr. Um die Einladung zurückzuziehen, drücken Sie **Revoke invitation**.
6. Die eingeladene Person öffnet den Link, meldet sich an, prüft, was die Einladung gewährt, und drückt **Accept invitation**. Nach erneuter Anmeldung arbeitet sie mit der von Ihnen festgelegten Berechtigung; das Mitglied zeigt dann **Aktiv** und **Invitation accepted**.
7. Um die Berechtigung eines bestehenden Mitglieds zu ändern, drücken Sie **Edit grant** und dann **Save grant**. Jedes Speichern erzeugt eine neue Revision der Berechtigung; ältere Revisionen bleiben in **Grant history**.
8. Für eine vorübergehende Übergabe verwenden Sie **Delegate from this member** und füllen Sie den Delegierten, **Gültig ab**, **Gültig bis**, **Begründung** und **Delegated permissions** aus.
9. Um den Zugriff zu beenden, verwenden Sie **Suspend** unter **Lifecycle**; **Resume** öffnet ihn wieder. **Revoke membership** ist endgültig.

## Was die Zustände bedeuten
- **Invited:** Die Mitgliedschaft wartet auf die eingeladene Person. Sie gewährt nichts und reserviert das Konto nirgendwo, auch nicht in einer anderen Organisation.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** der Zustand des neuesten Einladungslinks. Ein abgelaufener, ersetzter oder widerrufener Link kann niemals angenommen werden.
- **Active / Suspended / Revoked:** der Lebenszyklus der Mitgliedschaft. Die nächste Anfrage eines gesperrten oder widerrufenen Mitglieds wird abgelehnt.
- **Grant source — Managed here:** Die Berechtigung wurde auf dieser Seite vergeben und kann hier geändert werden.
- **Grant source — Operator configuration:** Die Berechtigung stammt aus der Serverkonfiguration (Bootstrap/Break-Glass) und kann hier nicht bearbeitet werden.
- **Grant source — No grant:** Es gibt einen Datensatz, der aber nichts gewährt.
- **Not held by you / You do not hold every permission of this bundle:** Sie können diese Berechtigung oder dieses Paket nicht vergeben, weil Sie es nicht besitzen.
- **Saved. The change takes effect on the member's next request.** Der Zugriff eines eingeladenen Mitglieds beginnt mit der nächsten Anmeldung nach der Annahme.

## Was Sie nicht tun können / Vorsicht
- Sie können keine Berechtigung, kein Fahrzeug und keinen mandantenweiten Zugriff vergeben, die Sie nicht selbst besitzen.
- Sie können Ihre eigene Mitgliedschaft nicht ändern; der letzte aktive Administrator kann nicht entfernt werden.
- Ein Konto kann nur in einer Organisation eine angenommene Mitgliedschaft haben. Eine Einladung verschiebt niemals ein Konto, das bereits anderswo dazugehört; die eingeladene Person sieht dann nur, dass die Einladung nicht angenommen werden kann.
- Behandeln Sie den Einladungslink wie einen Schlüssel: Jeder, der ihn besitzt und sich anmeldet, kann ihn annehmen, sofern Sie kein erwartetes Subject eingegeben haben. Veröffentlichen Sie ihn nicht in gemeinsamen Kanälen. Geben Sie immer den ganzen Link weiter: Der geheime Teil nach **#** wird nur vom Browser gelesen und erreicht niemals den Server oder dessen Protokolle.
- Entscheidungs-, Verifizierungs- und Genehmigungsberechtigungen sind niemals in einem Paket enthalten. Nur ein Administrator, der sie besitzt, kann sie einzeln vergeben. Aktionen, die an eine Bestellung gebunden sind (zum Beispiel Schlepper- oder Abfahrtsentscheidungen), erfordern zusätzlich eine Bestellung durch den Betreiber. Die Vergabe eines Rollenpakets bestellt niemanden zum Kapitän oder Verifizierer.
- Sie können keine Delegation genehmigen, bei der Sie Quelle oder Delegierter sind. Eine Delegation überschreitet niemals die aktuelle Befugnis der Quelle und ist niemals mandantenweit.
- POSEIDON speichert keine Passwörter, bewahrt nur einen Fingerabdruck des Einladungslinks auf und zeigt IdP-Subjects in der Liste niemals wieder an.

## Wenn etwas schiefgeht
- **This identity is configured by the operator, already a member here or already invited here.** Öffnen Sie stattdessen das bestehende Mitglied oder widerrufen Sie zuerst die alte Einladung.
- **Die eingeladene Person sieht „This invitation cannot be accepted“:** Der Link ist abgelaufen, wurde verwendet, ersetzt oder widerrufen, gehört zu einem anderen Konto, oder ihr Konto gehört bereits zu einer Organisation. Prüfen Sie den Zustand unter **Invitation** und drücken Sie bei Bedarf **Create a new invitation link**.
- **Der Link wurde nicht kopiert:** Markieren Sie das Linkfeld und kopieren Sie ihn manuell.
- **Exceeds your own authority:** Wählen Sie ein engeres Paket oder einen engeren Bereich, oder bitten Sie einen Administrator, der die Berechtigung besitzt.
- **The record changed meanwhile:** Drücken Sie **Reload current state** und nehmen Sie Ihre Änderung erneut vor.
- **The outcome is unknown. Retry with the same request or reload.** Sie können denselben Befehl gefahrlos erneut senden.
- **Not the trusted identity provider:** Prüfen Sie den Aussteller. Stammt das Konto von einem anderen IdP, wenden Sie sich an den Systemadministrator (13.03 §10).

## Verwandte Seiten
