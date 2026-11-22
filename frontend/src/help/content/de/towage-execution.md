---
key: towage-execution
title: Schleppeinsatz-Durchführung
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Durchführungskonsole erfasst die **tatsächliche Durchführung** eines eingereichten Schlepperauftrags als Zeitverlauf: den Start, Beobachtungen, die Aktionen **Unterbrechen / Fortsetzen / Abbrechen / Abschließen** und die spätere Verifizierung. Sie beantwortet: „Wann hat der Einsatz begonnen, wann wurde die Schleppleine verbunden und gelöst, welche Beobachtungen wurden erfasst, in welchem Zustand ist der Einsatz und wurde der Datensatz verifiziert?“ Start und Übergänge sind menschliche Aufzeichnungen des Schlepperkapitäns oder Kapitäns auf dem ausführenden Schlepper; die Konsole erteilt niemals ein GO.

## Wer sie verwendet
- **Schlepperkapitän / Kapitän:** um den Einsatz zu starten und den Zeitverlauf Start/Unterbrechen/Fortsetzen/Abbrechen/Abschließen sowie dessen Nachweise zu erfassen (13.03 §3).
- **Verifizierer (eine Person mit Befugnis zur Verifizierung der Durchführung):** um einen abgeschlossenen oder abgebrochenen Datensatz zu verifizieren oder seine Verifizierung abzulehnen (13.03 §1).
- **Koordinator Schlepperbetrieb / Landbetrieb:** um die Vollständigkeit der Nachweise und die Historie abgeschlossener Aufträge zu verfolgen; sie können einen Einsatz weder starten noch abbrechen (13.03 §7).

## Bevor Sie beginnen
- Öffnen Sie den Schlepper über **Schlepperbetrieb** und wählen Sie den Auftrag unter **Eingereichten Schlepperauftrag auswählen**.
- Für den Start muss all dies aktuell sein: die aktive Zuweisung des Schleppers, ein genehmigter **Schleppplan** (der Schlepper muss im Plan enthalten sein), ein ausgegebenes **Schlepprisiko und Briefing**, das von jedem Beteiligten bestätigt wurde, und ein **GO**-Eintrag in der **Einsatzbereitschaftsprüfung**, der an die aktuelle Bewertung gebunden ist.
- Start und Übergänge erfordern eine vom Server erfasste **Bestellung** (Schlepperkapitän oder Kapitän) für diesen Schlepper.

## Schritt für Schritt
1. Lesen Sie im Abschnitt **Startfreigabe-Prüfung** für jeden Schlepper die **Bereitschaftsentscheidung** und die **Hindernisse**.
2. Gibt es keine Hindernisse, lesen Sie als bestellte Person an Bord den Bestätigungstext, wählen Sie Ihre **Bestellung**, geben Sie optional eine **Startposition** ein und drücken Sie **Einsatz starten**.
3. Sobald die Durchführung offen ist, verfolgen Sie **Status**, **Verifizierung** und die Felder des Protokolls (Schleppleine verbunden/gelöst, maximal beobachtete Schlepplast, Kommunikation, Winde, Test der Notauslösung, aktive Minuten).
4. Wählen Sie unter **Beobachtung erfassen** den Beobachtungstyp (zum Beispiel Schleppleine verbunden, Schlepplast, Kommunikationsprüfung, Wetter, Windenzustand, Test der Notauslösung, Position, Nachweis, Notiz), geben Sie die Werte ein und drücken Sie **Beobachtung erfassen**.
5. Wählen Sie unter **Aktionen des Schlepperkapitäns** die **Aktion** (Unterbrechen, Fortsetzen, Abbrechen, Abschließen) und geben Sie Ihre **Bestellung** und eine **Begründung** ein. Abbrechen und Abschließen erfordern eine Begründung von mindestens 20 Zeichen und öffnen das Feld **Endposition**; ein Abschluss wird ohne Endposition nicht angenommen.
6. Werden **Abschlusshindernisse** aufgeführt, beseitigen Sie sie; ist zum Beispiel die Schleppleine noch verbunden, erfassen Sie zuerst deren Lösen.
7. Schreiben Sie als Verifizierer eine Begründung und drücken Sie **Datensatz verifizieren** oder **Verifizierung ablehnen**.
8. Sehen Sie alle Ereignisse der Reihe nach im **Zeitverlauf**.

## Was die Zustände bedeuten
- **Active / Suspended / Completed / Aborted:** der Zustand des Einsatzes. Abgeschlossen bedeutet nicht, dass der Datensatz verifiziert ist.
- **Pending / Verified / Rejected:** der Verifizierungszustand. Abgeschlossen ≠ Verifiziert; die Verifizierung ist die Aufzeichnung einer anderen Person.
- **Good / Lost / Pass / Fail / Conditional / Unknown:** das Ergebnis der Beobachtungen zu Kommunikation, Winde und Notauslösung. Unknown zeigt, dass nichts erfasst wurde; es steht niemals für **Gut** oder **Bestanden**.
- **Safety events — Unknown:** Diese Konsole zählt keine Sicherheitsereignisse; es wird kein Schluss „keine Ereignisse“ gezogen.
- **GO (human record) / NO GO (human record):** die vom Schlepperkapitän oder Kapitän in der Bereitschaftsprüfung erfasste Entscheidung.

## Was Sie nicht tun können / Vorsicht
- Landbenutzer, KI und Systemidentitäten können einen Einsatz weder starten noch abbrechen. Eine Zuweisung an Land setzt niemals die Sicherheitsentscheidung des Schlepperkapitäns an Bord außer Kraft (13.03 §7).
- Nur der bestellte Schlepperkapitän oder Kapitän darf unterbrechen, fortsetzen, abbrechen oder abschließen. Abbrechen ist immer verfügbar, solange der Einsatz läuft.
- Die Person, die den Einsatz gestartet oder beendet hat, kann denselben Datensatz nicht verifizieren; die Verifizierung erfolgt, nachdem der Einsatz beendet ist.
- Das Protokoll ist nur fortschreibbar; erfasste Ereignisse werden niemals gelöscht oder geändert.
- Offline wird kein Befehl gesendet. Beobachtungen können unter **Schlepper-Offlinebetrieb** in die Warteschlange gestellt werden; Start, Übergänge und Verifizierung bleiben online.

## Wenn etwas schiefgeht
- **Meldung „The server listed missing or blocking items.“:** Beseitigen Sie die Codes unter **Hindernisse** oder **Abschlusshindernisse** (zum Beispiel Plan nicht genehmigt, Bestätigung des Briefings ausstehend, GO-Eintrag fehlt oder ist nicht aktuell).
- **Meldung „The record changed or the command conflicts.“:** Verwenden Sie **Neuesten Stand laden**, um den Datensatz zu prüfen.
- **Meldung „The outcome is unknown.“:** **Erneut versuchen** sendet denselben Befehl erneut.
- **Keine Berechtigung:** Prüfen Sie Ihre Rolle, Ihren Schlepper- und Bestellungskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
