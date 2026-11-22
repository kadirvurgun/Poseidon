---
key: pilot-program
title: Pilotprogramm: Wochen und Wertbericht
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Pilotprogramm hält die Pilotwochen für ein Schiff (und optional einen Schlepper), die Checklistenpunkte jeder Woche, Pilotprobleme, tägliches Feedback und den Wertbericht an einer Stelle zusammen. Sie beantwortet: „Sind die Punkte jeder Pilotwoche durch echte Datensätze erfüllt, und wie haben sich die gemessenen Werte gegenüber der Ausgangsbasis verändert?“ Es gilt der Hinweis oben: „Software kann keinen Pilotbetrieb abschließen. Ein Checklistenpunkt ist nur durch eine von Ihnen gesetzte Verknüpfung zu einem echten Datensatz erfüllt, den der Server prüft; eine fehlende Ausgangsbasis bleibt Unbekannt; nur der bestellte Product Owner schließt eine Woche ab oder erfasst die Programmentscheidung.“

## Wer sie verwendet
- **Product Owner:** schließt Wochen ab und erfasst die Programmentscheidung (**Fortsetzen**, **Korrigieren**, **Stoppen**, **Erweitern**). Dazu ist eine aktuelle Product-Owner-Bestellung erforderlich.
- **Pilotteam:** verknüpft Datensätze mit Checklistenpunkten, meldet Probleme, erfasst Feedback und Messungen.
- **Triage-Verantwortlicher:** stuft Probleme ein und löst sie.
- 13.03 definiert für diese Seite keine eigene Rolle; es gelten die Grundsätze der Mandantenverwaltung (13.03 §9).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie mandantenweiten Lesezugriff und Lesezugriff auf das Pilotprogramm. Programme anlegen, Erfassen, Triage und Entscheiden benötigen jeweils eine eigene Berechtigung; Wochenabschlüsse und Entscheidungen benötigen zusätzlich eine Product-Owner-Bestellung.
- Die Datensätze, die Sie verknüpfen (zum Beispiel ein Zeugnis-, Crew- oder Arbeitsauftragsdatensatz), müssen auf ihren eigenen Seiten bereits existieren.
- Geben Sie im Feedback niemals Namen, E-Mail-Adressen, Telefon- oder Ausweisnummern ein; verwenden Sie nur ein Pseudonym und die Rolle.
- Das Erfassen benötigt eine aktive Verbindung.

## Schritt für Schritt
1. Öffnen Sie ein Programm unter **Pilotprogramme**. Für ein neues Programm füllen Sie in **Pilotprogramm anlegen** die Felder **Programmname**, **Schiff**, **Schlepper (optional)**, **Startdatum Woche 1** und **Kalenderzeitzone (IANA)** aus und drücken **Erfassen**.
2. Prüfen Sie in jedem Abschnitt **Woche** die Spalten **Checklistenpunkt**, **Kriterium (Quelle)**, **Bereich**, **Status** und **Verknüpfte Datensätze**.
3. Um einen Punkt zu erfüllen, geben Sie in **Datensatz verknüpfen** den **Checklistenpunkt**, den **Datensatztyp** und die **Datensatzkennung** ein und drücken **Datensatz verknüpfen**. Eine falsche Verknüpfung entfernen Sie mit **Verknüpfung zurückziehen**, nachdem Sie eine **Begründung des Zurückziehens** geschrieben haben.
4. Geben Sie in **Pilotprobleme** mit **Problem melden** den Bereich, die **Kategorie**, den Schweregrad und die **Problemzusammenfassung** ein. Der Triage-Verantwortliche verwendet **Triage** mit einer **Triage-Notiz** und löst das Problem anschließend.
5. Geben Sie in **Tägliches Feedback** mit **Feedback erfassen** eine **Zusammenfassung** und die **Stimmung** ein.
6. Geben Sie in **Wertbericht** mit **Messung erfassen** **Kennzahl**, **Phase** (**Ausgangsbasis** oder **Pilotzeitraum**), **Wert**, **Stichprobengröße**, **Zeitraum von**, **Zeitraum bis**, **Nachweisreferenz** sowie **Methode und Stichprobe** ein.
7. Wenn Sie der Product Owner sind, erfassen Sie Ergebnis und Begründung in **Woche abschließen**. Am Ende des Programms füllen Sie in **Programmentscheidung erfassen** die Entscheidung, die **Bestellung** und die **Begründung** aus und drücken **Erfassen**.

