---
key: towage-risk-briefing
title: Schlepprisiko und Briefing
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Diese Seite bereitet den **Datensatz zu Schlepprisiko und Briefing** für einen eingereichten Schlepperauftrag vor, gibt ihn aus und sammelt die **Bestätigungen** der Beteiligten. Sie beantwortet: „Welche Bedingungen, Gefährdungen, vorbeugenden und schadensbegrenzenden Barrieren, Stop-Work-Befugnis und Notfallvorkehrungen wurden besprochen, in welchem Zustand ist die verknüpfte Risikobeurteilung, und hat jeder Beteiligte die ausgegebene Revision bestätigt?“ Die Ausgabe des Briefings ist eine menschliche Aufzeichnung des bestellten Kapitäns oder Schlepperkapitäns; sie ist weder eine GO-Entscheidung noch eine Erlaubnis. Ein unvollständiges Briefing blockiert READY.

## Wer sie verwendet
- **Schlepperkapitän / Kapitän:** um das Briefing vorzubereiten und auszugeben und sicherzustellen, dass keine READY-Entscheidung getroffen wird, bevor Risiko und Briefing vollständig sind (13.03 §3).
- **Besatzung und andere Beteiligte (Lotse, Offizier, Decksbesatzung, Ingenieur):** um das ausgegebene Briefing im richtigen Rollenkontext zu bestätigen oder ihm zu widersprechen (13.03 §6).
- **Koordinator Schlepperbetrieb:** um den Zustand von Briefing und Bestätigungen zu verfolgen (13.03 §7).

## Bevor Sie beginnen
- Öffnen Sie den Schlepper über **Schlepperbetrieb** und wählen Sie den Auftrag unter **Eingereichten Schlepperauftrag auswählen**.
- Der **Schleppplan** des Auftrags muss genehmigt und die verknüpfte Risikobeurteilung freigegeben sein; andernfalls kann das Briefing nicht ausgegeben werden.
- Die Ausgabe erfordert eine vom Server erfasste **Bestellung** (Schlepperkapitän oder Kapitän) für den koordinierenden Schlepper. Außerdem erfordert sie Lesezugriff auf die Risikobeurteilung und Schreibzugriff auf den koordinierenden Schlepper. Bestätigen können nur Personen in der Liste der Beteiligten.

## Schritt für Schritt
1. Lesen Sie in der oberen Zeile **Status**, **Revision**, **Bestätigung**, **Risikostatus** und **Planstatus**.
2. Sehen Sie unter **Bereitschaftseingabe (ein unvollständiges Briefing blockiert READY)** die an die Bereitschaftsprüfung gesendeten Ergebnisse und Begründungscodes.
3. Füllen Sie **Beim Briefing bestätigte Bedingungen** (Wetter, Strömung, Sicht), **Gefährdungen**, **Vorbeugende Barrieren**, **Schadensbegrenzende Barrieren**, **Stop-Work-Befugnis** und **Notfallvorkehrungen** aus.
4. Geben Sie die **ID der verknüpften Risikobeurteilung** ein; verwenden Sie unter **Beteiligte** **Hinzufügen**, um für jeden Beteiligten **Personen-ID** und **Rolle** einzugeben.
5. Speichern Sie mit **Entwurf erstellen** oder **Entwurf speichern**; ist **Vor der Übermittlung fehlend** leer, wählen Sie eine **Bestellung** und drücken Sie **Briefing ausgeben**.
6. Lesen Sie als Beteiligter die ausgegebene Revision und drücken Sie **Bestätigen**. Sind Sie nicht einverstanden, schreiben Sie eine **Begründung** von mindestens 10 Zeichen und drücken Sie **Widersprechen**.
7. Muss das Briefing geändert werden, schreiben Sie eine Begründung und verwenden Sie **Ändern**. Die Listen **Bestätigungen** und **Revisionen** zeigen die Historie.

## Was die Zustände bedeuten
- **Draft / Issued:** der Zustand des Briefings. Ausgegeben bedeutet, dass die Bestätigungen beginnen können; für sich allein ist es kein Bereitschaftsergebnis.
- **Not issued / Pending / Complete / Disputed:** der Bestätigungszustand der Beteiligten. Er wird erst dann **Abgeschlossen**, wenn jeder Beteiligte die ausgegebene Revision bestätigt hat.
- **Approved and acknowledged / Attention / Blocked / Review required:** der Zustand der verknüpften Risikobeurteilung.
- **Pass / Fail / Unknown:** das an die Bereitschaftsprüfung gesendete Prüfergebnis. Ein fehlendes oder nicht ausgegebenes Briefing und ausstehende Bestätigungen sind ein **Nicht bestanden**, das READY blockiert.

## Was Sie nicht tun können / Vorsicht
- Es wird keine READY-Entscheidung getroffen, bevor Risiko und Briefing vollständig sind und die Bestätigungen der Beteiligten erfasst wurden (13.03 §3).
- Ein Widerspruch setzt den Bestätigungszustand auf **Widersprochen** und sendet ein Fail an die Bereitschaftsprüfung, sodass READY und GO blockiert sind. Die Begründung erscheint unter **Bestätigungen**. Eine Bestätigung kann für dieselbe Revision nicht geändert werden; um einen Widerspruch aufzulösen, wird das Briefing geändert (neue Revision), erneut ausgegeben und von jedem Beteiligten erneut bestätigt.
- Ändern sich Plan oder Risikobeurteilung nach dem Briefing, muss das Briefing erneut geprüft werden; frühere Bestätigungen gelten nicht für eine neue Revision.
- Niemand bestätigt im Namen eines anderen; jeder Beteiligte erfasst seine eigene Bestätigung (13.03 §6).
- Offline wird von dieser Seite kein Befehl gesendet. Eine Bestätigung kann unter **Schlepper-Offlinebetrieb** in die Warteschlange gestellt werden.

## Wenn etwas schiefgeht
- **Meldung „Required sections are missing.“:** Vervollständigen Sie die fehlenden Codes; prüfen Sie, ob Plan und Risikobeurteilung genehmigt sind.
- **Meldung „The record changed“:** Verwenden Sie **Neu laden**, um das aktuelle Briefing zu prüfen.
- **Meldung „The outcome is unknown“:** **Erneut versuchen** sendet dieselbe Anfrage erneut.
- **Keine Berechtigung:** Prüfen Sie Ihre Rolle, Ihren Schlepper- und Bestellungskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
