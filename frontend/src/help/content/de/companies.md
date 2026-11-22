---
key: companies
title: Unternehmen
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Unternehmen“ listet die **Unternehmensdatensätze** Ihres Arbeitsbereichs auf, die Sie sehen dürfen. Sie beantwortet: „Welche Unternehmen sind in diesem Arbeitsbereich definiert, und wie lauten ihr Code und ihr Name?“ Schiffs- und Schlepperdatensätze gehören zu einem Unternehmen; das richtige Unternehmen ist daher der erste Schritt bei der Wahl Ihres Kontexts.

## Wer sie verwendet
- **Mandantenadministrator:** erstellt und bearbeitet Unternehmensdatensätze nach dem Prinzip der minimalen Rechte (13.03 §9).
- **Landbetrieb:** findet den richtigen Unternehmenskontext.

## Bevor Sie beginnen
- Um diese Seite zu sehen, benötigen Sie die Berechtigung zum Lesen von Unternehmen. Ohne sie wird die Meldung „Sie haben keinen Zugriff auf diesen Arbeitsbereich.“ angezeigt.
- Zum Erstellen eines Datensatzes ist die Erstellungsberechtigung nötig. Ob Sie einen Datensatz bearbeiten können, hängt von der Berechtigung ab, die der Server für diesen Datensatz gewährt.

## Schritt für Schritt
1. Sehen Sie in der Liste Code und Namen jedes Unternehmens. Mit **Zurück** / **Weiter** gelangen Sie zu anderen Seiten.
2. Zum Suchen geben Sie etwas in **Nach Code oder Name suchen** ein und drücken **Suchen**.
3. Wählen Sie einen Unternehmensnamen, um den Abschnitt **Details** zu öffnen.
4. Für ein neues Unternehmen drücken Sie **Neuer Datensatz**, geben Code und Namen ein und drücken **Speichern**. Alle Felder sind Pflichtfelder.
5. Um einen bestehenden Datensatz zu ändern, verwenden Sie in den Details **Bearbeiten**.
6. Öffnen Sie **Rechtsträgerdatensatz anzeigen**, um die Angaben zum Rechtsträger zu sehen; dieser Abschnitt ist schreibgeschützt.

## Was die Zustände bedeuten
- **No records found:** Für Ihre Suche oder Ihren Berechtigungsbereich wurde kein Datensatz zurückgegeben.
- **Legal entity: Draft – incomplete, not active / Active / Inactive:** der Zustand des Rechtsträgerdatensatzes. Entwurf bedeutet, dass vor der Aktivierung noch Werte fehlen.
- **Unbekannt:** Der Wert ist nicht im Datensatz vorhanden. Unbekannt bedeutet weder „Nein“ noch „gültig“.
- **Saved:** Der Datensatz wurde auf den Server geschrieben. Dieser Datensatz erzeugt keine betriebliche Freigabe.

## Was Sie nicht tun können / Vorsicht
- Das Erstellen oder Bearbeiten eines Unternehmensdatensatzes ist keine betriebliche, Compliance- oder Sicherheitsfreigabe.
- Werte des Rechtsträgers werden nicht auf dieser Seite geändert; sie ändern sich nur über geprüfte HR-Befehle. Registerkennungen werden maskiert angezeigt.
- Derselbe Code kann nicht von zwei Unternehmen verwendet werden.
- Verlassen Sie die Seite mit einem nicht gespeicherten Entwurf, werden Sie gefragt, ob Sie ihn verwerfen oder weiter bearbeiten möchten.

## Wenn etwas schiefgeht
- **Dieser Datensatz wurde in einem anderen Vorgang geändert:** Ihr Entwurf bleibt erhalten. Verwerfen Sie den Entwurf, um den aktuellen Datensatz zu laden, und nehmen Sie Ihre Änderung dann erneut vor.
- **Dieser Code wird bereits verwendet:** Geben Sie einen anderen Code ein.
- **Das Ergebnis konnte nicht bestätigt werden:** Die Felder bleiben gesperrt, bis das Ergebnis bekannt ist; wiederholen Sie denselben Befehl gefahrlos.
- **Ihre Berechtigung hat sich möglicherweise geändert / Kein Zugriff:** Prüfen Sie Ihre Rolle und Ihren Arbeitsbereichskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Der Dienst ist nicht verfügbar:** Verwenden Sie „Erneut versuchen“; besteht das Problem weiter, melden Sie es mit der Support-Referenz.

## Verwandte Seiten
