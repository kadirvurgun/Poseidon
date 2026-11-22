---
key: port-activity-sof
title: Hafentätigkeit und SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Hafentätigkeit und SOF führt die **Hafenereignisse** des ausgewählten Schiffs als betrieblichen Datensatz mit Nachweisreferenzen und ermöglicht es einer bestellten Person, das **Statement of Facts (SOF)** intern abzuschließen. Sie beantwortet: „Welche Hafenereignisse haben wir angegeben, in welcher UTC-Reihenfolge und mit welchem Intervall, mit welchen Ladungs-, Tiefgangs-, Lotsen-, Schlepper-, Verzögerungs- und Protestreferenzen, und hat eine bestellte Person diese SOF-Revision abgeschlossen?“ Die Seite erzeugt keine Unterschrift, keine Liegezeit- oder Liegegeldberechnung, und erfasste Ereignisse sind weder Anweisungen noch genehmigte geplante Zeitfenster.

## Wer sie verwendet
- **Kapitän:** um Ankunft und Hafentätigkeit als betrieblichen Datensatz mit Nachweisen zu führen und das SOF abzuschließen (13.03 §2).
- **Erster Offizier / Leitender Ingenieur:** um Hafenereignisse zu erfassen und mit einer aktuellen Bestellung das SOF abzuschließen.

## Bevor Sie beginnen
- Wählen Sie in der oberen Kontextleiste das **richtige Unternehmen und Schiff**. Ist kein Schiff ausgewählt, zeigt die Seite **Schiff auswählen**.
- Zum Lesen benötigen Sie `operations.port.read`; zum Speichern von Entwürfen `operations.port.write` und Bearbeitungsrechte für dieses Schiff.
- Der Abschluss benötigt `operations.port.write`, Bearbeitungsrechte für dieses Schiff und `operations.port.finalize` sowie eine aktuelle Abschlussbestellung (Kapitän, Erster Offizier oder Leitender Ingenieur) für dieses Schiff.

## Schritt für Schritt
1. Wählen Sie das Schiff. **Zeitverlauf erfasster Hafenereignisse** listet die gelieferten Ereignisse in der UTC-Reihenfolge des Servers. **Zurückgegebene Seite filtern** grenzt nur diese Seite ein.
2. Lesen Sie die **Statement-of-Facts-Zeitleiste** unter der Liste: jedes Ereignis mit **Erfasstes Intervall**, **Verzögerungsgrund** und **Bemerkungen**.
3. Drücken Sie **Neuer Entwurfsdatensatz**. Füllen Sie **Hafenanlaufkontext**, **Erfasste Tätigkeit und Zeitangaben**, **Ladungs- und Tiefgangsangaben** sowie **Unterstützung, Ausnahmen und Nachweise** aus. Ereigniszeiten benötigen Z oder einen ausdrücklichen UTC-Versatz.
4. Drücken Sie **Ungeprüften Entwurf speichern**. Jedes Speichern fügt dem **Erfasster Revisionsverlauf** eine Revision hinzu.
5. Öffnen Sie bei einem gespeicherten Datensatz **Abschluss des Statement of Facts**. Wird **Fehlende Pflichtangaben** angezeigt, ergänzen Sie zuerst diese Felder.
6. Setzen Sie bei jeder aufgeführten ungelösten Referenz das Häkchen, um sie zur Kenntnis zu nehmen, wählen Sie Ihre **Bestellung** und drücken Sie **SOF abschließen**. Das System erfasst, wer abgeschlossen hat, wann, die Funktion und den Grund.

## Was die Zustände bedeuten
- **ENTWURF:** Das SOF ist nicht abgeschlossen.
- **FINAL (intern):** Eine bestellte Person hat diese Revision abgeschlossen. Er ist intern: keine Unterschrift und keine externe Vereinbarung.
- **Unterschrift nicht erfasst:** wird immer angezeigt; auf dieser Seite gibt es keine Unterschrift.
- **Nach FINAL geändert: neue Prüfung und neuer Abschluss erforderlich.:** Der Datensatz wurde nach dem Abschluss erneut gespeichert und ist wieder ENTWURF.
- **UNKNOWN (unresolved references):** Für diese erforderlichen Referenzen gibt es keine zuständige Nachschlagequelle. Die Kenntnisnahme macht sie nicht bekannt.
- **Prüfung erforderlich / Ungeprüfter Entwurfsdatensatz:** Hafenereignisse sind Angaben; nichts ist geprüft.
- **Entwurf (nicht finalisiert) / Final (intern; keine Unterschrift):** der vom Server für diese Revision erfasste SOF-Zustand, angezeigt in der Datensatzübersicht. Final entsteht nur aus einem erfassten menschlichen Abschluss.

## Was Sie nicht tun können / Vorsicht
- Das System schließt das SOF nie ab. Der Abschluss ist eine menschliche Handlung eines bestellten Kapitäns, Ersten Offiziers oder Leitenden Ingenieurs (13.03 §2).
- **SOF abschließen** bleibt deaktiviert, solange erforderliche Angaben fehlen, keine Bestellung vorliegt oder ungelöste Referenzen nicht zur Kenntnis genommen sind.
- Der Abschluss ist keine Unterschrift, keine externe Vereinbarung und keine Liegezeit- oder Liegegeldberechnung.
- Die Fußzeile der Seite lautet: „Die SOF-Finalisierung ist eine interne menschliche Handlung, die ein bestellter Kapitän, Erster Offizier oder Leitender Ingenieur im Finalisierungsbereich erfasst. Unterschriften, betriebliche/rechtliche Genehmigung und Offline-Erfassung bleiben nicht verfügbar.“
- Speichern von Entwürfen und Abschließen benötigen eine aktuelle Online-Sitzung; diese Seite hat keine Offline-Erfassung.

## Wenn etwas schiefgeht
- **„Keine aktuelle Abschlussbestellung für dieses Schiff.“:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext mit Ihrem Mandantenadministrator (13.03 §10).
- **„Ergebnis unbekannt: Ein erneuter Versuch sendet denselben Befehl einmal.“:** Verwenden Sie **Erneut versuchen**; beginnen Sie keinen neuen Abschluss.
- **„Befehl nicht angenommen“:** Lesen Sie den angezeigten Code, laden Sie den Datensatz mit **Aktuellen gespeicherten Datensatz lesen** neu und versuchen Sie es erneut.
- **„The saved source changed.“:** Laden Sie ausdrücklich neu oder verwerfen Sie, bevor Sie erneut speichern.

## Verwandte Seiten
