---
key: defects
title: Mängel und Schutzmaßnahmen
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite für Mängel und vorübergehende Maßnahmen enthält die **erfassten Mängel, Einschränkungen und angegebenen vorübergehenden Maßnahmen** des ausgewählten Fahrzeugs. Sie beantwortet: „Wie kritisch ist dieser Mangel, welche Schutzmaßnahme oder betriebliche Einschränkung ist erfasst, wurde die Korrekturmaßnahme verifiziert, und was verhindert noch den Abschluss?“ Jeder Schritt ist eine menschliche Aufzeichnung; die Seite erteilt keine Sicherheits- oder Betriebsbefugnis.

## Wer sie verwendet
- **Leitender Ingenieur / Technischer Inspektor:** erfasst die Schutzmaßnahme oder Einschränkung und die Auswirkung eines kritischen Mangels auf die Bereitschaft und verfolgt die Korrekturmaßnahme (13.03 §5).
- **Kapitän:** sieht die Auswirkung eines Mangels auf betriebliche Einschränkungen (13.03 §2).
- **Ingenieur / Besatzung:** gibt einen gefundenen Mangel an (13.03 §6).

## Bevor Sie beginnen
- Zum Lesen benötigen Sie `defect.read` und `equipment.read`. Angaben und Lebenszyklus-Einträge erfordern `defect.write`.
- Die Verifizierung erfordert `defect.verify`, die Wirksamkeit `defect.effectiveness` und der Abschluss `defect.close`, jeweils mit einem angemeldeten menschlichen Benutzer.
- Wählen Sie das richtige Fahrzeug unter **Schiff oder Schlepper auswählen**.

## Schritt für Schritt
1. Wählen Sie das Fahrzeug. Filtern Sie bei Bedarf **Erfasste Angaben** mit **Zurückgegebene Ablaufbeobachtungen filtern** und öffnen Sie eine Angabe.
2. Für einen neuen Mangel drücken Sie **Neue Mangelangabe**, füllen die Felder aus und drücken **Unbewertete Angabe speichern**. Unvollständige Entwürfe können gespeichert werden.
3. Drücken Sie bei einer gespeicherten Angabe im Abschnitt **Mangel-Lebenszyklus** auf **Lebenszyklus starten**.
4. Wählen Sie mit **Bewertung erfassen** die Kritikalität und schreiben Sie die Aussage (mindestens 10 Zeichen).
5. Verwenden Sie **Schutzmaßnahme oder Einschränkung erfassen** und wählen Sie als **Art der Maßnahme** entweder **Schutzmaßnahme** oder **Betriebliche Einschränkung**. Geben Sie bei einer vorübergehenden Maßnahme den Ablauf (UTC) ein.
6. Beschreiben Sie mit **Korrekturmaßnahme erfassen** die ausgeführte Arbeit.
7. Eine berechtigte Person, die keine Korrekturmaßnahme erfasst hat, erfasst **Verifizierung erfassen** und danach **Wirksamkeit erfassen**.
8. Wenn keine **Abschlusshindernisse** mehr bestehen, erfasst eine berechtigte Person, die ebenfalls keine Korrekturmaßnahme erfasst hat, **Schließen und wieder in Betrieb nehmen**. Die Einträge erscheinen unter **Lebenszyklus-Einträge**.

## Was die Zustände bedeuten
- **Detect / Assess / Safeguard / Action / Verify / Effectiveness:** die Stufe des Lebenszyklus.
- **Geschlossen (erfasste menschliche Entscheidung):** Der Abschluss ist eine menschliche Entscheidung; er ist kein grünes Ergebnis „behoben“.
- **Kritisch-Regel gilt:** Ein kritischer Mangel kann ohne Schutzmaßnahme nicht geschlossen werden.
- **Active / Expired — blocking / Lifted / Ended at closure:** der Zustand einer Schutzmaßnahme oder Einschränkung. Eine abgelaufene vorübergehende Maßnahme muss erneuert oder aufgehoben werden.
- **Critical defect without an active safeguard / Criticality unknown and no safeguard:** Warnungen. Diese Mängel erscheinen auch unter **Zustandswarnungen** im [PMS-Kommandozentrum](/app/pms-command-center).
- **Unbekannt:** Die Kritikalität wurde nicht bewertet. Unbekannt bedeutet weder „Niedrig“ noch „Nicht kritisch“.

## Was Sie nicht tun können / Vorsicht
- Wenn Sie die Korrekturmaßnahme erfasst haben, können Sie weder Verifizierung noch Wirksamkeit noch Wiederinbetriebnahme erfassen; diese erfordern eine eigene Befugnis und eine andere Person.
- Verifiziert, Wirksam und Geschlossen sind getrennte menschliche Aufzeichnungen. Eine angegebene Stufe oder Maßnahme genehmigt weder den Betrieb noch verifiziert sie die Wirksamkeit noch erlaubt sie den Abschluss.
- Ändert sich die Angabe nach der Verifizierung, wird der Abschluss blockiert, und der Datensatz geht zurück in die Prüfung.
- Die Kritikalität der Anlage wird nicht als Bewertung dieses Mangels übernommen.
- Offline sind die aktuellen Mängelquellen nicht verfügbar; es gibt keine Offline-Speicherung.

## Wenn etwas schiefgeht
- **Jemand anderes hat diesen Datensatz geändert:** Verwenden Sie **Neu laden**, um den aktuellen Datensatz zu lesen, und erfassen Sie erneut.
- **Das Ergebnis des Speicherns ist unbekannt:** **Denselben Befehl wiederholen** sendet denselben Befehl.
- **Sie haben nicht die Serverberechtigung für diese Aktion oder diesen Datensatz:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Abschlusshindernisse:** Ergänzen Sie den fehlenden Eintrag in der Liste. Kann ein Datensatz nicht verifiziert werden, prüfen Sie den Nachweis mit dem Verifizierer (13.03 §10).

## Verwandte Seiten
