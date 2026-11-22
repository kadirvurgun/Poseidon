---
key: critical-spares
title: Kritische Ersatzteile und Anforderungen
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite für kritische Ersatzteile und Anforderungen zeigt für das ausgewählte Schiff den **deklarierten Bestand, Minimum und Maximum, Warnungen zu niedrigem Bestand und Vorschläge für technische Anforderungen**. Sie beantwortet: „Welches kritische Ersatzteil hat sein Minimum erreicht, wie viele offene Arbeitsaufträge verweisen darauf, und wurde eine technische Anforderung vorgeschlagen und entschieden?“ Zählungen sind menschliche Deklarationen und werden nicht gegen ein Lager verifiziert.

## Wer sie verwendet
- **Leitender Ingenieur / Technischer Inspektor:** verfolgt die Mindestschwellen kritischer Ersatzteile und startet eine technische Anforderung (13.03 §5). Die Person, die eine Anforderung vorschlägt, und die Person, die sie genehmigt, sind verschiedene Personen.

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie `spare.read`. Zum Definieren von Ersatzteilen und Erfassen von Zählungen ist `spare.write` nötig, zum Vorschlagen einer Anforderung `requisition.propose` und zum Entscheiden `requisition.approve`.
- Vorschläge und Entscheidungen erfordern einen angemeldeten menschlichen Benutzer.
- Wählen Sie das richtige Schiff aus der Liste **Schiff**.

## Schritt für Schritt
1. Wählen Sie das Schiff. Die Ansicht **Kritische Ersatzteile** öffnet sich. Lesen Sie oben die Anzahlen **Niedriger Bestand** und **Prüfung erforderlich**.
2. Lesen Sie in jeder Tabellenzeile **Kritikalität des Ersatzteils**, **Kritikalität der Ausrüstung**, **Bestandsstatus**, **Prognostiziert**, **Min. / Max.** und **Offene Arbeitsaufträge**.
3. Drücken Sie **Neues kritisches Ersatzteil** für ein neues Ersatzteil oder **Bearbeiten** für ein bestehendes. Geben Sie **Teilenummer**, **Name**, **Einheit**, **Mindestbestand** und **Höchstbestand** ein. Lassen Sie Schwellen leer, wenn sie nicht festgelegt sind.
4. Um eine Zählung einzugeben, kreuzen Sie **Neue Bestandszählung erfassen** an, füllen **Gezählte Menge** aus und drücken **Speichern**.
5. Drücken Sie in einer Zeile mit **Niedriger Bestand** ohne offene Anforderung auf **Anforderung vorschlagen**. Der Server berechnet die vorgeschlagene Menge (Maximum minus prognostizierter Bestand). Ist kein Maximum festgelegt, wird keine Menge vorgeschlagen, und die genehmigende Person muss **Genehmigte Menge** eingeben.
6. In der Ansicht **Technische Anforderungen** prüft eine berechtigte Person, die den Vorschlag nicht gemacht hat, die **Genehmigte Menge** und drückt **Für ERP-Export genehmigen** oder wählt einen **Ablehnungsgrund** und drückt **Ablehnen**.
7. Öffnen Sie bei einer genehmigten Anforderung **ERP-Exportreferenz**, um die Referenz zu sehen.

## Was die Zustände bedeuten
- **Niedriger Bestand:** Der prognostizierte Bestand (letzte deklarierte Zählung minus der Ersatzteile, die seit dieser Zählung auf eingereichten Arbeitsaufträgen erfasst wurden) liegt beim oder unter dem Minimum. Der Anforderungsvorschlag wird in dieser Zeile angeboten.
- **Über dem Minimum (deklariert):** Der prognostizierte Bestand liegt über dem Minimum; die Zählung ist nicht verifiziert.
- **Minimum nicht konfiguriert:** Es wurde keine Schwelle eingegeben. Eine leere Schwelle wird als Prüfung erforderlich gemeldet, nie als in Ordnung.
- **Stock unknown / Consumption unresolved:** Der Bestand konnte nicht berechnet werden. Unbekannt bedeutet nicht „genug Bestand“.
- **Proposed / Approved for ERP export / Rejected:** der Zustand der technischen Anforderung.
- **Critical (declared) / Non-critical (declared) / Unbekannt:** Die Kritikalität des Ersatzteils ist eine Deklaration.

## Was Sie nicht tun können / Vorsicht
- Über eine Anforderung, die Sie selbst vorgeschlagen haben, können Sie nicht entscheiden. Die Seite zeigt „Sie haben dies vorgeschlagen; eine andere Person muss entscheiden.“
- Vorschlag und Genehmigung sind getrennte menschliche Handlungen. Die Genehmigung erzeugt nur eine ERP-Exportreferenz; Bestellungen, Lieferanten oder Preise werden hier nicht verwaltet.
- Die Seite hat kein separates Feld für eine Nachbestellschwelle; niedriger Bestand wird am Mindestbestand gemessen.
- Offline können die Daten nicht aktualisiert und keine Aktionen ausgeführt werden.

## Wenn etwas schiefgeht
- **Der Datensatz hat sich geändert:** Verwenden Sie **Erneut versuchen**, um die Seite zu aktualisieren, und versuchen Sie es erneut.
- **Ergebnis unbekannt:** **Denselben Befehl wiederholen** sendet denselben Befehl.
- **Die Anfrage wurde durch eine Regel abgelehnt:** Prüfen Sie den Bestandsstatus und die Angaben zur offenen Anforderung.
- **Sie haben keine Berechtigung:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
