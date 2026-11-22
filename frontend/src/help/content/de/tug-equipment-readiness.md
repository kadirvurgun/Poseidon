---
key: tug-equipment-readiness
title: Bereitschaft der Schleppausrüstung
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Diese Seite zeigt die **Erklärungen zur Schleppausrüstung** des ausgewählten Schleppers (Schleppwinde, Schlepphaken, Schleppleine, Notauslösung, Feuerlöschausrüstung FiFi) und die zugehörigen **Prüfungen des Leitenden Ingenieurs**. Sie beantwortet: „Wie lauten SWL, Prüfdaten, Zustand und Kritikalität jedes Teils, wer hat es verifiziert und was sagt die Bereitschaftsprüfung?“ Die Bewertung ist nur ein Hinweis und fließt in die Bereitschaftsprüfung ein; sie ist keine GO-Entscheidung.

## Wer sie verwendet
- **Schlepperkapitän:** um Winde, Schleppleine, Schlepphaken / Kettenstopper, Notauslösung, FiFi und Nachweise zu kritischen Mängeln zu prüfen (13.03 §3).
- **Leitender Ingenieur:** um Erklärungen zu verifizieren oder nicht zu verifizieren und die Auswirkung eines kritischen Mangels auf die Bereitschaft zu erfassen (13.03 §5).

## Bevor Sie beginnen
- Öffnen Sie den Schlepper über **Schlepperbetrieb**; dieser Bildschirm benötigt keine Zuweisung.
- Zum Hinzufügen oder Bearbeiten einer Erklärung benötigen Sie Schreibzugriff auf Schlepper und Ausrüstung.
- Zum Erfassen einer Verifizierung ist eine vom Server erfasste Bestellung als **Leitender Ingenieur** für diesen Schlepper erforderlich.

## Schritt für Schritt
1. Lesen Sie das Gesamtkennzeichen in der Zeile **Bewertung**.
2. Prüfen Sie auf jeder Ausrüstungskarte das Ergebniskennzeichen, den Begründungscode, **SWL (t)**, **Nächste Prüfung fällig**, **Zustand** und **Kritisch**.
3. Drücken Sie für eine neue Erklärung **Ausrüstung hinzufügen**; füllen Sie **Ausrüstungsart**, **Bezeichnung**, **SWL (t)**, **Letzte Prüfung**, **Nächste Prüfung fällig**, **Zustand**, **Kritisch**, **PMS-Anlagen-ID**, **Zertifikats-ID** und **Notizen** aus und drücken Sie dann **Erklärung speichern**.
4. Um eine bestehende Erklärung zu ändern, verwenden Sie **Bearbeiten** auf ihrer Karte.
5. Wählen Sie als Leitender Ingenieur Ihre **Bestellung**, schreiben Sie eine Begründung und verwenden Sie **Verifiziert** oder **Nicht verifiziert**, um die Option **Verifizierung erfassen** auszuführen. Für „Nicht verifiziert“ ist eine Begründung erforderlich.
6. Sehen Sie die Verifizierungshistorie mit Revision und Uhrzeit unter **Prüfungen des Leitenden Ingenieurs** und unten die Tabelle **Quellenprüfungen**.

## Was die Zustände bedeuten
- **Ready / Conditional / Not ready / Review required:** die beratende Bewertung. Bereit ist keine GO-Entscheidung.
- **Pass / Fail / Unknown:** das Prüfergebnis für ein Teil.
- **Serviceable / Degraded / Failed / Unknown:** der angegebene Zustand. Wird kein Zustand gewählt, bleibt er „Unbekannt“; er wird nicht als einsatzfähig gezählt.
- **Nicht bewertet:** Die Kritikalität wurde nicht bewertet. Das bedeutet nicht „Nein“.
- **Verifiziert / Nicht verifiziert:** die menschliche Aufzeichnung des Leitenden Ingenieurs.
- **durch spätere Revision ersetzt:** Die Verifizierung gehörte zu einer älteren Revision der Erklärung; die aktuelle Revision benötigt eine neue Verifizierung.

## Was Sie nicht tun können / Vorsicht
- Speichern erfasst eine nicht verifizierte Erklärung. Jede Änderung erfordert eine neue Verifizierung durch den Leitenden Ingenieur.
- Einen Datensatz abzuschließen und ihn zu verifizieren ist nicht dieselbe Handlung; die Verifizierung wartet auf die berechtigte Person (13.03 §1).
- Die Ausrüstungsart kann nach dem Speichern nicht geändert werden; fügen Sie für eine andere Art eine neue Erklärung hinzu.
- Die Seite erklärt Schleppausrüstung niemals für sicher; die Entscheidung bleibt beim Schlepperkapitän (13.03 §3).
- Offline sind Befehle deaktiviert.

## Wenn etwas schiefgeht
- **Meldung „The record changed“:** Verwenden Sie **Neu laden**, um die aktuelle Erklärung zu prüfen.
- **Meldung „The outcome is uncertain“:** **Erneut versuchen** sendet denselben Befehl.
- **Datensatz kann nicht verifiziert werden:** Prüfen Sie den Nachweis und die PMS-/Zertifikatsquelle; wenden Sie sich an den Verifizierer (13.03 §10).
- **Keine Berechtigung:** Prüfen Sie Ihre Rolle, Ihren Schlepper- und Bestellungskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
