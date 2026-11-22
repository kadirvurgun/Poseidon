---
key: data-trust-center
title: Datenvertrauenszentrum
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Das Datenvertrauenszentrum zeigt **Datenqualitätssignale** für Kontrollen, Nachweise und Schiffsprofilfelder des ausgewählten Schiffs. Es beantwortet: „Welche Datensätze dieses Schiffs fehlen, sind veraltet, widersprüchlich, unverifiziert oder verwaist, und welche Quellen- und Aktualitätsangaben sagen das?“ Ein Vertrauenssignal ist keine Verifizierung des zugrunde liegenden Datensatzes. Die Seite markiert nie etwas als bereit.

## Wer sie verwendet
- **Regulatory-/Assurance-Nutzer:** vermeidet verwaiste oder unvollständige Datensätze in der Kette Anforderung → Kontrolle → Nachweis → Verifizierung (13.03 §8).
- **DPA:** verfolgt Lücken bei Nachweisen und Verifizierungen (13.03 §4).
- **Technischer Inspektor / Kapitän:** sieht die Signale, die eine Bereitschaftsbewertung bei „Prüfung erforderlich“ halten (13.03 §2, §5).

## Bevor Sie beginnen
- Prüfen Sie in der oberen Kontextleiste, dass das **richtige Unternehmen** ausgewählt ist (13.03 §1).
- Die Seite benötigt Lesezugriff auf Kontrollen, Nachweise, Dateien, Anwendbarkeit und regulatorische Mandantenentwürfe, alle zusammen, mit mandantenweitem Lesebereich.
- Ist kein Schiff ausgewählt, zeigt die Seite zuerst die Liste „Schiff auswählen“.

## Schritt für Schritt
1. Wählen Sie das Schiff aus der Liste „Schiff auswählen“.
2. Lesen Sie unter **Zusammenfassung** den Gesamtzustand („Prüfung erforderlich“ oder „Kein wesentliches Signal“) und die Anzahlen unter **Gegenstände je Signal**.
3. Prüfen Sie in der Tabelle **Kontrollen und Nachweise** für jeden Gegenstand die Spalten „Quelle / Herkunft“, „Aktualität“, „Verifizierung“ und „Signale“.
4. Notieren Sie den Grundcode neben einem Signalabzeichen und prüfen Sie den Datensatz auf seiner eigenen Seite.
5. Die Signale der Profilfelder sehen Sie in der Tabelle **Schiffsprofilfelder**.
6. Mit **Zurück** / **Weiter** gelangen Sie zu weiteren Gegenständen.
7. Erscheint „Diese Signale sind möglicherweise veraltet.“, drücken Sie **Neu laden**.

## Was die Zustände bedeuten
- **Missing / Stale / Conflicting / Unverified / Orphan:** die Art des Signals an einem Gegenstand. Ein Signal ist eine Qualitätsmarkierung; es beweist nicht, dass der Datensatz richtig oder falsch ist.
- **Kein Signal:** An diesem Gegenstand wurde kein Signal gefunden. Das bedeutet nicht, dass der Datensatz verifiziert ist.
- **Prüfung erforderlich:** Es gibt ein wesentliches Signal. Die Bereitschaft wird bei „Prüfung erforderlich“ gehalten, solange wesentliche Signale bestehen.
- **Kein wesentliches Signal:** Dies ist keine Bereitschaftsaussage.
- **Von Menschen verifiziert:** Eine berechtigte Person hat den Nachweis verifiziert. Dies stammt aus dem eigenen Datensatz des Nachweises; das Zentrum verifiziert nichts.
- **Not verified / Verification expired / Verification rejected:** der Verifizierungszustand des Nachweises.
- **Für Kontrollen nicht zutreffend:** wird in Kontrollzeilen angezeigt; der Verifizierungszustand gilt für Nachweiszeilen.
- **Observation time unknown / No freshness policy / Future-dated:** Die Aktualität konnte nicht festgestellt werden, oder das Datum liegt in der Zukunft. Keiner dieser Zustände bedeutet „aktuell“.

## Was Sie nicht tun können / Vorsicht
- Datensätze werden auf dieser Seite weder korrigiert noch verifiziert noch geschlossen; das geschieht auf der eigenen Seite des Datensatzes durch eine berechtigte Person.
- Wurden nur die ersten Gegenstände bewertet, bleibt die Zusammenfassung bei „Prüfung erforderlich“.
- Gegenstände, die Sie nicht lesen können, werden gezählt, aber nicht angezeigt, und die Zusammenfassung bleibt dann bei „Prüfung erforderlich“. Ein Datensatz, den Sie nicht sehen, bedeutet nicht, dass es kein Problem gibt.
- Offline können Datenvertrauenssignale nicht aktualisiert werden.

## Wenn etwas schiefgeht
- **Sie sind nicht berechtigt, Datenvertrauenssignale zu lesen:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Datensatz kann nicht verifiziert werden:** Prüfen Sie den Nachweis und seine Herkunft; wenden Sie sich an den berechtigten Verifizierer.
- **Unerwartetes Signal:** Geben Sie den Grundcode an den Kontrollverantwortlichen oder einen Fachexperten weiter.
- **Datenvertrauenssignale sind nicht verfügbar:** Verwenden Sie „Erneut versuchen“; besteht das Problem weiter, melden Sie es dem Support mit einem Screenshot, der Uhrzeit und Ihren Schritten.

## Verwandte Seiten
