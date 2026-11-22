---
key: audit
title: Aktionsverlauf
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: [1, 2, 3]
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Aktionsverlauf“ zeigt eine **schreibgeschützte**, zugeordnete Aufzeichnung der Änderungen an Geschäftsdatensätzen in Ihrem berechtigten Bereich. Sie beantwortet: „Wann hat sich dieser Datensatz geändert, mit welcher Aktion und unter welchem Akteur- und Rollenkontext?“ Sicherheitsereignisse sind gesondert beschränkt und erscheinen hier nicht (13.03 §4, §9).

## Wer sie verwendet
- **DPA / HSEQ:** verfolgt Datensätze zu Feststellungen, Maßnahmen und Verifizierungen (13.03 §4).
- **Regulatory-/Assurance-Nutzer:** prüft den Verlauf eines Datensatzes vor einem Audit oder einer Prüfung (13.03 §8).
- **Mandantenadministrator:** prüft Änderungen an Mitgliedschaften und Berechtigungen sowie Beginn und Ende von Vertretungen (13.03 §9).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie Audit-Lesezugriff. Ohne ihn sehen Sie „Auditberechtigung erforderlich.“
- Es werden nur Datensätze in dem Mandanten- und Schiffsbereich aufgeführt, für den Sie berechtigt sind.
- Suchen Sie einen bestimmten Datensatz, halten Sie dessen **Schiffs-ID** oder **Objekt-ID** bereit. Verlaufslinks auf anderen Seiten öffnen diese Seite bereits gefiltert.

## Schritt für Schritt
1. Füllen Sie die Filter aus: **Schiffs-ID**, **Objekt-ID**, **Objekttyp** und **Aktion** (**Alle**, **Erstellt**, **Aktualisiert**, **Gelöscht**).
2. Drücken Sie **Filtern**. Mit **Filter zurücksetzen** setzen Sie die Filter zurück.
3. Prüfen Sie unter **Autorisierte Datensätze** die Spalten **Zeit**, **Objekt** und **Aktion**. Mit **Erste Seite** und **Weiter** wechseln Sie zwischen den Seiten.
4. Drücken Sie den Objektlink in einer Zeile, um **Datensatzdetails** zu öffnen. Lesen Sie **Akteurreferenz**, **Referenzen zu zugewiesenen Rollen**, **Vertrauenswürdige Befugnisse**, **Begründung**, **Metadaten**, **Ressourcenversion** und **Korrelation**.
5. Wenn ein unveränderlicher Revisionsnachweis vorhanden ist, drücken Sie **Genaue unveränderliche Revision öffnen**, um den Datensatz in seinem damaligen Zustand zu sehen. Mit **Schließen** schließen Sie die Details.

## Was die Zustände bedeuten
- **Erstellt / Aktualisiert / Gelöscht:** die Art der Aktion am Datensatz.
- **Erfasst:** Die Metadaten wurden vollständig erfasst.
- **Historical metadata incomplete / Historical identifier unavailable / Historical context unavailable:** Ältere Datensätze haben diese Information nicht gespeichert. Fehlende Informationen werden nie durch Raten ergänzt.
- **Werte zurückgehalten:** Der Feldwert ist aus Datenschutzgründen verborgen; angezeigt wird nur, dass er sich geändert hat.
- **Unveränderliche Revisionsnachweise sind für diesen Datensatz nicht verfügbar.** Für diesen Datensatztyp gibt es keine Revisionsmomentaufnahme.

## Was Sie nicht tun können / Vorsicht
- Die Seite ist schreibgeschützt. Ein Eintrag im Aktionsverlauf kann nicht bearbeitet oder gelöscht werden.
- Dass ein Datensatz im Verlauf erscheint, bedeutet nicht, dass die Arbeit verifiziert oder wirksam war; Verifiziert ≠ Wirksam (13.03 §4).
- Diese Seite erfasst Geschäftsänderungen. Für sich genommen ist sie weder eine vollständige Prüfung von Benutzeraktionen noch ein Nachweis der Datenschutzkonformität; Sicherheitsereignisse werden gesondert gespeichert.
- Die Liste zeigt nur den Bereich, für den Sie berechtigt sind. Datensätze außerhalb Ihres Bereichs werden nicht aufgeführt; dass Sie einen Datensatz nicht sehen, bedeutet nicht, dass er nie geändert wurde.

## Wenn etwas schiefgeht
- **Auditberechtigung erforderlich.** Prüfen Sie Ihre Rolle und Ihren Bereich; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).
- **Melden Sie sich an, um fortzufahren.** Melden Sie sich erneut an.
- **Verlauf nicht verfügbar. Prüfen Sie die Filter und versuchen Sie es erneut.** Prüfen Sie, dass die Kennungen vollständig und korrekt sind, und drücken Sie dann **Erneut versuchen**.
- **Keine autorisierten Datensätze entsprechen diesen Filtern.** Erweitern Sie die Filter.
- Besteht ein Anwendungsfehler weiter, melden Sie ihn dem Support mit der Korrelations-ID, dem Bildschirm, der Uhrzeit und Ihren Schritten (13.03 §10).

## Verwandte Seiten
