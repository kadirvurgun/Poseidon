---
key: deck-daily-log
title: Tägliches Decksprotokoll
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Das tägliche Decksprotokoll führt die **Deckswachaufzeichnungen** des ausgewählten Schiffs als Entwürfe mit ihrer erfassten Quellversion. Es beantwortet: „Was hat die Wache für dieses Datum und diesen Zeitraum zu Position, Kurs, Geschwindigkeit, Wetter, Sicht, Navigation, Hafen- und Ladungsarbeiten, Ballast, Decksarbeiten, Sicherheitsbeobachtungen und Übergabe angegeben, und wurde für genau diese Revision eine Prüfung erfasst?“ Ein gespeicherter Eintrag ist eine unverifizierte Angabe, kein genehmigtes Protokoll.

## Wer sie verwendet
- **Wachoffizier (Crew / OOW):** führt die Wachaufzeichnung in Echtzeit und in chronologischer Reihenfolge (13.03 §6).
- **Kapitän oder Erster Offizier:** prüft das gespeicherte Protokoll und erfasst diese Prüfung als menschliche Handlung (13.03 §2). Nur eine Bestellung mit der Funktion Kapitän oder Erster Offizier kann eine Prüfung des Decksprotokolls erfassen.

## Bevor Sie beginnen
- Wählen Sie in der oberen Kontextleiste das **richtige Unternehmen und Schiff**. Ist kein Schiff ausgewählt, zeigt die Seite „Schiff auswählen“.
- Zum Lesen benötigen Sie `daily.deck.read`. Zum Erstellen und Speichern sind `daily.deck.write` und Bearbeitungsrechte für dieses Schiff nötig; andernfalls sehen Sie „Schreibgeschützter Zugriff. Bearbeiten und Speichern sind nicht erlaubt.“
- Zum Erfassen einer Prüfung benötigen Sie `daily.deck.write` mit Bearbeitungsrechten für dieses Schiff, `daily.deck.review` sowie eine aktuelle Bestellung als Kapitän oder Erster Offizier für genau diesen Datensatz.

## Schritt für Schritt
1. Wählen Sie das Schiff. **Erfasste Entwürfe** listet jedes Protokoll nach Datum und Revision mit „Entwurf · Prüfung erforderlich“ auf.
2. Drücken Sie **Neuer Entwurf** für einen **Neuer Tagesentwurf**, oder öffnen Sie ein aufgeführtes Protokoll mit **Erfassten Entwurf bearbeiten**.
3. Füllen Sie zuerst **Wachkontext** aus (**Protokolldatum**, **Wache / Zeitraum**, **Referenz des Wachoffiziers**). Die Offiziersliste bietet nur Personen an, die für das angegebene Protokolldatum verfügbar sind.
4. Füllen Sie **Navigation und Wetter**, **Hafen- und Ladungstätigkeiten** und **Decksarbeiten und Übergabe** aus. Wählen Sie für jedes Feld **Bekannt**, **Unbekannt** oder **Fehlend**.
5. Drücken Sie während oder direkt nach der Wache **Entwurf speichern**. Jedes Speichern fügt dem **Erfasster Revisionsverlauf** eine Revision hinzu; frühere Revisionen bleiben unverändert.
6. Der Kapitän oder Erste Offizier öffnet **Erfassung menschlicher Prüfungen**, wählt eine **Bestellung einer befugten Person** und einen **Prüfeintrag**, schreibt eine **Prüfbegründung** und drückt **Prüfeintrag erfassen**.

## Was die Zustände bedeuten
- **Draft · declarations unverified / Unverified declaration:** von einer Person angegeben; nicht verifiziert.
- **Bekannt / Unbekannt / Fehlend:** der Datenzustand des Feldes. Unbekannt bedeutet weder „Nein“ noch „Bereit“ noch „Nicht zutreffend“.
- **Ausstehend · Prüfung nicht erfasst:** Für diese Revision gibt es keine menschliche Prüfung.
- **Menschliche Metadaten erfasst · unbewertet:** Für diese Revision gibt es einen Prüfeintrag. Er begründet weder eine Verifizierung noch eine Einreichung noch eine Sicherheitsfreigabe durch den Kapitän.
- **Record reviewed declaration (unassessed) / Return for correction:** die beiden Arten von Prüfeinträgen, die ein Prüfer erfassen kann.
- **Historical revision:** Die Prüfung gehört zu einer älteren Revision, nicht zum aktuellen Entwurf.

## Was Sie nicht tun können / Vorsicht
- Das System prüft das Protokoll nie und schließt es nie ab. Die Prüfung ist eine menschliche Handlung des bestellten Kapitäns oder Ersten Offiziers (13.03 §2).
- Wer eine Revision des Protokolls verfasst hat, kann es nicht prüfen. Einem Verfasser wird keine Bestellung angeboten, und die Seite zeigt „You authored this log revision; an independent reviewer must record the review.“; der Server lehnt eine solche Prüfung zudem wegen Funktionstrennung ab.
- Halten Sie die Einträge in Echtzeit und chronologisch; schreiben Sie eine alte Wache nicht so um, dass sie wie eine neue aussieht (13.03 §6).
- Ändern Sie nicht den geprüften Datensatz einer anderen Person; ein späteres Speichern ist eine neue Revision und benötigt eine neue Prüfung (13.03 §6).
- Die Offline-Erfassung umfasst nur ausdrücklich klassifizierte Felder. Ein Offline-Konflikt wird nie automatisch zusammengeführt; gehen Sie nicht davon aus, dass sich Ihr Datensatz durchsetzt (13.03 §6).

## Wenn etwas schiefgeht
- **„The record changed. Your inputs are retained.“:** Verwerfen Sie ausdrücklich und laden Sie neu, bevor Sie es erneut versuchen.
- **„Save outcome unknown“:** Verwenden Sie **Exakten gespeicherten Befehl wiederholen**; geben Sie die Felder nicht neu ein.
- **Offiziersreferenz nicht verfügbar:** Lassen Sie das Feld auf Fehlend oder Unbekannt; ein eingetippter Name kann die Suche nicht ersetzen.
- **„Human conflict resolution required“:** Öffnen Sie **Konflikt prüfen** oder das Konfliktzentrum und wählen Sie ausdrücklich (13.03 §10).
- **Keine Bestellung verfügbar:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext mit Ihrem Mandantenadministrator (13.03 §10).

## Verwandte Seiten
