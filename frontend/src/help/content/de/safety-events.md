---
key: safety-events
title: Sicherheitsereignisse und Beinaheunfälle
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: [1]
reviewed: false
---
## Wofür diese Seite dient
Auf der Seite Sicherheitsereignisse und Beinaheunfälle melden Sie ein Sicherheitsereignis auf einem Fahrzeug schnell und verfolgen die menschlichen Schritte, die darauf folgen: Triage, Untersuchung, unabhängige Prüfung, Korrektur- und Vorbeugemaßnahmen, Verifizierung, Wirksamkeit und Abschluss. Sie beantwortet: „Was ist passiert, wer bearbeitet es, und hat die Maßnahme tatsächlich gewirkt?“ Hier wird weder Schweregrad noch Grundursache noch Abschluss automatisch entschieden; jedes davon ist ein eigener menschlicher Datensatz. Die Seite folgt der Kette Feststellung → Maßnahme → Verifizierung, und **abgeschlossen ist nicht dasselbe wie wirksam** (13.03 §4).

## Wer sie verwendet
- **Crew / OOW / Ingenieur und Kapitän:** melden Beinaheunfälle, Vorfälle, Unfälle, Ermüdung sowie Umwelt- oder Cyberereignisse (13.03 §2, §6).
- **DPA / HSEQ:** beobachtet als Sicherheitsbrücke kritische Ereignisse und überfällige Maßnahmen und steuert Untersuchung und Maßnahmenwirksamkeit (13.03 §4).
- Triage, Untersuchung, Prüfung, Lesen der Identität, Schreiben von Maßnahmen, Verifizierung und Wirksamkeitsbewertung sind getrennte Berechtigungen; jeder Benutzer sieht nur die Teile, für die er berechtigt ist.

## Bevor Sie beginnen
- Wählen Sie das richtige Fahrzeug (**Schiff auswählen**). Ohne Zugriff sehen Sie „Sie haben keinen Zugriff auf Sicherheitsereignisse dieses Schiffes.“
- Zum Melden benötigen Sie die Meldeberechtigung; zum Lesen der Liste die Leseberechtigung.
- Eine **Vertraulich**-Meldung und **Meldung absenden** benötigen eine Verbindung. Offline können Sie nur einen namentlichen Entwurf speichern, und nur auf einem vorab vorbereiteten Gerät.
- Zeiten werden in UTC eingegeben.

## Schritt für Schritt
1. Sehen Sie die Ereignisse des Fahrzeugs in der Liste **Ereignisse**. Mit **Nur meine Meldungen** sehen Sie nur Ihre eigenen.
2. Für einen neuen Datensatz drücken Sie **Ereignis melden**. Wählen Sie als **Meldeart** **Namentlich** oder **Vertraulich**. Für eine vertrauliche Meldung, die eine Nachverfolgung braucht, wählen Sie **Privaten Nachverfolgungscode erhalten**.
3. Füllen Sie **Ereignisart**, **Wann ist es passiert?**, **Betrieb**, **Ort an Bord**, **Zusammenfassung**, **Was ist passiert**, **Wurden Sofortmaßnahmen ergriffen?** und optional **Ihre Einschätzung des Schweregrads** aus. Ist die Zeit nicht bekannt, wählen Sie **Unbekannt** und füllen **Warum ist die Zeit unbekannt?** aus.
4. Mit **Entwurf speichern** können Sie auch unvollständig speichern. Wenn **Vor dem Einreichen noch erforderlich** leer ist, drücken Sie **Meldung absenden**. Ein nicht eingereichter Entwurf kann mit **Entwurf zurückziehen** und einer Begründung zurückgezogen werden.
5. Wurde ein Nachverfolgungscode ausgegeben, bewahren Sie den Code aus **Ihr vertraulicher Nachverfolgungscode** sofort privat auf und drücken **Ich habe ihn gespeichert**. Er wird nur einmal angezeigt. Später geben Sie ihn unter **Vertrauliche Nachverfolgung** als **Nachverfolgungscode** ein und drücken **Öffnen**, um Antworten des Teams zu lesen.
6. Ein Triagierender erfasst in **Triage** den Schweregrad und **Untersuchung erforderlich** mit Begründung und drückt **Triage erfassen**, oder er gibt die Meldung mit **An Meldenden zurückgeben** zurück.
7. Ein Untersuchender drückt **Untersuchung eröffnen**, füllt **Beitragende Faktoren**, **Grundursachen**, **Barrieren** und **Unsicherheiten** aus und drückt dann **Untersuchung speichern** und **Zur Prüfung einreichen**. In **Unabhängige Prüfung** wählt eine andere Person **Genehmigen**, **Zurückgeben** oder **Ablehnen**.
8. Verwenden Sie in **Korrektur- und Vorbeugemaßnahmen** **Maßnahme hinzufügen** mit einem Verantwortlichen, einem Fälligkeitsdatum und der Feststellung, auf die sie sich bezieht. Der Verantwortliche erfasst **Erledigung melden**, eine andere Person erfasst **Verifizieren**, und eine dritte erfasst **Wirksamkeit bewerten**.
9. Wenn **Abschlusshindernisse** leer ist, drückt ein Prüfer (Prüfberechtigung), der weder der Meldende noch ein Verfasser der Untersuchung ist, **Ereignis abschließen**. In **Nachrichten** können Sie Nachrichten mit der Sichtbarkeit **Nur Team** oder **Mit Meldendem geteilt** senden.

