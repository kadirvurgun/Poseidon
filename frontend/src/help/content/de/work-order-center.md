---
key: work-order-center
title: Arbeitsaufträge
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Arbeitsauftragszentrale enthält die **Arbeitsauftragsentwürfe und angegebenen Ausführungsdatensätze** des ausgewählten Fahrzeugs. Sie beantwortet: „Welche Arbeitsaufträge gibt es auf diesem Fahrzeug, und welche wurden freigegeben, ausgeführt, mit eingereichten Nachweisen versehen und von einer Person verifiziert?“ Jede gespeicherte Revision bleibt in einem unveränderlichen Verlauf erhalten. Verifizierung und Abschluss sind weder eine Wiederinbetriebnahme noch eine Erlaubnis oder Risikofreigabe.

## Wer sie verwendet
- **Leitender Ingenieur / Technischer Inspektor:** um geplante und ungeplante Arbeitsaufträge zu eröffnen, freizugeben, zu verifizieren und abzuschließen (13.03 §5).
- **Ingenieur / Besatzung:** um zugewiesene Arbeit zu beginnen und Messwerte, Ersatzteile, Foto- und Dokumentnachweise zu erfassen und einzureichen (13.03 §6).

## Bevor Sie beginnen
- Zum Lesen benötigen Sie `workorder.read`, `equipment.read` und `maintenance.read`. Entwürfe erfordern `workorder.write`, die Freigabe `workorder.release`, die Ausführung `workorder.execute`, die Verifizierung `workorder.verify` und der Abschluss `workorder.close`.
- Freigabe, Verifizierung und Abschluss erfordern einen angemeldeten menschlichen Benutzer.
- Wählen Sie das richtige Fahrzeug unter **Schiff oder Schlepper auswählen**.

## Schritt für Schritt
1. Wählen Sie das Fahrzeug. Filtern Sie unter **Erfasste Arbeitsaufträge** diese Seite mit **Zurückgegebene Arbeitsauftragsnummern suchen** und öffnen Sie einen Datensatz.
2. Drücken Sie für neue Arbeit **Neuer Arbeitsauftragsentwurf**, füllen Sie die Felder aus und drücken Sie **Unbewerteten Entwurf speichern**. Unvollständige Entwürfe können gespeichert werden.
3. Wählen Sie unter **Ausführung und Nachweise**, sofern Sie Freigabeberechtigung haben, bei **Richtlinie für erforderliche Nachweise** die Option **Erforderliche Nachweise festlegen**, markieren Sie die Nachweisarten und drücken Sie **Arbeitsauftrag freigeben**. Geben Sie frei, ohne festzulegen, bleibt die Verifizierung blockiert, bis ein Freigebender **Erforderliche Nachweise ändern** → **Erforderliche Nachweise speichern** verwendet.
4. Der Ausführende drückt **Arbeit beginnen**, füllt **Messwerte**, **Verbrauchte Ersatzteile**, **Foto- und Dokumentnachweise**, **Geleistete Arbeit** und **Befunde** aus, drückt **Nachweise speichern** und dann **Nachweise einreichen**.
5. Ein berechtigter Verifizierer, der die Arbeit nicht ausgeführt hat, prüft die Nachweise. Er drückt **Verifizierung erfassen** oder wählt einen **Rückgabegrund** und drückt **An Ausführenden zurückgeben**.
6. Für einen verifizierten Arbeitsauftrag drückt eine berechtigte Person **Abschluss erfassen**.
7. Sehen Sie frühere Revisionen unter **Unveränderlicher Arbeitsauftragsverlauf**. Ist ein Ausfallzeitdatensatz erforderlich, verwenden Sie **Verknüpften Ausfallzeitentwurf öffnen**.

## Was die Zustände bedeuten
- **Not released for execution. / Released / In progress:** Die Arbeit ist noch nicht freigegeben, ist freigegeben oder wird ausgeführt.
- **Nachweise eingereicht:** Der Ausführende hat die Arbeit und die Nachweise angegeben. Das bedeutet nicht verifiziert.
- **Verifiziert (menschliche Aufzeichnung):** erfasst von einem berechtigten Verifizierer, der die Arbeit nicht ausgeführt hat. **Abgeschlossen (menschliche Aufzeichnung)** ist ein separater Schritt.
- **Required evidence missing / Present, pending human verification / Verified by a human:** der Zustand der Prüfung der erforderlichen Nachweise.
- **Prüfung erforderlich: Richtlinie nicht konfiguriert:** Es wurden keine erforderlichen Nachweise festgelegt. Nicht konfiguriert bedeutet niemals, dass nichts erforderlich ist; die Verifizierung bleibt blockiert.
- **Vom Verifizierer zurückgegeben:** Die Arbeit ging mit dem angezeigten Grund an den Ausführenden zurück.

## Was Sie nicht tun können / Vorsicht
- Haben Sie die Arbeit ausgeführt, können Sie sie nicht verifizieren oder abschließen. Die Seite zeigt „Sie haben diese Arbeit ausgeführt und können sie daher nicht verifizieren oder abschließen.“
- Nachweise eingereicht, Verifiziert und Abgeschlossen sind separate menschliche Aufzeichnungen. Die Seite erklärt Ausrüstung niemals für betriebsbereit.
- Die Zusammenfassung **Gespeicherte Arbeitsauftragsquelle** beschreibt die gespeicherten Entwurfsfelder und hält fest: „Release, evidence recording, independent verification and closure are recorded in the execution panel. Submitted evidence is accepted only by independent human verification, never by the person who did the work; there is no separate evidence-acceptance step, and verification is not a return-to-service decision.“ Diese Aktionen werden unter **Ausführung und Nachweise** vorgenommen.
- Messwerte werden wie angegeben gespeichert; es wird keine Einheitenumrechnung und kein Schwellenwert angewendet.
- Die Offline-Nachweiswarteschlange funktioniert nur, wenn eine genehmigte Offline-Richtlinie besteht und die Arbeit läuft. Konflikte werden niemals automatisch zusammengeführt; verwenden Sie **Konfliktzentrum öffnen**, und eine Person entscheidet.

## Wenn etwas schiefgeht
- **Der Datensatz hat sich geändert oder diese Aktion ist in seinem aktuellen Zustand nicht zulässig:** Verwenden Sie **Neu laden**, um den aktuellen Datensatz zu lesen, und versuchen Sie es erneut.
- **Ergebnis unbekannt:** **Denselben Befehl wiederholen** sendet denselben Befehl; es wird kein neuer Datensatz angelegt.
- **Berechtigung oder menschliche Befugnis fehlt:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Die Prüfung der erforderlichen Nachweise hat diese Aktion abgelehnt:** Vervollständigen Sie die fehlenden Nachweisarten. Kann ein Datensatz nicht verifiziert werden, prüfen Sie den Nachweis und seine Herkunft mit dem Verifizierer (13.03 §10).

## Verwandte Seiten
