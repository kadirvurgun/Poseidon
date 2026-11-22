---
key: engine-daily-log
title: Tägliches Maschinenprotokoll
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Das tägliche Maschinenprotokoll führt die **Maschinenwachaufzeichnungen** des ausgewählten Schiffs als Entwürfe mit ihrer erfassten Quellversion. Es beantwortet: „Was hat die Maschinenwache zu Maschinenstatus und Betriebsstunden, Brennstoff- und Schmierölverbrauch, Frischwasser, kritischen Messwerten, Alarmen, Mängeln, Bunkern, Bilge und Umweltkontrollen angegeben, und hat der Leitende Ingenieur für genau diese Revision eine Prüfung erfasst?“ Ein gespeicherter Eintrag ist eine unverifizierte Angabe. Entwurfsdaten werden nicht in Schlussfolgerungen zu Emissionen, Brennstoffsicherheit oder Bereitschaft umgewandelt.

## Wer sie verwendet
- **Wachingenieur (Crew / OOW / Engineer):** führt die Maschinenwachaufzeichnung in Echtzeit und in chronologischer Reihenfolge (13.03 §6).
- **Leitender Ingenieur:** prüft das gespeicherte Protokoll, verknüpft es mit Arbeitsaufträgen und Mängeln und erfasst die Prüfung als menschliche Handlung (13.03 §5). Nur eine Bestellung mit der Funktion Leitender Ingenieur kann eine Prüfung des Maschinenprotokolls erfassen.

## Bevor Sie beginnen
- Wählen Sie in der oberen Kontextleiste das **richtige Unternehmen und Schiff**.
- Zum Lesen benötigen Sie `daily.engine.read`; zum Speichern `daily.engine.write` und Bearbeitungsrechte für dieses Schiff. Zum Erfassen einer Prüfung benötigen Sie `daily.engine.write` mit Bearbeitungsrechten für dieses Schiff, `daily.engine.review` sowie eine aktuelle Bestellung als Leitender Ingenieur für genau diesen Datensatz.
- Zum Verknüpfen von Arbeitsaufträgen oder Mängeln ist außerdem Lesezugriff auf diese Datensätze nötig.

## Schritt für Schritt
1. Wählen Sie das Schiff. **Erfasste Entwürfe** listet jedes Protokoll nach Datum und Revision mit „Entwurf · Prüfung erforderlich“ auf.
2. Drücken Sie **Neuer Entwurf**, oder öffnen Sie ein aufgeführtes Protokoll mit **Erfassten Entwurf bearbeiten**.
3. Füllen Sie **Maschinenwachkontext**, **Maschinenstatus und Betriebsstunden**, **Verbrauch und Erzeugung**, **Messungen, Alarme und Mängel** und **Arbeits- und Umweltkontext** aus. Wählen Sie für jedes Feld **Bekannt**, **Unbekannt** oder **Fehlend**.
4. Drücken Sie **Entwurf speichern**. Jedes Speichern fügt dem **Erfasster Revisionsverlauf** eine Revision hinzu.
5. Verwenden Sie bei einem gespeicherten Protokoll **Arbeitsaufträge und Mängel**: Wählen Sie einen **Datensatztyp** und **Datensatz** und dann **Verknüpfen**. Eine Verknüpfung fixiert genau eine Revision; **Verknüpfung entfernen** entfernt sie.
6. Öffnen Sie bei Bedarf **Brennstoffdatensatz aus gespeicherter Quelle öffnen** oder **Ausfallzeitdatensatz aus gespeicherter Quelle öffnen**. Mengen und Einheiten werden nicht übernommen.
7. Der Leitende Ingenieur öffnet **Erfassung menschlicher Prüfungen**, wählt eine **Bestellung einer befugten Person** und einen **Prüfeintrag**, schreibt eine **Prüfbegründung** und drückt **Prüfeintrag erfassen**.

## Was die Zustände bedeuten
- **Unverified declaration / Bekannt / Unbekannt / Fehlend:** angegebene Werte und ihr Datenzustand. Unbekannt bedeutet weder „Nein“ noch „Bereit“ noch „Nicht zutreffend“.
- **Ausstehend · Prüfung nicht erfasst:** Für diese Revision gibt es keine menschliche Prüfung.
- **Menschliche Metadaten erfasst · unbewertet:** Für diese Revision gibt es einen Prüfeintrag des Leitenden Ingenieurs. Er begründet weder eine Verifizierung noch eine Abzeichnung durch den Leitenden Ingenieur.
- **Current revision / Newer revision exists:** ob sich ein verknüpfter Arbeitsauftrag oder Mangel seit der Verknüpfung geändert hat. Eine Verknüpfung dient nur der Herkunftsverfolgung; sie verifiziert, schließt oder genehmigt diesen Datensatz nicht.
- **Suche nicht verfügbar — UNBEKANNT:** Der verknüpfte Datensatztyp kann mit Ihrem Zugriff nicht gelesen werden.

## Was Sie nicht tun können / Vorsicht
- Das System prüft das Protokoll nie und schließt es nie ab. Die Prüfung ist eine menschliche Handlung des bestellten Leitenden Ingenieurs (13.03 §5).
- Wer eine Revision des Protokolls verfasst hat, kann es nicht prüfen. Einem Verfasser wird keine Bestellung angeboten, und die Seite zeigt „You authored this log revision; an independent reviewer must record the review.“; der Server lehnt eine solche Prüfung zudem wegen Funktionstrennung ab.
- Halten Sie die Einträge in Echtzeit und chronologisch; ändern Sie nicht den geprüften Datensatz einer anderen Person (13.03 §6).
- Das Verknüpfen eines Mangels hier erfasst keine Einschränkung und schließt ihn nicht; tun Sie das auf der Mängel- oder Arbeitsauftragsseite (13.03 §5).
- Verknüpfungen benötigen eine aktuelle Online-Verbindung; offline wird nichts verknüpft. Offline-Konflikte werden nie automatisch zusammengeführt.

## Wenn etwas schiefgeht
- **„The log entry changed. Reload it before changing links.“:** Laden Sie neu und verknüpfen Sie dann erneut.
- **„Save outcome unknown“:** Verwenden Sie **Exakten gespeicherten Befehl wiederholen**; geben Sie nichts neu ein.
- **„Human conflict resolution required“:** Öffnen Sie **Konflikt prüfen** oder das Konfliktzentrum und wählen Sie ausdrücklich (13.03 §10).
- **Keine Bestellung verfügbar oder Zugriff verweigert:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext mit Ihrem Mandantenadministrator (13.03 §10).

## Verwandte Seiten
