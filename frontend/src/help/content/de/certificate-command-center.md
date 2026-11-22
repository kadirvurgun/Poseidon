---
key: certificate-command-center
title: Zeugniszentrum
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: [1, 2]
reviewed: false
---
## Wofür diese Seite dient
Das Zeugniszentrum listet die Zeugnis**angaben** des ausgewählten Schiffs mit ihren angegebenen Kalenderzuständen auf. Es beantwortet: „Welche Zeugnisse dieses Schiffs nähern sich dem Ablauf oder einem Besichtigungsfenster, und welche Datensätze warten noch auf eine menschliche Prüfung?“ Die angezeigten Daten sind Angaben. Die Seite entscheidet nie, dass ein Zeugnis gültig oder verifiziert ist (13.03 §1, §8).

## Wer sie verwendet
- **Kapitän, Leitender Ingenieur / Technischer Inspektor:** beobachten den Zeugniskalender des Schiffs, erfassen neue Zeugnisentwürfe und starten die Vorbereitung einer Erneuerung.
- **Regulatory-/Assurance-Nutzer:** prüft, dass die Angaben auf einer amtlichen, versionierten Quelle beruhen, und erfasst die Feststellung eines bestellten Auditors (13.03 §8).
- Nutzer mit reinem Lesezugriff sehen nur die Liste und können sie nicht ändern.

## Bevor Sie beginnen
- Zum Anzeigen ist Lesezugriff auf Zeugnisse nötig. Zum Erfassen von Entwürfen benötigen Sie Schreibzugriff auf Zeugnisse und Bearbeitungsbereich für dieses Schiff; zum Extrahieren von Dokumentvorschlägen Lesezugriff auf Dateien und die Extraktionsberechtigung; zum Erfassen einer menschlichen Feststellung Schreibzugriff auf das Verzeichnis, die Berechtigung für Verifizierungsdatensätze und eine aktuelle Bestellung als **Auditor** für dieses Schiff und Zeugnis. Geschützte Besatzungsangaben erfordern zusätzlich Lesezugriff auf Besatzung, Besatzungszeugnisse, Besatzungsidentität und Dateien.
- Wählen Sie zuerst das richtige Schiff: Drücken Sie es in der Liste **Schiff auswählen** (13.03 §1).
- Die Seite benötigt einen Live-Lesezugriff online. Offline sehen Sie „Current certificate sources unavailable offline or hidden.“, und es wird keine Offline-Berechtigung abgeleitet.

## Schritt für Schritt
1. Öffnen Sie das Schiff aus der Liste **Schiff auswählen**.
2. Wählen Sie mit **Zeugnisbereich** die Option **Schiffszeugnisse** oder, falls Sie die Berechtigung haben, **Geschützte Besatzungsangaben**. Grenzen Sie die Liste mit **Filter für angegebene Kalenderdaten** ein.
3. Öffnen Sie einen Datensatz, indem Sie seine Zeile drücken. Zeilen tragen die Kennzeichnung **Ungeprüfte Zeugnisangabe**.
4. Für einen neuen Datensatz drücken Sie **Neuer Zeugnisentwurf**. Füllen Sie **Dokumentname**, **Angegebene regulatorische Grundlage**, **Angegebene Flagge**, das Besichtigungsfenster und **Angegebene Verfügbarkeit** anhand des Quelldokuments aus und drücken Sie dann **Ungeprüften Entwurf speichern**.
5. Um Vorschläge aus dem verknüpften Dokument zu erhalten, drücken Sie unter **Dokumentfakten prüfen** die Schaltfläche **Aus verknüpftem Dokument extrahieren**. Vergleichen Sie jeden Vorschlag mit der Quelle, kreuzen Sie **Ich habe diesen Fakt mit der Quelle abgeglichen** und **Ich habe geprüft, dass dieses Dokument zu diesem Schiff und Zeugnis gehört** an und drücken Sie dann **Ausgewählte Fakten bestätigen und Entwurf speichern**.
6. Als bestellter Auditor füllen Sie unter **Datensatz einer menschlichen Feststellung** die Felder **Tatsächliche Zuweisung auswählen** und **Erfasste Aussage auswählen** aus, geben die Begründung ein und drücken dann **Unbewertete Feststellung erfassen**.
7. Um eine Erneuerung zu starten, verwenden Sie im Datensatz unter **Zeugnisbezogene Arbeiten** die Schaltfläche **Erneuerung vorbereiten**. Damit gelangen Sie zum Ablauf [Zeugniserneuerung](/app/renewal-workflow).

