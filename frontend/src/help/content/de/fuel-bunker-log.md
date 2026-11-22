---
key: fuel-bunker-log
title: Brennstoff, Bunker und ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite für Brennstoff, Bunker und ROB führt die **Brennstoff- und Bunkerangaben** des ausgewählten Schiffs als unverifizierte Entwürfe mit ihrem Quellenverlauf. Sie beantwortet: „Welcher Brennstoff wurde übernommen oder verbraucht, welchen ROB haben wir vorher und nachher angegeben, welche BDN-, Lieferanten-, Proben- und Tankreferenzen gehören dazu, und setzt sich die angegebene ROB-Kette von einem Datensatz zum nächsten fort?“ Die Seite korrigiert keine Mengen, berechnet keine Emissionen und stellt weder Konformität noch Verifizierung fest.

## Wer sie verwendet
- **Leitender Ingenieur / Maschinenoffiziere:** erfassen Bunkerungen, Verbrauch und ROB-Angaben mit ihren Nachweisreferenzen (13.03 §5).
- **Kapitän:** verfolgt die Brennstoffangaben neben den vom Kapitän geprüften Noon-Zusammenfassungen (13.03 §2).

## Bevor Sie beginnen
- Wählen Sie in der oberen Kontextleiste das **richtige Unternehmen und Schiff**. Ist kein Schiff ausgewählt, zeigt die Seite „Schiff auswählen“.
- Zum Lesen benötigen Sie `operations.fuel.read`. Zum Erstellen und Speichern sind `operations.fuel.write` und Bearbeitungsrechte für dieses Schiff nötig.
- Zeitangaben müssen Datum, Sekunden und Z oder einen ausdrücklichen UTC-Versatz enthalten; eine lokale Zeit ohne Zeitzone wird nicht akzeptiert.

## Schritt für Schritt
1. Wählen Sie das Schiff. **Brennstoff- und ROB-Datensätze** listet die zurückgegebenen Datensätze auf. Mit **Zurückgegebene Seite filtern** grenzen Sie nur diese Seite ein.
2. Lesen Sie unterhalb der Liste die **ROB-Kontinuitätsprüfung**. Sie zeigt je angegebener Brennstoffart die Bilanz (vorher + übernommen − verbraucht − nachher), **Kontinuität zum vorherigen Eintrag** und **Prüfhinweise**.
3. Lesen Sie die **Vom Kapitän geprüfte Noon-Kraftstoff- und ROB-Zusammenfassungen** als Kontext. Sie werden als angegebener Text angezeigt und nicht ins Journal übernommen.
4. Drücken Sie **Neuer Entwurfsdatensatz**. Füllen Sie **Datensatz- und Lieferkontext**, **Mengen- und ROB-Angaben** und **Brennstoffeigenschaften und Probendatensatz** aus. Behalten Sie für jedes Feld einen angegebenen Wert, **Fehlend** oder **Unbekannt**.
5. Drücken Sie **Ungeprüften Entwurf speichern**. Jedes Speichern fügt dem **Erfasster Revisionsverlauf** eine Revision hinzu.
6. Um von einem gespeicherten Noon-Report oder Maschinenprotokoll aus zu beginnen, verwenden Sie auf jener Seite **Brennstoffdatensatz aus gespeicherter Quelle öffnen**. Die **Verknüpfte Tagesangabe** bleibt hier schreibgeschützt.

## Was die Zustände bedeuten
- **Draft declaration / Unverified draft record:** Eine Person hat die Werte angegeben; nichts ist verifiziert.
- **Prüfung erforderlich:** der Konformitätsprüfzustand jedes Datensatzes; die Konformität wurde nicht bewertet.
- **Ausstehend; Verifizierung nicht nachgewiesen:** der Verifizierungsprüfzustand; es gibt keine Verifizierung.
- **Keine Abweichung festgestellt:** Die angegebenen Zahlen gehen auf. Dies ist keine Verifizierung.
- **Unbekannt:** Die Kontinuität kann nicht beurteilt werden, zum Beispiel weil ein Wert fehlt oder ältere Historie außerhalb des bewerteten Zeitfensters liegt. Unbekannt bedeutet weder „Keine Abweichung“ noch „Bereit“.
- **Fehlend / Unbekannt:** getrennt von null.

## Was Sie nicht tun können / Vorsicht
- Menschliche Prüfung, betriebliche oder rechtliche Genehmigung, Abschluss und Offline-Erfassung sind auf dieser Seite nicht verfügbar.
- Das System markiert Unterschiede zur menschlichen Prüfung; es korrigiert nie eine Menge und entscheidet nie über die Brennstoffkonformität.
- Bewertet werden nur die neuesten 100 Datensätze. Ohne Tankreferenz ist eine Lücke bei einem Schiff mit mehreren Tanks eine Frage nach fehlenden Tankdaten, kein Datenfehler.
- Mengen aus Noon-Report oder Maschinenprotokoll mit ungeklärter Einheit werden nie in übernommene, verbrauchte oder ROB-Werte umgerechnet.

## Wenn etwas schiefgeht
- **„The saved source changed.“:** Ihre Eingaben bleiben erhalten; laden Sie ausdrücklich neu oder verwerfen Sie, bevor Sie erneut speichern.
- **„Outcome unknown.“:** Verwenden Sie **Exakten betrieblichen Befehl wiederholen**; geben Sie nichts neu ein.
- **„Check the declared values, explicit time offsets and current source permission.“:** Korrigieren Sie das markierte Feld oder lassen Sie es auf Fehlend oder Unbekannt.
- **Betriebliche Quelle oder Zugriff nicht verfügbar:** Verwenden Sie **Quellenlesen wiederholen**; bleibt der Zugriff verweigert, prüfen Sie Ihre Rolle und Ihren Schiffskontext mit Ihrem Mandantenadministrator (13.03 §10).

## Verwandte Seiten
