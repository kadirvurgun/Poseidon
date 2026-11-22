---
key: high-risk-work
title: Arbeiten mit hohem Risiko
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Arbeiten mit hohem Risiko“ zeigt Ausnahmen bei Flottenarbeiten wie Heißarbeiten, Arbeiten in geschlossenen Räumen, Arbeiten in der Höhe, elektrischer Freischaltung, kritischer Instandhaltung, Schleppen und Bunkern in einer **schreibgeschützten** Liste. Sie beantwortet: „Bei welcher Arbeit mit hohem Risiko gibt es ein offenes Problem bei Risiko, Arbeitserlaubnis, kritischer Barriere, Besatzung oder Ausrüstung, und wer ist für die nächste Maßnahme zuständig?“ Jede Dimension wird getrennt angezeigt; es gibt keine Gesamtpunktzahl. Aktionen öffnen den zuständigen Datensatz; diese Seite ändert keinen Datensatz und trifft keine Entscheidung über den Arbeitsbeginn (13.03 §4).

## Wer sie verwendet
- **DPA / HSEQ:** beobachtet im Rahmen der Risikosteuerung Ausnahmen bei Arbeiten mit hohem Risiko in der gesamten Flotte (13.03 §4).
- **Kapitän und Schlepperkapitän:** sehen die Hindernisse für Arbeiten mit hohem Risiko auf ihrem eigenen Fahrzeug und gehen zum zuständigen Datensatz. Die Entscheidung, Arbeiten zu beginnen oder zu stoppen, bleibt bei ihnen (13.03 §2, §3).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie die Berechtigungen zum Lesen von Risiken und von Arbeiten mit hohem Risiko. Ohne sie sehen Sie „Sie haben keinen Zugriff auf die Ansicht für Arbeiten mit hohem Risiko.“
- Die Liste umfasst nur die Flotten und Fahrzeuge in Ihrem Zugriffsbereich.
- Offline ist die angezeigte Liste möglicherweise veraltet.

## Schritt für Schritt
1. Wählen Sie die Filter **Flotte**, **Schiff** und **Arbeitsart**. **Alle** entfernt einen Filter.
2. Lesen Sie in der Tabelle für jede Arbeit die Zustände **Risiko**, **Arbeitserlaubnis**, **Kritische Barriere**, **Besatzung** und **Ausrüstung** getrennt. Die Abzeichen **Kritische Blocker** und **Prüfpunkte** zeigen Anzahlen.
3. Anhand der Spalten **Nächste Maßnahme** und **Verantwortlich / fällig** sehen Sie, wer was tun muss.
4. Um zu sehen, warum eine Zeile in ihrem Zustand ist, drücken Sie **Warum**. Eine Tabelle mit **Dimension**, **Status** und **Begründung** öffnet sich; schließen Sie sie mit **Ausblenden**.
5. Um etwas zu ändern, folgen Sie dem Link der Arbeit oder **Datensatz öffnen**; die Aktion erfolgt auf der eigenen Seite des zuständigen Datensatzes.
6. Blättern Sie mit **Zurück** und **Weiter**. **Bewertet am** und **Quellenabdeckung** zeigen, wann und aus welchen Quellen die Liste berechnet wurde.

## Was die Zustände bedeuten
- **Ready / Conditional / Attention / Not ready / Blocked:** der Zustand der Dimension in ihrer Quelle. Eine Dimension im Zustand Bereit bedeutet nicht, dass die Arbeit beginnen kann; die Entscheidung bleibt bei der berechtigten Person.
- **Prüfung erforderlich:** Eine Person muss es prüfen.
- **Unbekannt:** Die Quelle ist nicht verfügbar; das ist nie bereit. POSEIDON hat noch keine Quelle für Arbeitserlaubnisse, daher zeigt die Spalte **Arbeitserlaubnis** immer **Unbekannt**.
- **Healthy / Degraded / Failed:** der Zustand der kritischen Barriere.
- **Nicht klassifiziert:** Die Arbeitsart ist nicht bekannt oder ihre Kritikalität ist nicht bestimmt; sie wird nicht aus der Liste ausgelassen.
- **Waiting for source / Resolve stop or dispute / Renew expired item / Complete required item / Reassess / Open owner record:** die nächste Maßnahme, angezeigt in einer festen Prioritätsreihenfolge.
- **Due unresolved / Not planned:** Das Fälligkeitsdatum oder der geplante Beginn ist nicht bekannt.

## Was Sie nicht tun können / Vorsicht
- Diese Seite ist schreibgeschützt; sie ändert, genehmigt oder erlaubt nichts.
- Es gibt keine Gesamtpunktzahl; eine Arbeit mit einem kritischen Blocker wird immer nach oben sortiert, und dass andere Dimensionen in Ordnung sind, verdeckt ihn nie.
- Lesen Sie **Unbekannt** nicht als bereit. Eine frühere aktive Arbeitserlaubnis ist nie eine aktuell gültige Arbeitserlaubnis.
- Die Entscheidung, Arbeiten zu beginnen oder zu stoppen, bleibt beim Kapitän oder Schlepperkapitän.

## Wenn etwas schiefgeht
- **Der Dienst ist nicht verfügbar. Versuchen Sie es erneut.** Drücken Sie **Erneut versuchen**.
- **Sie sind offline. Die angezeigte Liste ist möglicherweise veraltet.** Drücken Sie online **Neu laden**.
- Wirkt ein Zustand unerwartet, öffnen Sie die Tabelle **Warum** und fragen Sie den Verantwortlichen des Datensatzes oder den Kontrollverantwortlichen (13.03 §10).
- Ohne Zugriff prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).

## Verwandte Seiten
