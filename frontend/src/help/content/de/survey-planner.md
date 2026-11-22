---
key: survey-planner
title: Survey-Planer
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Der Survey-Planer zeigt angegebene Besichtigungsfenster in einem Flottenkalender zusammen mit der Reise- und Hafeneignung. Er beantwortet: „Bei welchen Zeugnissen auf welchen Fahrzeugen öffnet sich im gewählten Zeitraum ein Besichtigungsfenster oder ist bereits verstrichen, und gibt es eine Vorbereitungscheckliste?“ Er ist nur eine Planungshilfe: kein behördlicher Zeitplan, keine Buchung und kein Besichtigungsergebnis (13.03 §8).

## Wer sie verwendet
- **Kapitän, Leitender Ingenieur / Technischer Inspektor:** sehen anstehende Besichtigungen und beginnen die Vorbereitungscheckliste rechtzeitig (13.03 §5).
- **Benutzer Regulatory / Assurance:** beobachtet angegebene Fenster und fehlende Angaben in der gesamten Flotte (13.03 §8).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie Lesezugriff auf Zeugnisse. Es werden nur Fahrzeuge aufgeführt, auf die Sie Zugriff haben.
- Die Fenster stammen aus Angaben im Zeugnisdatensatz. Ist kein Fenster angegeben, erscheint die Zeile unter **Fenster nicht deklariert**; geben Sie die Daten aus dem Quelldokument in der [Zeugniszentrale](/app/certificate-command-center) ein.
- Die Seite benötigt einen aktuellen Online-Abruf. Offline sehen Sie „Offline: Ein aktueller Online-Abruf ist erforderlich.“

## Schritt für Schritt
1. Wählen Sie in **Kalenderzeitraum** **Von** und **Bis** und drücken Sie **Anzeigen**.
2. Lesen Sie in der Übersichtszeile die Anzahl offener, bevorstehender, überfälliger und unbekannter Fenster.
3. Prüfen Sie in jeder Zeile **Zertifikat**, **Aussteller**, **Deklarierte Flagge**, **Rechtsgrundlage**, **Deklariertes Ablaufdatum**, **Besichtigungsfenster** und **Reise-/Hafeneignung**.
4. Drücken Sie **Zertifikat öffnen**, um das Zeugnis zu sehen.
5. Verwenden Sie **Checkliste öffnen**, wenn eine Vorbereitungsliste existiert, oder sonst **Checkliste starten**, um zur [Checkliste vor der Besichtigung](/app/pre-survey-checklist) zu gelangen.

## Was die Zustände bedeuten
- **Bevorstehend / Fenster offen:** die Lage relativ zum angegebenen Fenster.
- **Fenster verstrichen, Erfüllung nicht erfasst:** Das Fenster ist abgelaufen, und kein Datensatz zeigt, dass die Besichtigung erfolgt ist. Es beweist aber auch nicht, dass die Besichtigung versäumt wurde; prüfen Sie die Datensätze.
- **Fenster unbekannt:** Das Datum fehlt oder ist nicht lesbar. Ein unbekanntes Fenster bedeutet nie „kein Problem“; es muss geprüft werden.
- Reise-/Hafeneignung: **Bedingt**, **Nicht geeignet**, **Unbekannt**. Die Seite erteilt nie eine Freigabe „geeignet“.
- Bereitschaft der Checkliste: **Blockiert**, **Unvollständig**, **Vorbereitet, menschliche Bestätigung ausstehend**.

## Was Sie nicht tun können / Vorsicht
- Der Planer bucht nichts und reicht nichts bei einer Behörde ein. Die Abstimmung mit Klasse, Flagge und Behörde erfolgt extern und wird hier nicht simuliert.
- Was der Kalender zeigt, ist weder ein Besichtigungsergebnis noch die Gültigkeit eines Zeugnisses. Reise- und Besichtigungsentscheidungen liegen bei berechtigten Personen.
- Die Seite ist schreibgeschützt; Änderungen werden auf den Seiten für Zeugnisse und Checklisten vorgenommen.

## Wenn etwas schiefgeht
- **Nur ein Teil der Flotte wird angezeigt; Zeitraum eingrenzen.** Wählen Sie einen kürzeren Kalenderzeitraum.
- **Keine Zertifikate mit Besichtigungsfenstern in diesem Zeitraum.** Erweitern Sie den Zeitraum oder prüfen Sie die Angaben in den Zeugnissen.
- **Laden fehlgeschlagen. Bitte erneut versuchen.** Drücken Sie **Erneut versuchen**. Hält das an, melden Sie es dem Support mit Bildschirm, Uhrzeit und Schritten (13.03 §10).
- **Sie haben keinen Zugriff auf diese Ansicht.** Prüfen Sie Ihre Rolle und Ihren Fahrzeugbereich; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).

## Verwandte Seiten
