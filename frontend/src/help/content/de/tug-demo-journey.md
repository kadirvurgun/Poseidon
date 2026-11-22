---
key: tug-demo-journey
title: Schlepper-Demo-Ablauf
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [tug-job-intake, tug-assignment, tug-readiness, towage-plan, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Der Demo-Ablauf ist ein geführter Rundgang durch die echten Schlepperbildschirme mit **synthetischen, nicht unterschriebenen und nicht verifizierten Daten**. Er beantwortet: „Welche Bildschirme durchläuft ein Schlepperauftrag von der Anfrage bis zur Historie, und welche Prüfung erzwingt der Server bei jedem Schritt?“ Die Demo erzeugt keine Entscheidung, Freigabe oder Verifizierung; sie verlinkt nur auf die echten Bildschirme und erklärt sie.

## Wer sie verwendet
- **Koordinator Schlepperbetrieb / Landbetrieb:** um den Arbeitsablauf und die Prüfung bei jedem Schritt kennenzulernen (13.03 §7).
- **Schlepperkapitän:** um zu sehen, wo die Entscheidungsschritte an Bord (Bereitschafts-GO, Durchführung) im gesamten Ablauf liegen (13.03 §3).

## Bevor Sie beginnen
- Wählen Sie den Schlepper; ohne ausgewählten Schlepper zeigt die Seite **Wählen Sie einen Schlepper, um fortzufahren.**
- Öffnen Sie die Seite mit einem Auftrag, führen die Schritt-Links diesen Auftrag mit.
- Jeder Link öffnet den echten Bildschirm; dort gelten dessen eigene Berechtigungsregeln.

## Schritt für Schritt
1. Folgen Sie den Schritten der Reihe nach: **Auftragserfassung**, **Schlepperzuweisung**, **Bereitschaft von Pfahlzug, Besatzung und Ausrüstung**, **Schleppplan**, **Risikobewertung und Briefing**, **Master release (GO als menschlicher Datensatz)**, **Ausführung**, **Nachweis und Verifizierung**, **Operationshistorie**, **Offline-Kontinuität**.
2. Verwenden Sie bei jedem Schritt **Ansicht öffnen**, um zum echten Bildschirm zu wechseln.
3. Öffnen Sie **Warum**, um die Prüfung zu lesen, die der Server bei diesem Schritt erzwingt; zum Beispiel wird der Plan von einem Schlepperkapitän oder Kapitän genehmigt, der ihn nicht verfasst hat, und jeder Beteiligte bestätigt genau die ausgegebene Revision.

## Was die Zustände bedeuten
- Diese Seite hat kein eigenes Zustandskennzeichen. Die Bedeutung der Zustände auf den geöffneten Bildschirmen ist im Leitfaden des jeweiligen Bildschirms beschrieben.
- Wie der Bereitschaftsschritt erklärt, bleibt UNKNOWN REVIEW_REQUIRED, niemals READY.
- GO ist eine menschliche Aufzeichnung, die an die aktuelle Bereitschaftsbewertung gebunden ist; das System erzeugt sie niemals.

## Was Sie nicht tun können / Vorsicht
- Demodaten sind fiktiv und nicht verifiziert. Freigaben, GO-Entscheidungen und Verifizierungen werden immer von berechtigten Personen erfasst, niemals von der Demo.
- Ein Ergebnis auf den Demo-Bildschirmen ersetzt niemals eine Sicherheitsentscheidung für einen echten Einsatz; die Entscheidung bleibt beim Schlepperkapitän (13.03 §3).
- Eine Zuweisung von Land setzt niemals die Sicherheitsentscheidung des Schlepperkapitäns an Bord außer Kraft (13.03 §7).

## Wenn etwas schiefgeht
- **Meldung „This page address is not valid.“:** Öffnen Sie die Seite erneut über das Menü.
- **Der Bildschirm eines Schritts öffnet sich nicht oder der Zugriff fehlt:** Prüfen Sie Ihre Rolle und Ihren Schlepperkontext für diesen Bildschirm; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Unerwartetes Bereitschaftsergebnis:** Öffnen Sie die Warum-Ansicht des betreffenden Bildschirms und prüfen Sie sie mit dem Kontrollverantwortlichen (13.03 §10).

## Verwandte Seiten
