---
key: risk-template-library
title: Risikovorlagen-Bibliothek
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Risikovorlagen-Bibliothek verwaltet mit Revisionen die im gesamten Mandanten verwendeten Vorlagen für Risikobeurteilungen (Arbeitsart, Gefährdungen, Standardmaßnahmen, erforderliche Barrieren, SMS- und Anforderungsquellen) sowie die Risikomethoden (Matrix). Sie beantwortet: „Welche veröffentlichte Vorlage gilt für diese Arbeitsart, und welche Revision ist in Kraft?“ Die Seite ist **nur ein Entwicklungsziel**: Eine Veröffentlichung hier ist ein erfasster menschlicher HSEQ-Nachweis, keine Arbeitserlaubnis, keine fachliche Risikoakzeptanz und kein bestandenes HSE-Gate (13.03 §4).

## Wer sie verwendet
- **DPA / HSEQ:** bereitet im Rahmen der Risikosteuerung Vorlagenrevisionen, Barrieren und die Risikomatrix vor, prüft und veröffentlicht sie (13.03 §4).
- **Kapitän, Schlepperkapitän und Crew:** lesen veröffentlichte Vorlagen und senden **Vorschläge der Besatzung** zu einer Vorlage.

## Bevor Sie beginnen
- Zum Lesen benötigen Sie die Leseberechtigung für Vorlagen. Schreiben und Veröffentlichen von Vorlagen benötigen mandantenweiten Bereich und die Schreib- bzw. Veröffentlichungsberechtigung für Vorlagen. Für einen Vorschlag genügt die Vorschlagsberechtigung für Vorlagen.
- Anlegen und Veröffentlichen einer Risikomethode sind getrennte Berechtigungen und benötigen mandantenweiten Bereich.
- Risikobefehle benötigen eine aktuelle Online-Verbindung; offline wird nichts in eine Warteschlange gestellt oder freigegeben.

## Schritt für Schritt
1. Sehen Sie die Vorlagen in der Liste. Jede Zeile zeigt den Zustand, die Revision **Aktuell** und die Anzahl **Vorschläge**. Öffnen Sie eine Vorlage.
2. Für eine neue Vorlage drücken Sie **Neue Vorlage**. Füllen Sie **Vorlagencode**, **Titel**, **Arbeitsart**, **Schiffs-/Schlepperbereich**, **Freigabeverantwortlicher**, **Änderungsgrund**, **Erforderliche Barrieren**, **SMS-/Anforderungsquellen** und **Gefährdungen** aus und drücken Sie dann **Entwurf speichern**.
3. Drücken Sie bei einer Entwurfsrevision **Zur Prüfung einreichen**. Fehlende Angaben werden als blockierende Punkte aufgeführt.
4. Ein Veröffentlicher gibt **Gültig ab** und eine **Begründung** mit mindestens 10 Zeichen ein und drückt **Veröffentlichen (HSEQ-Nachweis)** oder **Zurückgeben**.
5. Um eine veröffentlichte Vorlage zu ändern, schreiben Sie eine Begründung und drücken **Neue Revision beginnen**; die ältere Revision ändert sich nicht.
6. Die Crew schreibt einen Vorschlag in **Vorschlag senden** und sendet ihn. Eine berechtigte Person prüft ihn mit **Annehmen** oder **Ablehnen** und einer Begründung.
7. Geben Sie in **Risikomethoden (Matrix)** mit **Neue Methode** den **Methodencode**, die **Wahrscheinlichkeitscodes**, die **Schwerecodes** und die **Matrixzellen** ein; eine andere Person veröffentlicht sie mit **Methode veröffentlichen**.

## Was die Zustände bedeuten
- **Entwurf / In Prüfung / Zurückgegeben / Veröffentlicht / Ersetzt:** der Lebenszyklus der Vorlagenrevision.
- **Aktuell / Geplant / Nicht wirksam:** die Wirksamkeitsansicht. Die neueste veröffentlichte Revision, deren Wirksamkeitsdatum erreicht ist, ist **Aktuell**; eine mit künftigem Datum ist **Geplant**. Eine künftige Revision ersetzt die ältere nie vorzeitig.
- **Unbekannt:** Das Feld ist nicht ausgefüllt; ein leerer Wert wird nie mit einem Standardwert gefüllt.
- **No published risk method: ratings stay not assessed.** Das System bietet keine Standardmatrix und keine Schwelle an.

## Was Sie nicht tun können / Vorsicht
- Ein Verfasser einer Revision oder die Person, die sie eingereicht hat, kann dieselbe Revision nicht veröffentlichen; die Veröffentlichung ist ein eigener menschlicher Nachweis (Funktionstrennung).
- Eine Veröffentlichung ist keine Arbeitserlaubnis, keine fachliche Risikoakzeptanz und kein genehmigter Produktionskatalog.
- Eine Vorlage wird auf eine Risikobeurteilung nur als **Kopie** angewendet; sie enthält keine Bewertung, keine Unterschrift, keine Bestätigung der Beteiligten, keine Freigabe und keine Kenntnisnahme. Ändert sich eine Vorlage, ändern sich bestehende Beurteilungen nicht von selbst.
- Eine veröffentlichte Revision wird nie bearbeitet; beginnen Sie für eine Änderung eine neue Revision.

## Wenn etwas schiefgeht
- **Gesperrt, bis die aufgeführten Punkte erledigt sind.** Erledigen Sie die blockierenden Punkte und reichen Sie erneut ein.
- **This record changed elsewhere.** Drücken Sie **Neu laden**, um die aktuelle Revision zu sehen.
- **Sie dürfen diese Aktion nicht ausführen (Funktionstrennung oder Geltungsbereich).** Die Veröffentlichung muss eine andere berechtigte Person vornehmen, die die Revision nicht verfasst hat; fehlt eine Berechtigung, wenden Sie sich an den Mandantenadministrator (13.03 §10).
- **Diese Aktion passt nicht mehr zum aktuellen Zustand. Neu laden.** Der Zustand der Revision hat sich geändert.

## Verwandte Seiten
