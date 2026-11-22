---
key: notification-escalation
title: Benachrichtigungen
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Benachrichtigungen“ listet die **in der App konfigurierten Quellenausnahmen** (Instandhaltung fällig, Instandhaltungsprüfung erforderlich, Instandhaltungsquelle geändert oder nicht verfügbar) für das ausgewählte Schiff oder den ausgewählten Schlepper auf. Sie beantwortet: „Welche Instandhaltungsbenachrichtigungen haben meine Rolle erreicht, wie schwerwiegend sind sie, welche wurden eskaliert, weil niemand sie rechtzeitig zur Kenntnis genommen hat, und auf welchem Quelldatensatz beruhen sie?“ Die Kenntnisnahme verifiziert keine Nachweise und genehmigt keinen Betrieb.

## Wer sie verwendet
- **Leitender Ingenieur / Technischer Inspektor:** verfolgt Fälligkeiten der Instandhaltung und Prüfanfragen und wechselt zum Quellenplan (13.03 §5).
- **Schlepperkapitän / Kapitän / Besatzung:** sehen die an ihre Rollen geleiteten Benachrichtigungen und nehmen sie zur Kenntnis (13.03 §6).

## Bevor Sie beginnen
- Sie benötigen Lesezugriff auf Benachrichtigungen, Instandhaltung und Ausrüstung. Zur Kenntnisnahme und zum Speichern von Einstellungen sind gesonderte Berechtigungen nötig; ohne sie werden die zugehörigen Schaltflächen nicht angezeigt.
- Wählen Sie unter **Benachrichtigungsschiff** ein berechtigtes Schiff oder einen berechtigten Schlepper.
- Offline kann kein Befehl zur Kenntnisnahme oder zu Einstellungen gesendet werden.

## Schritt für Schritt
1. Wählen Sie unter **Benachrichtigungsschiff** ein Fahrzeug.
2. Lesen Sie die Regelzeile: „Konfiguriert · ungeprüfte Quellenregel · Zustellung nur in der App“.
3. Lesen Sie für jede Benachrichtigung den Titel, das Schweregradabzeichen (**Information**, **Warnung**, **Kritisch**) und das Zustandsabzeichen; prüfen Sie **Quelle / Revision**, **Fälligkeitsdatum** und **Erstellt am**. **Weiterleitungsregel** zeigt den Regelcode.
4. Mit **Quellenplan öffnen** gelangen Sie zum zugehörigen Instandhaltungsplan.
5. Um zu erfassen, dass Sie eine Benachrichtigung gesehen haben, drücken Sie **Bestätigen** (nur im Zustand **Offen** oder **Eskaliert**).
6. Wenn Sie Zugriff auf Einstellungen haben, bearbeiten Sie **Nicht verbindliche Hinweisbenachrichtigungen stummschalten** und **Ruhezeiten** (Beginn, Ende, IANA-Zeitzone) und drücken Sie **Hinweiseinstellung speichern**.
7. Mit **Vorherige Benachrichtigungen** / **Weitere Benachrichtigungen** gelangen Sie zu anderen Seiten, mit **Eingang neu laden** lesen Sie erneut.

## Was die Zustände bedeuten
- **Offen:** Die Benachrichtigung wartet auf Kenntnisnahme.
- **Zur Kenntnis genommen:** Eine Person hat erfasst, dass sie sie gesehen hat. Das bedeutet nicht, dass die Instandhaltung ausgeführt oder ein Nachweis verifiziert wurde.
- **Eskaliert:** Die Benachrichtigung wurde nicht rechtzeitig zur Kenntnis genommen und an die Eskalationsrolle ihrer Regel weitergeleitet.
- **Source changed / Source unavailable:** Der zugrunde liegende Datensatz hat sich geändert oder kann nicht bestätigt werden. Die erfasste Grundlage bleibt erhalten; die Benachrichtigung wird dadurch weder aufgehoben noch neu ausgelöst.
- **Verbindlicher Meldeweg · kann nicht stummgeschaltet werden:** Einstellungen können diese Benachrichtigung nicht stummschalten.
- **Unknown (Due date):** Die Quelle hat kein Fälligkeitsdatum. Das bedeutet nicht, dass nichts fällig ist.

## Was Sie nicht tun können / Vorsicht
- Die Kenntnisnahme verifiziert keine Nachweise und genehmigt keinen Betrieb.
- Verbindliche und kritische Meldewege können durch Hinweiseinstellungen nicht stummgeschaltet werden. Ruhezeiten verzögern nur optionale Hinweise.
- Eine leere Seite bedeutet nicht, dass alles in Ordnung ist. Sind die Regel oder die Quellen nicht verfügbar, sagt die Seite das gesondert.
- Es gibt keine automatische Wiederholung; ein unsicherer Befehl bleibt in diesem Tab erhalten, und Kontextwechsel bleiben gesperrt.

## Wenn etwas schiefgeht
- **Meldung „Outcome unknown“:** Verwenden Sie **Exakten Benachrichtigungsbefehl wiederholen**, um den gespeicherten Befehl erneut zu senden.
- **Meldung „Notification version changed“:** Verwenden Sie **Eingang neu laden**, um die aktuelle Liste zu laden.
- **Meldung „Notification sources unavailable or access denied.“:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Meldung „The saved quiet hours could not be applied“:** Alle Hinweise werden angezeigt; prüfen Sie Ihre Ruhezeiteinstellungen.

## Verwandte Seiten