## Was die Zustände bedeuten
- **Verknüpft und geprüft:** Ein vom Server geprüfter echter Datensatz ist mit dem Punkt verknüpft. Punkte mit **Verknüpfung nicht mehr gültig** und **Fehlend** sind nicht erfüllt.
- **Abschluss erforderlich:** Der verknüpfte Datensatz muss in seinem abgeschlossenen Zustand sein. „Der Prüfpfad belegt, dass eine Person den Datensatz geschrieben hat; er belegt nicht, dass der Datensatz oder Ablauf abgeschlossen ist.“
- **Bereit für menschlichen Abschluss / Nicht bereit:** eine Empfehlung für die Woche; nur der Product Owner schließt sie als **Angenommen** oder **Nicht angenommen** ab.
- **Am selben Tag triagiert / Verspätet triagiert / Triage heute fällig / Überfällig:** der Triage-Zeitpunkt von P0-Problemen gemessen am Kalendertag des Programms. Für Probleme mit **Kein Taggleich-Ziel in der Quelle** erfindet das System keine Frist.
- **Gemessen / Unbekannt:** Fehlt ein Ausgangs- oder Pilotwert, bleibt der Wertbericht **Unbekannt**; er wird nie als null oder als Verbesserung gezählt. Werte mit **Nicht vergleichbar** werden nicht verglichen.
- **Telemetrie (erklärt, ungeprüft):** ein erklärter Telemetriewert.
- **Erweiterung möglich; menschliche Entscheidung ausstehend / Korrektur empfohlen:** eine Programmempfehlung, keine Entscheidung.

## Was Sie nicht tun können / Vorsicht
- „Angenommen ist nur möglich, wenn jeder Punkt geprüft ist und keine P0-Triage offen, ausstehend oder verspätet ist.“
- „Erweitern ist nur möglich, wenn jede Woche angenommen ist und Werte, UAT, Mängel und P0 geklärt sind.“
- Wenn Sie Nachweise verfasst haben, auf denen eine Woche oder Entscheidung beruht (Verknüpfungen, Feedback, Messungen), können Sie diese Woche nicht abschließen und diese Entscheidung nicht erfassen.
- Für ein fiktives Fixture-Programm kann kein Wochenabschluss und keine Entscheidung erfasst werden.
- Das System schließt nie einen Pilotbetrieb ab, schließt nie eine Woche ab und erfindet nie Entscheidungen oder Freigaben.

## Wenn etwas schiefgeht
- **Sie haben keine aktuelle Product-Owner-Bestellung; Wochenabschlüsse und Entscheidungen sind schreibgeschützt.** Wenden Sie sich wegen der Bestellung an den Mandantenadministrator (13.03 §10).
- **Feedback kann erst erfasst werden, wenn ein Betreiber die Aufbewahrungsrichtlinie konfiguriert hat.** Bitten Sie den Systemadministrator, die Aufbewahrungsrichtlinie zu konfigurieren.
- **Der Datensatz hat sich geändert oder wurde bereits verwendet. Neu laden.** Öffnen Sie den aktuellen Stand mit **Neu laden**.
- **Das Ergebnis ist unsicher. Erneut versuchen sendet denselben Befehl.** Verwenden Sie **Erneut versuchen**, um denselben Befehl sicher erneut zu senden.
- **Durch die Regeln abgelehnt.** Zum Beispiel existiert der verknüpfte Datensatz nicht, liegt außerhalb des Zugriffsbereichs oder ist nicht abgeschlossen.
- **Nicht erlaubt.** Sie haben Verknüpfungen, Feedback oder Messungen verfasst, auf denen dieser Wochenabschluss oder diese Entscheidung beruht; eine andere Person muss sie erfassen.
- Eine Woche, die noch nicht begonnen hat, kann nicht abgeschlossen werden (angezeigt als „Der Datensatz hat sich geändert oder wurde bereits verwendet. Neu laden.“).
- **Sie haben keinen Zugriff auf diese Datensätze.** Prüfen Sie Ihre Rolle und Ihren Mandantenbereich (13.03 §10).

## Verwandte Seiten