## Was die Zustände bedeuten
- **Entwurf / Eingereicht / Zurückgegeben / Triagiert / In Untersuchung / Maßnahmenverfolgung / Abgeschlossen / Zurückgezogen:** der Lebenszyklus des Ereignisses.
- **Schweregrad — Nicht triagiert / Nicht bewertet:** noch keine menschliche Einstufung. Die **Einschätzung des Meldenden** ist nur ein Vorschlag. **Severity policy: not configured – human classification only** zeigt, dass das System keinen Schweregrad ableitet.
- **Untersuchungsbedarf ist UNKNOWN:** Es wurde keine Entscheidung getroffen; das bedeutet nicht „nein“ und blockiert den Abschluss.
- **Maßnahme — Offen / Abgeschlossen / Verifiziert:** Abgeschlossen ist nicht Verifiziert, und Verifiziert ist nicht Wirksam.
- **Wirksamkeit — Ausstehend / Wirksam / Nicht wirksam / Nicht eindeutig / Keine Maßnahmen / Nicht sichtbar:** Die Wirksamkeit ist eine menschliche Bewertung je Maßnahme und vom Abschluss getrennt. **Keine Maßnahmen** bedeutet nicht „Wirksam“.
- Übersicht **Offene Sicherheitsereignisse**: **Ereignisse mit hohem Schweregrad offen**, **Prüfung erforderlich** oder **Kein offenes Ereignis mit hohem Schweregrad**. Sie ist nur eine Warnung; sie trifft keine Bereitschaftsentscheidung.
- **Vertraulich (geschützt):** Die Identität des Meldenden liegt in einem geschützten Tresor und wird Lesenden nicht angezeigt.
- **Offline erfasst / Wartet auf Synchronisierung / Auf diesem Gerät gespeichert. Noch nicht gesendet.** In der Warteschlange ist nicht gesendet.

## Was Sie nicht tun können / Vorsicht
- Ein Meldender kann seine eigene Meldung nicht triagieren. Ein Verfasser der Untersuchung, der Einreicher oder der Meldende kann die unabhängige Prüfung nicht durchführen.
- Nur der Maßnahmenverantwortliche meldet die Erledigung. Weder der Verantwortliche noch der Erledigende kann verifizieren; weder der Verantwortliche noch der Erledigende noch der Verifizierer kann die Wirksamkeit bewerten. Dies sind getrennte Datensätze verschiedener Personen.
- Grundursachen sind menschliche Aussagen; das System schlägt nie eine vor.
- Eine eingereichte Meldung kann von ihrem Verfasser nie zurückgezogen oder gelöscht werden.
- Bei einer vertraulichen Meldung wird die Identität nur nach dem Need-to-know-Prinzip geöffnet, von jemandem mit der Berechtigung zum Lesen der Identität, der einen **Zweck** wählt; jedes Lesen wird mit seinem Zweck erfasst. Anonyme Meldungen benötigen den öffentlichen Meldekanal, der nicht konfiguriert ist.
- Eine Bewertung **Nicht eindeutig** kann wiederholt werden; **Wirksam** und **Nicht wirksam** sind endgültig; eine Maßnahme, die nicht gewirkt hat, braucht eine neue Folgemaßnahme.

## Wenn etwas schiefgeht
- **Der Server hat den Befehl abgelehnt:** Ergänzen Sie die darunter aufgeführten fehlenden oder blockierenden Punkte.
- **Der Datensatz wurde zwischenzeitlich geändert. Neu laden und prüfen, bevor Sie es erneut versuchen.** Drücken Sie **Neu laden**.
- **Das Ergebnis ist unbekannt (Verbindung verloren).** Verwenden Sie **Denselben Befehl wiederholen**; er wird nicht doppelt angewendet.
- **Diese Aktion ist für Sie nicht zulässig.** Prüfen Sie Ihre Rolle sowie Ihren Fahrzeug- und Objektkontext; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).
- **Der Geräteschlüssel ist nicht verfügbar** oder **Online-Neuautorisierung erforderlich:** Erfasste Daten bleiben erhalten; autorisieren Sie sich online erneut. Bei einem Anwendungsfehler wenden Sie sich mit der Korrelations-ID, dem Bildschirm, der Uhrzeit und Ihren Schritten an den Support (13.03 §10).

## Verwandte Seiten
