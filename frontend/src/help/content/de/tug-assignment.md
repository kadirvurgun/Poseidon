---
key: tug-assignment
title: Schlepperzuweisung
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Diese Seite zeigt die **Zuweisungsvorschläge für Schlepper** zu einem eingereichten Schlepperauftrag und ihre **Konflikte** mit anderen Aufträgen. Sie beantwortet: „Welcher Schlepper wurde für diesen Auftrag mit welcher Rolle und welchem Zeitfenster vorgeschlagen, kollidiert er mit anderer Arbeit, und hat der Schlepperkapitän den Vorschlag angenommen?“ Land schlägt vor, der Schlepperkapitän bestätigt; die Seite trifft keine Entsendungs- oder Freigabeentscheidung. Der Hinweis oben sagt es: „The system never issues GO.“

## Wer sie verwendet
- **Koordinator Schlepperbetrieb / Landbetrieb:** um nach Prüfung von Konflikten und Bereitschaft in der Auftragsübersicht einen Zuweisungsvorschlag zu machen (13.03 §7).
- **Schlepperkapitän:** um einen Vorschlag für den eigenen Schlepper im Kontext zu bestätigen und ihn anzunehmen oder abzulehnen (13.03 §3).

## Bevor Sie beginnen
- Öffnen Sie den Schlepper über **Schlepperbetrieb**; ist kein Schlepper ausgewählt, verweist die Seite Sie dorthin.
- Für einen Vorschlag benötigen Sie Schreibzugriff für Schlepper und Plan; der Auftrag muss **Eingereicht** sein.
- Für eine Antwort ist eine vom Server erfasste **Bestellung** (Schlepperkapitän oder Kapitän) für diesen Schlepper erforderlich. Die Person, die einen Vorschlag gemacht hat, kann nicht darauf antworten.

## Schritt für Schritt
1. Öffnen Sie die Auftragsnummer unter **Eingereichten Schlepperauftrag auswählen**. Auch der Link **Zuweisungen und Bereitschaft** in den Auftragsdetails öffnet diese Seite.
2. Lesen Sie bei bestehenden Vorschlägen **Status**, **Bestätigung des Schlepperkapitäns**, **Rolle**, **Geplanter Beginn** und **Geplantes Ende**.
3. Sehen Sie unter **Konflikte** die Zeit- und Ressourcenkonflikte mit anderen Aufträgen, ihr Ergebniskennzeichen und ihren Begründungscode.
4. Wählen Sie für einen neuen Vorschlag im Formular **Zuweisung vorschlagen** **Schlepper**, **Geplanter Beginn**, **Geplantes Ende** und **Rolle** (Lead, Support, Escort) und drücken Sie **Zuweisung vorschlagen**.
5. Wählen Sie als Schlepperkapitän Ihre **Bestellung**, fügen Sie bei Bedarf eine **Begründung** hinzu und drücken Sie **Annehmen** oder **Ablehnen**. Zum Ablehnen ist eine Begründung erforderlich.
6. Um einen Vorschlag zu entfernen, geben Sie eine Begründung ein und drücken Sie **Vorschlag zurückziehen**.
7. Verwenden Sie die Links **Einsatzbereitschaftsprüfung** und **Besatzung und Schleppkompetenz** unter jedem Vorschlag, um zu den Bereitschaftsbildschirmen zu wechseln.

## Was die Zustände bedeuten
- **Proposed / Withdrawn:** der Lebenszyklus des Vorschlags. Vorgeschlagen bedeutet nicht, dass der Schlepper entsandt wurde.
- **Pending / Accepted / Rejected:** die Antwort des Schlepperkapitäns auf den Vorschlag. Angenommen bestätigt den Umfang des Vorschlags; es ist keine GO-Entscheidung.
- **Pass / Fail / Unknown:** das Ergebnis einer Konfliktprüfung. Unknown zeigt, dass für die Prüfung nicht genügend Quelldaten vorlagen; es bedeutet niemals „kein Konflikt“.
- **Kein Konflikt festgestellt; Eingaben können unvollständig sein.:** Es wurde kein Konflikt festgestellt, dies ist jedoch kein vollständiges Eignungsergebnis.
- **Die Anfrage hat sich nach diesem Vorschlag geändert; ein neuer Vorschlag ist erforderlich.:** Die Auftragsanfrage hat sich nach der Revision geändert, auf der der Vorschlag beruhte.

## Was Sie nicht tun können / Vorsicht
- Ein Zuweisungsvorschlag von Land setzt niemals die Sicherheitsentscheidung des Schlepperkapitäns an Bord außer Kraft (13.03 §3, §7).
- Der Vorschlagende kann seinen eigenen Vorschlag nicht annehmen; die Antwort steht nur der für diesen Schlepper bestellten Person zu.
- Für einen Auftrag im Entwurf oder einen stornierten Auftrag kann kein Vorschlag gemacht werden.
- Offline sind Befehle deaktiviert.

## Wenn etwas schiefgeht
- **Meldung „The outcome is uncertain“:** **Erneut versuchen** sendet denselben Befehl; legen Sie keinen neuen Vorschlag an.
- **Meldung „The record changed“:** Verwenden Sie **Neu laden**, um den aktuellen Datensatz zu prüfen, und handeln Sie dann erneut.
- **„You are not authorized for this action.“ / „You do not have access to this record.“:** Prüfen Sie Ihre Rolle, Ihren Schlepper- und Bestellungskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Unerwarteter Konflikt:** Notieren Sie den Begründungscode und prüfen Sie ihn mit dem Verantwortlichen des anderen Auftrags.

## Verwandte Seiten
