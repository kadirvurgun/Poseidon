---
key: noon-report
title: Noon-Report
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: [1]
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Noon-Report“ führt die **täglichen Mittagsangaben** des ausgewählten Schiffs als Entwürfe mit ihrer erfassten Quellversion. Sie beantwortet: „Was haben wir zu Berichtszeit, Reise, Position, Distanz, Wetter, Ladung, Maschinenstunden, Brennstoff und ROB angegeben, und hat der Kapitän für genau diese Revision eine Prüfung erfasst?“ Ein gespeicherter Entwurf ist eine unverifizierte Angabe. Das Speichern begründet keine Genehmigung durch Kapitän oder Leitenden Ingenieur, und die Seite zieht daraus keine Schlussfolgerung zu Emissionen, Brennstoffsicherheit oder Bereitschaft.

## Wer sie verwendet
- **Offizier, der den Bericht erstellt (Crew / OOW):** füllt den Noon-Entwurf für das richtige Schiff und Datum aus und speichert ihn (13.03 §6).
- **Kapitän:** prüft den gespeicherten Bericht und erfasst diese Prüfung als menschliche Handlung (13.03 §2). Nur eine Bestellung mit der Funktion Kapitän kann eine Noon-Prüfung erfassen.

## Bevor Sie beginnen
- Wählen Sie in der oberen Kontextleiste das **richtige Unternehmen und Schiff**. Ist kein Schiff ausgewählt, zeigt die Seite „Schiff auswählen“.
- Zum Lesen benötigen Sie `daily.noon.read`. Zum Erstellen und Speichern von Entwürfen sind `daily.noon.write` und Bearbeitungsrechte für dieses Schiff nötig; ohne sie sehen Sie „Schreibgeschützter Zugriff. Bearbeiten und Speichern sind nicht erlaubt.“
- Zum Erfassen einer Prüfung benötigen Sie `daily.noon.write` mit Bearbeitungsrechten für dieses Schiff, `daily.noon.review` sowie eine aktuelle Bestellung als Kapitän für genau diesen Datensatz.

## Schritt für Schritt
1. Wählen Sie das Schiff. Die Liste **Erfasste Entwürfe** zeigt jeden Bericht nach Datum und Revision mit dem Abzeichen „Entwurf · Prüfung erforderlich“. Mit **Vorherige Entwürfe** / **Weitere Entwürfe** blättern Sie.
2. Drücken Sie **Neuer Entwurf**, um **Neuer Tagesentwurf** zu öffnen, oder öffnen Sie einen aufgeführten Bericht mit **Erfassten Entwurf bearbeiten**.
3. Füllen Sie die Gruppen aus (**Bericht und Reise**, **Position und Tätigkeit**, **Wetter und Ladung**, **Maschinen, Brennstoff und ROB**). Wählen Sie für jedes Feld einen **Datenzustand**: **Bekannt**, **Unbekannt** oder **Fehlend**. Unvollständige Entwürfe können gespeichert werden.
4. Drücken Sie **Entwurf speichern**. Die Seite bestätigt „Draft saved. Human review and operational approval remain separate.“ Jedes Speichern fügt unter **Erfasster Revisionsverlauf** eine Revision hinzu.
5. Bei einem gespeicherten Bericht öffnet der Kapitän **Erfassung menschlicher Prüfungen**, wählt eine **Bestellung einer befugten Person** und einen **Prüfeintrag** („Geprüfte Angabe erfassen (unbewertet)“ oder „Zur Korrektur zurückgeben“), schreibt eine **Prüfbegründung** und drückt dann **Prüfeintrag erfassen**.
6. Muss Brennstoff oder ein Hafenereignis erfasst werden, verwenden Sie **Gespeicherte betriebliche Quelle verwenden** und **Brennstoffdatensatz aus gespeicherter Quelle öffnen** oder **Hafenereignis aus gespeicherter Quelle öffnen**. Mengen, Einheiten und Genehmigungen werden nicht übernommen.

## Was die Zustände bedeuten
- **Draft · declarations unverified / Unverified declaration:** Die Werte wurden von einer Person angegeben; niemand hat sie verifiziert.
- **Bekannt / Unbekannt / Fehlend:** der Datenzustand eines Feldes. Unbekannt bedeutet weder „Nein“ noch „Bereit“ noch „Nicht zutreffend“, und Fehlend ist nicht null.
- **Ausstehend · Prüfung nicht erfasst:** Für diese Revision wurde keine menschliche Prüfung erfasst.
- **Human metadata recorded · unassessed / Recorded · unassessed:** Für die angezeigte Revision gibt es einen Prüfeintrag des Kapitäns. Das Erfassen einer Prüfung begründet weder eine Verifizierung noch eine Einreichung noch eine Sicherheitsfreigabe durch den Kapitän.
- **Historical revision:** Der Prüfeintrag gehört zu einer älteren Revision und prüft nicht den aktuell gespeicherten Entwurf.

## Was Sie nicht tun können / Vorsicht
- Das System prüft den Bericht nie und schließt ihn nie ab. Das Erfassen einer Prüfung ist eine menschliche Handlung des bestellten Kapitäns (13.03 §2).
- Wer eine Revision des Protokolls verfasst hat, kann es nicht prüfen. Einem Verfasser wird keine Bestellung angeboten, und die Seite zeigt „You authored this log revision; an independent reviewer must record the review.“; der Server lehnt eine solche Prüfung zudem wegen Funktionstrennung ab. Hat der Kapitän den Bericht geschrieben, muss trotzdem ein unabhängiger Prüfer mit Prüfbefugnis die Prüfung erfassen.
- Ein neues Speichern erzeugt eine neue Revision; die frühere Prüfung bleibt bei ihrer eigenen Revision. Prüfen Sie nach Änderungen erneut.
- Eine eingetippte Freitextreferenz kann eine berechtigte Suche nicht ersetzen; lassen Sie das Feld auf Fehlend oder Unbekannt.
- Offline können nur ausdrücklich klassifizierte Felder in die verschlüsselte Warteschlange gelangen. Prüf-, Genehmigungs- und Verifizierungsfelder sind ausgeschlossen.

## Wenn etwas schiefgeht
- **„The record changed. Your inputs are retained.“:** Jemand hat eine neuere Version gespeichert. Verwerfen Sie ausdrücklich und laden Sie neu, bevor Sie es erneut versuchen.
- **„Save outcome unknown“:** Die Felder sind eingefroren; verwenden Sie **Exakten gespeicherten Befehl wiederholen**, geben Sie nichts neu ein.
- **„No current source-authorized human appointment is available for this record.“:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext mit Ihrem Mandantenadministrator (13.03 §10).
- **„Human conflict resolution required“:** Eine Offline-Erfassung steht im Konflikt mit dem Server. Öffnen Sie **Konflikt prüfen** oder das Konfliktzentrum und wählen Sie ausdrücklich; nichts wird automatisch zusammengeführt (13.03 §10).
- **Tagesquellen nicht verfügbar oder Zugriff verweigert:** Verwenden Sie **Lesen der Quellen wiederholen**; besteht das Problem weiter, melden Sie dem Support die Uhrzeit und Ihre Schritte.

## Verwandte Seiten
