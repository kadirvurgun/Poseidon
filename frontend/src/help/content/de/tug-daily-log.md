---
key: tug-daily-log
title: Schlepper-Tagesprotokoll
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## Wofür diese Seite dient
Das Schlepper-Tagesprotokoll erfasst für ein Datum und eine Wache **Aufträge, Betriebsstundenzähler der Maschinen, Kraftstoff und Mängel** und vergleicht sie zum **Abgleich** mit den Schleppeinsätzen des Tages. Es beantwortet: „Wie viele Aufträge wurden an diesem Tag / in dieser Wache abgeschlossen, stimmen die Zählerstunden mit den Einsatzzeiten überein, wie viel Kraftstoff wurde verbraucht und welche Mängel wurden gemeldet?“ Der Abgleich ist beratend; die Prüfung durch den Kapitän ist eine separate menschliche Aufzeichnung.

## Wer sie verwendet
- **Besatzung / Wachhabender:** um das Wachprotokoll in Echtzeit und in der richtigen Reihenfolge zu führen (13.03 §6).
- **Schlepperkapitän / Kapitän:** um ein eingereichtes Protokoll zu prüfen und es zu verifizieren oder zurückzugeben (13.03 §3).

## Bevor Sie beginnen
- Öffnen Sie den Schlepper über **Schlepperbetrieb**.
- Zum Anlegen und Bearbeiten eines Protokolls benötigen Sie Schreibzugriff auf Schlepper.
- Die Prüfung erfordert eine vom Server erfasste **Bestellung** (Schlepperkapitän oder Kapitän) für diesen Schlepper. Wer das Protokoll geschrieben oder eingereicht hat, kann es nicht prüfen.

## Schritt für Schritt
1. Öffnen Sie ein bestehendes Protokoll aus der Liste oder wählen Sie **Protokolldatum** und **Wache** und drücken Sie **Tagesprotokoll anlegen**.
2. Geben Sie die **Zeitzone (IANA)** ein.
3. Geben Sie unter **Betriebsstundenzähler der Maschinen** für jeden **Zähler** den **Anfangsstand**, den **Endstand** und bei Bedarf den **Nachweis für Rücksetzung oder Überlauf** ein.
4. Geben Sie unter **Kraftstoffverbrauch** den Wert, die Einheit und die **Referenz der Kraftstoffquelle** ein.
5. Markieren Sie unter **Schleppeinsätze des Tages** die Einsätze, die zu diesem Protokoll gehören; füllen Sie **Linked defect identifiers** und **Tageszusammenfassung** aus (Zusammenfassung mindestens 20 Zeichen).
6. Drücken Sie **Speichern**. Prüfen Sie in der Zeile **Abgleich** die abgeschlossenen Aufträge, Einsatzstunden, Zählerstunden und gemeldeten Mängel.
7. Ist die Liste **Hindernisse** leer, drücken Sie **Zur Prüfung durch den Kapitän einreichen**.
8. Wählen Sie als Kapitän Ihre **Bestellung**, schreiben Sie eine Begründung und drücken Sie **Kapitän: Protokoll verifizieren** oder **Kapitän: Protokoll zurückgeben**.

## Was die Zustände bedeuten
- **Draft / Submitted / Returned / Verified:** der Zustand des Protokolls.
- **Master review — Pending / Attention / Reviewed:** Pending = wartet auf Prüfung; Attention = zurückgegeben oder eingereicht, während der Abgleich nicht Reconciled ist; Reviewed = vom Kapitän verifiziert. Ein eingereichtes Protokoll ist kein verifiziertes Protokoll.
- **Reconciled / Review required / Incomplete:** das beratende Vergleichsergebnis. Die Begründungscodes werden darunter aufgeführt.
- **offenes Intervall:** Ein Einsatz läuft noch; die Einsatzstunden sind noch nicht endgültig.
- **Safety events — Unknown:** Diese Seite zählt keine Sicherheitsereignisse; es wird kein Schluss „keine Ereignisse“ gezogen.
- **Synced / Waiting to sync / Conflict / Failed:** der Synchronisationszustand. Waiting to sync zeigt, dass die Änderung den Server noch nicht erreicht hat.

## Was Sie nicht tun können / Vorsicht
- Wer das Protokoll geschrieben oder zur Prüfung durch den Kapitän eingereicht hat, kann es nicht verifizieren.
- Das Ergebnis des Abgleichs ist keine Genehmigung; die Prüfung nimmt der Kapitän vor.
- Einen Datensatz abzuschließen und ihn zu verifizieren ist nicht dieselbe Handlung (13.03 §1).
- Offline wird eine nicht gespeicherte Änderung im Speicher dieser Seite gehalten und bei wiederhergestellter Verbindung mit ihrem ursprünglichen Schlüssel gesendet; lassen Sie die Seite geöffnet. Einreichen und Prüfen erfolgen online.
- Erscheint ein Konflikt, gehen Sie nicht davon aus, dass Ihr eigener Datensatz automatisch Vorrang hat (13.03 §6).

## Wenn etwas schiefgeht
- **Meldung „The server listed missing or blocking items.“:** Beseitigen Sie die Codes unter **Hindernisse**.
- **Meldung „The record changed or the command conflicts.“:** Verwenden Sie **Neuesten Stand laden**, um das aktuelle Protokoll zu prüfen; die zurückgehaltene lokale Änderung wird verworfen.
- **Meldung „The outcome is unknown.“:** **Erneut versuchen** sendet denselben Befehl erneut.
- **Konflikt:** Eine auf dieser Seite gehaltene Änderung wird niemals zusammengeführt; verwenden Sie **Neuesten Stand laden**. Konflikte bei Tageseinträgen, die unter **Schlepper-Offlinebetrieb** erfasst wurden, werden dort gelöst; sie werden auch in der **Zentrale für wesentliche Konflikte** aufgeführt (13.03 §10).

## Verwandte Seiten