## Was die Zustände bedeuten
- **Declared expiry <90 days / <60 days / <30 days / Declared expiry passed:** eine Kalenderbeobachtung anhand des angegebenen Ablaufdatums. Sie ist keine Entscheidung über die Gültigkeit.
- **Declared survey window open / Beyond due window:** die Lage relativ zum angegebenen Besichtigungsfenster.
- **Kalenderprüfung erforderlich:** Ein Datum fehlt, ist mehrdeutig oder widersprüchlich. Der Datensatz ist nicht bereit, bis eine Person ihn mit dem Quelldokument abgleicht.
- **Unbekannt:** Der Wert wurde nicht angegeben. Ein unbekannter Wert bedeutet nie „nicht fehlend“ oder „konform“; er muss geprüft werden.
- **Low confidence · review / Ambiguous · review / Prüfung erforderlich:** ein Dokumentvorschlag, der noch nicht von einer Person bestätigt wurde. Ein Konfidenzwert ist keine Verifizierung.
- „This page only. Calendar observations are separate from qualification; these are not fleet-wide readiness totals.“ (Nur diese Seite. Kalenderbeobachtungen sind von der Qualifikation getrennt; dies sind keine flottenweiten Bereitschaftssummen.)

## Was Sie nicht tun können / Vorsicht
- Das Speichern eines Entwurfs verifiziert das Zeugnis nicht. Das Schreiben des Datensatzes, das Bestätigen von Dokumentvorschlägen und die Feststellung des Auditors sind getrennte Schritte; Abgeschlossen ≠ Verifiziert.
- Die automatische Extraktion macht nur Vorschläge. Fehlende Werte bleiben unbekannt, und nur die Fakten, die Sie auswählen und korrigieren, aktualisieren den Entwurf.
- Eine Administratorrolle allein erlaubt nicht das Erfassen einer menschlichen Feststellung; eine aktuelle Bestellung und die Quelldatei sind erforderlich.
- Ein gespeichertes Zeugnis wird nie stillschweigend geändert. Jedes Speichern erzeugt eine neue Revision, und ältere Revisionen bleiben unter **Erfasster Revisionsverlauf** erhalten (13.03 §1).
- Die Seite erteilt keine Klassen-, Flaggen- oder Behördengenehmigung und entscheidet nicht über die Reisetauglichkeit. Diese Entscheidung liegt bei berechtigten Personen.

## Wenn etwas schiefgeht
- **Zeugnisquelle oder Zugriff nicht verfügbar. Daraus wird kein gültiger, leerer oder verifizierter Bestand abgeleitet.** Drücken Sie **Quellenlesen wiederholen**. Besteht das Problem weiter, prüfen Sie Ihre Rolle sowie Ihren Schiffs- und Objektbereich; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).
- **Outcome unknown.** Behalten Sie den ursprünglichen Befehl bei und senden Sie dieselbe Anfrage mit **Exakten Zeugnisbefehl wiederholen** erneut.
- **Current source changed.** Ihre lokalen Felder bleiben erhalten; verwenden Sie **Zurück zu den Datensätzen**, lesen Sie den aktuellen Datensatz erneut und wiederholen Sie Ihre Änderung.
- **Automatic reading is not available for this document:** Geben Sie die Werte manuell anhand des Quelldokuments ein.
- Kann ein Datensatz nicht verifiziert werden, prüfen Sie seinen Nachweis und seine Quellenherkunft und verweisen Sie ihn an den Verifizierer (13.03 §10).

## Verwandte Seiten
