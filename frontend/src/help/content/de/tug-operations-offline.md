---
key: tug-operations-offline
title: Schlepper-Offlinebetrieb
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Diese Seite hält einen **vorab geladenen Snapshot** eines Schlepperauftrags auf dem Gerät und stellt drei Arten von Erfassungen, die ohne Verbindung gemacht werden, in eine **dauerhafte, verschlüsselte Warteschlange**: Bestätigungen von Briefings, Beobachtungen zur Durchführung und Einträge im Tagesprotokoll. Sie beantwortet: „Was kann ich ohne Verbindung sicher erfassen, was wartet in der Warteschlange und wie hat der Server es aufgenommen?“ Kapitänsfreigabe, Bereitschafts-GO, Start/Übergänge der Durchführung, Verifizierung und Prüfungen bleiben menschliche Online-Aufzeichnungen.

## Wer sie verwendet
- **Schlepperkapitän:** um kritische Arbeit bei schwacher Verbindung fortzusetzen und den Synchronisationsstatus zu verfolgen (13.03 §10).
- **Besatzung:** um Briefing-Bestätigungen und Wacheinträge offline vorzunehmen und Konflikte zu lösen, ohne davon auszugehen, dass der eigene Datensatz automatisch Vorrang hat (13.03 §6).

## Bevor Sie beginnen
- Öffnen Sie die Seite aus einem Schlepperauftrag; ohne Auftrag zeigt die Seite **Öffnen Sie diese Ansicht aus einem Schlepperauftrag, um den Snapshot vorab zu laden.**
- Die Zeile **Offline-Richtlinie** muss eine konfigurierte Speicherrichtlinie anzeigen (`CONFIGURED_UNVERIFIED`); ohne Richtlinie sind das Vorabladen des Snapshots und die Offline-Erfassung nicht verfügbar.
- Laden Sie den Snapshot **online** vorab. Prüfen Sie vor dem Erfassen, dass das Lease des Snapshots nicht abgelaufen ist.

## Schritt für Schritt
1. Drücken Sie online **Auftrags-Snapshot vorab laden**. Die Meldung **Snapshot auf diesem Gerät versiegelt.** erscheint.
2. Prüfen Sie unter **Auftrags-Snapshot** die Auftragsnummer, ihren Status und **Lease bis**.
3. Sind Sie Beteiligter eines ausgegebenen Briefings, wählen Sie unter **Briefing bestätigen** **Bestätigen** oder **Widersprechen** (mit einer Begründung von mindestens 10 Zeichen) und drücken Sie **Versiegeln und einreihen**.
4. Wählen Sie für eine laufende Durchführung unter **Ausführungsbeobachtung erfassen** die Durchführung und den Beobachtungstyp (Note, Towline connected, Towline released), schreiben Sie die Notiz und drücken Sie **Versiegeln und einreihen**.
5. Wählen Sie für ein Tagesprotokoll im Entwurf oder ein zurückgegebenes Tagesprotokoll das Protokoll unter **Tagesberichtseintrag**, schreiben Sie die **Tageszusammenfassung** und drücken Sie **Versiegeln und einreihen**.
6. Verfolgen Sie den Status jeder Erfassung unter **Wartende Erfassungen**. Erfassungen werden bei wiederhergestellter Verbindung der Reihe nach gesendet; zum manuellen Senden verwenden Sie **Wartende Erfassungen senden**.
7. Zeigt eine Erfassung **Wesentlicher Konflikt**, lesen Sie **Auf dem Server geändert**, wählen Sie ausdrücklich **Serverdatensatz behalten** oder **Meinen erfassten Wert auf den aktuellen Datensatz anwenden** und drücken Sie **Konflikt lösen**.

## Was die Zustände bedeuten
- **Auf Gerät in Warteschlange (nicht gesendet):** Die Erfassung ist auf diesem Gerät versiegelt; in der Warteschlange bedeutet nicht gesendet.
- **Vom Server angenommen:** Der Server hat die Erfassung erhalten. Dies ist weder eine Genehmigung noch eine Verifizierung.
- **Wesentlicher Konflikt:** Der Serverdatensatz hat sich nach der Erfassung geändert; er wird niemals automatisch zusammengeführt und wartet auf die Entscheidung einer Person.
- **Server value kept / Refused by server:** das Ergebnis des Servers für die Erfassung.
- **UNKNOWN (offline):** Der Richtlinienstatus konnte offline nicht gelesen werden; es wird keine Richtlinie angenommen.

## Was Sie nicht tun können / Vorsicht
- Nur online: Kapitänsfreigabe, Bereitschafts-GO, Start und Übergänge der Durchführung, Ausgabe des Briefings, Verifizierung, Einreichen und Prüfen des Tagesprotokolls.
- Wesentliche Konflikte werden niemals automatisch zusammengeführt; die Lösung ist immer eine ausdrückliche menschliche Wahl (13.03 §1).
- Mit einem abgelaufenen Snapshot-Lease kann keine neue Erfassung gemacht werden; laden Sie einen neuen Snapshot vorab.
- Bei der Wiedergabe wird jede Erfassung einmal mit ihrer ursprünglichen ID verarbeitet; legen Sie dieselbe Erfassung nicht erneut an.

## Wenn etwas schiefgeht
- **Meldung „The device key for this queue is unavailable.“:** Die erfassten Datensätze bleiben erhalten, können aber nicht gelesen werden; löschen Sie sie nicht und wenden Sie sich an den Support.
- **Meldung „Outcome unknown“:** Dieselbe Erfassung wird mit ihrer ursprünglichen ID erneut versucht; legen Sie keine neue an.
- **Meldung „The offline policy does not allow a snapshot.“:** Wenden Sie sich wegen der Richtlinienkonfiguration an Ihren Mandantenadministrator (13.03 §10).
- **Wesentlicher Konflikt:** Verwenden Sie das Formular **Konflikt lösen** auf dieser Seite; Konflikte werden auch in der **Zentrale für wesentliche Konflikte** aufgeführt (13.03 §10).

## Verwandte Seiten
