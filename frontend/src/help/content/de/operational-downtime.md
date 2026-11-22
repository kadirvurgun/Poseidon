---
key: operational-downtime
title: Betriebliche Ausfallzeit
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Diese Seite erfasst die **angegebenen betrieblichen Unterbrechungen** (Ausfallzeiten) eines Schiffs zusammen mit ihrer Quellenherkunft. Sie beantwortet: „Welche Unterbrechung wurde für dieses Schiff angegeben, wann, aus welcher Ursache und mit welchem Mangel oder Arbeitsauftrag verknüpft, und wo stehen ihre Prüfung und ihr Abschluss?“ Die Seite entscheidet nicht über einen Loss-of-Hire-Anspruch, den sicheren Betrieb oder irgendeine Sicherheitsfreigabe.

## Wer sie verwendet
- **Leitender Ingenieur / Technischer Inspektor:** um Ausfallzeiten durch einen technischen Ausfall oder Instandhaltung zu erfassen und mit der Mangel- oder Arbeitsauftragshistorie zu verknüpfen (13.03 §5).
- **Kapitän:** um die betrieblichen Auswirkungen einer Ausfallzeit zu verfolgen und sie bei Bestellung zu prüfen (13.03 §2).
- **Bestellter Prüfer (Kapitän, Erster Offizier, Leitender Ingenieur, Flottenmanager, DPA):** um einen Ausfallzeitdatensatz zu verifizieren, zur Korrektur zurückzugeben oder abzuschließen.

## Bevor Sie beginnen
- Prüfen Sie in der oberen Kontextleiste, dass das **richtige Unternehmen** ausgewählt ist; die Seite listet nur Schiffe in Ihrem Zugriffsbereich.
- Zum Lesen benötigen Sie Lesezugriff auf Ausfallzeiten; zum Öffnen eines Entwurfs Schreibzugriff. Prüfaktionen benötigen zusätzlich Prüfzugriff und eine aktuelle Dienstbestellung für dieses Schiff.
- Um eine verknüpfte Ausfallzeit zu öffnen, können Sie auf einer Mangel- oder Arbeitsauftragsseite **Verknüpften Ausfallzeitentwurf öffnen** verwenden.

## Schritt für Schritt
1. Wählen Sie das Schiff aus der Liste **Schiff auswählen**. Die Liste **Erfasste betriebliche Unterbrechungen** öffnet sich.
2. Mit **Ursachen auf dieser Seite suchen** grenzen Sie die Datensätze auf dieser Seite ein.
3. Für einen neuen Datensatz drücken Sie **Neuer Ausfallzeitentwurf**.
4. Wählen Sie für jedes Feld zuerst den Wertzustand: **Fehlend**, **Unbekannt** oder **Angegebener Wert**. Einen Wert geben Sie nur ein, solange „Angegebener Wert“ ausgewählt ist.
5. Geben Sie in **Unterbrechungsbeginn** und **Unterbrechungsende** einen vollständigen Zeitstempel mit Sekunden und ausdrücklichem UTC-Versatz ein.
6. Drücken Sie **Unbewerteten Entwurf speichern**. Ein unvollständiger Entwurf kann gespeichert werden.
7. Verknüpfen Sie in **Ausfallzeit – Prüfung und Abschluss** bei Bedarf Anspruchsnachweise mit **Nachweis wählen** und drücken Sie dann **Zur Prüfung einreichen**.
8. Wenn Sie bestellter Prüfer sind, wählen Sie Ihre **Bestellung** und verwenden Sie **Verifizieren**, **Zur Korrektur zurückgeben** oder **Schließen**.
9. Frühere Revisionen sehen Sie unter **Unveränderlicher Entwurfsverlauf**.

## Was die Zustände bedeuten
- **Entwurf / Prüfung erforderlich:** Der erfasste Inhalt ist unbewertet. Die Gründe stehen unter **Warum dieser Entwurf unbewertet bleibt**.
- **Nicht eingereicht / Offen / In Prüfung:** der Lebenszykluszustand des Datensatzes.
- **Verifiziert (Mensch):** Eine bestellte Person, die nicht der Erfasser ist, hat ihn verifiziert. Abgeschlossen ist nicht Verifiziert (13.03 §1).
- **Abgeschlossen:** Die Ausfallzeit wurde abgeschlossen. Ein verifizierter Abschluss ist keine Sicherheitsfreigabe.
- **Unbekannt:** Der Wert wurde nicht angegeben oder konnte nicht bestimmt werden. Unbekannt bedeutet weder „Nein“ noch „Nicht zutreffend“. Deshalb bleiben Loss of Hire und Abschluss „Unbekannt“.
- **Loss-of-Hire-Kandidat:** nur ein Kandidat; er nennt weder einen Anspruch noch einen Betrag.
- **Erfasstes Intervall:** eine unbewertete technische Beobachtung; sie ersetzt nicht die angegebenen Minuten.

## Was Sie nicht tun können / Vorsicht
- Wer die Ausfallzeit erfasst hat, kann sie weder verifizieren noch abschließen; das muss eine andere bestellte Person tun.
- Einreichen, Verifizieren und Abschließen sind nicht möglich, solange erforderliche Angaben fehlen.
- Das Verknüpfen eines Anspruchsnachweises ist keine Verifizierung.
- Aus der zugehörigen Quelle (Mangel, Arbeitsauftrag) werden weder Beschreibung noch Zeit noch Abschlussentscheidung übernommen; nur die exakte Revisionsverknüpfung wird gespeichert.
- Offline können aktuelle Quellen nicht gelesen werden, und es gibt keine Offline-Speicherung; Ihre Eingaben bleiben nur in der geöffneten Seite.

## Wenn etwas schiefgeht
- **Das Ergebnis des Speicherns ist unbekannt:** Ihr ursprünglicher Befehl ist geschützt. Verwenden Sie mit demselben Konto **Exakten Ausfallzeitbefehl wiederholen**.
- **Der gespeicherte Entwurf hat sich geändert:** Jemand anderes hat den Datensatz geändert. Ihre Eingaben bleiben erhalten; laden Sie mit **Aktuellen Entwurf lesen** neu oder verwerfen Sie sie.
- **Ausfallzeitdatensätze oder der aktuelle Zugriff sind nicht verfügbar:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Die Quelle hat sich geändert:** Verwenden Sie für einen neuen Entwurf die aktuelle Revision der zugehörigen Quelle.
- **Befehl nicht angenommen:** Melden Sie es dem Support mit einem Screenshot, der Uhrzeit und Ihren Schritten.

## Verwandte Seiten
