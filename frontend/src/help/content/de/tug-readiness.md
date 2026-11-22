---
key: tug-readiness
title: Schlepper-Bereitschaftsprüfung
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Bereitschaftsprüfung bewertet die Einsatzbereitschaft einer Schlepperzuweisung anhand von Quellenprüfungen **nur beratend** und lässt den bestellten Schlepperkapitän oder Kapitän eine begründete **GO/NO-GO-Entscheidung** erfassen. Sie beantwortet: „Was sagen die Prüfungen zu Besatzung, Ausrüstung, PMS, Zertifikaten, Kommunikation, Wetter, Pfahlzug-Reserve, Zuweisung und Briefing, und welche Hindernisse gibt es?“ Die Bewertung ist keine Entscheidung: „The system never issues GO; only the appointed Master or Tug Master records a GO/NO-GO decision with a reason.“

## Wer sie verwendet
- **Schlepperkapitän / Kapitän:** um Bewertung und Hindernisse zu prüfen und eine GO- oder NO-GO-Entscheidung zu erfassen (13.03 §3).
- **Koordinator Schlepperbetrieb / Landbetrieb:** um Bereitschaft und Hindernisse zu verfolgen; sie entscheiden nicht (13.03 §7).

## Bevor Sie beginnen
- Öffnen Sie die Seite aus einer Zuweisung: Verwenden Sie unter **Zuweisung und Konflikte** den Link **Einsatzbereitschaftsprüfung** unter einem Vorschlag. Ohne ausgewählte Zuweisung zeigt die Seite **Wählen Sie eine Zuweisung aus der Zuweisungsliste des Auftrags.**
- Zum Erfassen einer Entscheidung benötigen Sie Schreibzugriff auf Schlepper und eine vom Server erfasste **Bestellung** (Schlepperkapitän oder Kapitän) für diesen Schlepper, und die Zuweisung muss noch im Zustand Proposed sein.
- Ohne Briefing gibt es kein READY: **Schlepprisiko und Briefing** muss ausgegeben und von jedem Beteiligten bestätigt sein (13.03 §3).

## Schritt für Schritt
1. Lesen Sie das Zustandskennzeichen in der Zeile **Bewertung**. Der Hinweis daneben lautet **Beratend, keine GO-Entscheidung.**
2. Prüfen Sie **Erforderlicher Pfahlzug (t)**, **Verifizierter verfügbarer Pfahlzug (t)** und **Pfahlzug-Reserve (t)**. Ein fehlender Wert zeigt **Unbekannt**.
3. Prüfen Sie die Codes unter **Kritische Blocker**, **Prüfgründe** und **Bedingungen**.
4. Sehen Sie in der Kategorietabelle das Ergebnis jeder Kategorie (Besatzung, Schleppkompetenz, Ausrüstung, Schleppausrüstung, PMS und Mängel, Zertifikate, Kommunikation, Wetter, Pfahlzug-Reserve, Zuweisung, Schlepprisiko und Briefing). Für Details lesen Sie die Begründungscodes in der Tabelle **Quellenprüfungen**.
5. Haben Sie Entscheidungsbefugnis, wählen Sie im Formular **Kapitänsentscheidung erfassen** **NO-GO** oder **GO**, geben Sie Ihre **Bestellung** und eine **Begründung** ein und drücken Sie **Erfassen**.
6. Sehen Sie die erfassten Entscheidungen in der Liste **Kapitänsentscheidungen**.

## Was die Zustände bedeuten
- **Bereit (READY):** Jede Quellenprüfung ist bestanden (kein Hindernis, kein Unbekannt, keine Bedingung). Dies ist eine Information, keine Entscheidung; die GO-Entscheidung wird weiterhin vom Schlepperkapitän oder Kapitän erfasst.
- **Conditional (CONDITIONAL):** Die Bedingungen unter **Bedingungen** gelten. GO ist deaktiviert, und die Seite erklärt: „This assessment is CONDITIONAL. GO needs a READY assessment: no source defines which conditions would permit GO, so the server refuses it. Resolve the conditions and reassess, or record NO-GO.“
- **Not ready (NOT_READY):** Es gibt mindestens einen kritischen Blocker.
- **Review required (REVIEW_REQUIRED):** Einige Quellen sind unbekannt oder warten auf Prüfung. Eine unbekannte Prüfung wird niemals als **Bereit** gezählt.
- **Pass / Fail / Unknown:** das Ergebnis einer einzelnen Kategorie oder Prüfung.
- **GO / NO-GO:** die vom Schlepperkapitän oder Kapitän erfasste menschliche Entscheidung und Begründung.

## Was Sie nicht tun können / Vorsicht
- GO kann nur bei einer READY-Bewertung erfasst werden; der Server lehnt GO bei CONDITIONAL, NOT_READY oder REVIEW_REQUIRED ab. Die Seite aktiviert GO nur bei einer READY-Bewertung; andernfalls zeigt sie **GO kann nur bei einer Bewertung BEREIT erfasst werden.** NO-GO kann immer mit einer Begründung erfasst werden.
- Solange Risiko und Briefing unvollständig sind oder Bestätigungen der Beteiligten fehlen, gibt es kein READY; die Option GO ist dann deaktiviert (13.03 §3).
- Ein Zuweisungsvorschlag eines Landbenutzers setzt niemals die Sicherheitsentscheidung des Schlepperkapitäns an Bord außer Kraft (13.03 §7).
- Ändert sich die Bewertung vor Ihrer Entscheidung, wird die Entscheidung abgelehnt; prüfen Sie die aktuelle Bewertung erneut.
- Offline sind Befehle deaktiviert.

## Wenn etwas schiefgeht
- **Meldung „The assessment changed“:** Verwenden Sie **Neu laden**, prüfen Sie die aktuelle Bewertung und entscheiden Sie erneut.
- **Meldung „The outcome is uncertain“:** **Erneut versuchen** sendet denselben Befehl.
- **Unerwartete Bereitschaft:** Öffnen Sie die Kategorietabelle und die Gründe der Quellenprüfungen; prüfen Sie sie mit dem Kontrollverantwortlichen oder einem Fachexperten (13.03 §10).
- **Keine Berechtigung:** Prüfen Sie Ihre Rolle, Ihren Schlepper- und Bestellungskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
