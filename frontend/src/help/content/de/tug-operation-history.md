---
key: tug-operation-history
title: Schlepperhistorie
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Einsatzhistorie zeigt die **schreibgeschützte Historie** von Schlepperaufträgen, Durchführungen, Tagesprotokollen und Nachweisen. Sie beantwortet: „Welche Aufträge endeten mit welchem Ergebnis, wurden die Durchführungen verifiziert, gibt es einen Nachweisdatensatz und welche Tagesprotokolle gehören zu diesen Aufträgen?“ Stornierte und abgebrochene Datensätze bleiben sichtbar; nichts wird gelöscht oder umbenannt.

## Wer sie verwendet
- **Koordinator Schlepperbetrieb / Landbetrieb:** um die Vollständigkeit der Nachweise sowie Ereignis- und Historiendatensätze abgeschlossener Aufträge zu verfolgen (13.03 §7).
- **Schlepperkapitän:** um frühere Aufträge und Durchführungszeitverläufe des eigenen Schleppers zu sehen (13.03 §3).

## Bevor Sie beginnen
- Öffnen Sie den Schlepper über **Schlepperbetrieb**; ohne ausgewählten Schlepper zeigt die Seite **Wählen Sie einen Schlepper, um fortzufahren.**
- Lesezugriff auf Schlepper genügt; die Seite ändert keine Daten.
- Die Historie wird vom Server gelesen; offline kann sie nicht aktualisiert werden.

## Schritt für Schritt
1. Setzen Sie unter **Filter** optional **Schlepper-ID**, **Von (UTC-Datum)**, **Bis (UTC-Datum)** und **Ergebnis** und drücken Sie dann **Filter anwenden**. Der Datumsbereich beträgt höchstens 400 Tage.
2. Lesen Sie in jeder Tabellenzeile **Auftrag** (Auftragsnummer, unterstütztes Schiff, Auftragstyp, Status von Plan und Briefing), **Aktivitätszeit**, **Ergebnis** und **Nachweis**.
3. Sehen Sie unter **Ausführungen** für jede Durchführung Status, Verifizierungsstatus, Dauer, Kapitänsaufzeichnung und Anzahl der Nachweiseinträge; verwenden Sie **Ausführungszeitleiste öffnen**, um zur Durchführungskonsole zu wechseln.
4. Verwenden Sie die Links unter **Tagesberichte**, um das zugehörige Protokoll zu öffnen. Der Hinweis **vom Bericht nicht verknüpft** zeigt, dass das Protokoll diese Durchführung nicht verknüpft.
5. Verwenden Sie **Mehr laden** für ältere Datensätze. **Gelesen am** zeigt, wann die Daten gelesen wurden.

## Was die Zustände bedeuten
- **Not started / In progress / Completed (recorded) / Aborted / Cancelled / Mixed:** das Gesamtergebnis des Auftrags. Completed (recorded) bedeutet nicht, dass der Auftrag verifiziert wurde.
- **Verified, evidence recorded / Verified, no evidence entry / Verification rejected / Evidence unverified / Unknown:** der Nachweis- und Verifizierungsstatus.
- **verification pending / verified / verification rejected:** der Verifizierungsstatus einer einzelnen Durchführung.
- **Keine:** Für diesen Auftrag gibt es keinen verknüpften Datensatz. Das bedeutet nicht, dass kein Datensatz erforderlich war.

## Was Sie nicht tun können / Vorsicht
- Auf dieser Seite wird kein Datensatz bearbeitet, gelöscht oder verifiziert.
- Kein stilles Löschen: Stornierte Aufträge und abgebrochene oder abgelehnte Durchführungen bleiben in der Historie.
- Abgeschlossen ≠ Verifiziert: Die Verifizierung ist die Aufzeichnung einer anderen Person.
- Die angezeigten Daten entsprechen dem Stand zum Zeitpunkt **Gelesen am**; offline werden sie nicht aktualisiert.

## Wenn etwas schiefgeht
- **Meldung „The filter is not valid“:** Prüfen Sie, ob die Schlepper-ID eine UUID ist und der Datumsbereich 400 Tage nicht überschreitet.
- **Meldung „The history could not be loaded.“:** Prüfen Sie Ihre Verbindung und drücken Sie **Erneut versuchen**.
- **„You do not have access to this tug scope.“:** Prüfen Sie Ihre Rolle und Ihren Schlepperkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Datensatz kann nicht verifiziert werden:** Prüfen Sie die Nachweisreferenz und ihre Quelle im Durchführungszeitverlauf; wenden Sie sich an den Verifizierer (13.03 §10).

## Verwandte Seiten
