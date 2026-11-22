---
key: pilot-gate
title: Pilot-Go/No-Go-Prüfung
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Pilot-Go/No-Go-Prüfung zeigt den Zustand der für einen Pilotbetrieb erforderlichen Gates, die Anzahl offener P0 und die Support-Bereitschaft an einer Stelle und erfasst die Go/No-Go-Entscheidung des bestellten Product Owners. Sie beantwortet: „Ist jedes Pilot-Gate mit Nachweis erfüllt, und wurde eine menschliche Entscheidung erfasst?“ Es gilt der Hinweis oben: „Nur beratende Prüfung. Unbekannte oder zurückgestellte Gates und ein unbekanntes oder offenes P0 empfehlen NO-GO. Nur der bestellte Product Owner erfasst eine Entscheidung.“

## Wer sie verwendet
- **Product Owner:** liest das Prüfergebnis und erfasst die Go/No-Go-Entscheidung mit Begründung. Für eine Entscheidung ist eine aktuelle Product-Owner-Bestellung erforderlich.
- **Support / Mandantenadministrator:** markiert Support- und Bereitschaftspunkte mit einer Nachweisreferenz.
- 13.03 definiert für diese Seite keine eigene Rolle; es gelten die Grundsätze der Mandantenverwaltung (minimale Rechte, kontrollierte Veröffentlichung) (13.03 §9).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie mandantenweiten Lesezugriff und Lesezugriff auf das Pilot-Gate. Das Markieren eines Supportpunkts benötigt die Berechtigung zum Erfassen von Support; eine Entscheidung benötigt die Entscheidungsberechtigung und zusätzlich eine auf dem Server eingerichtete aktuelle Product-Owner-Bestellung. Die Berechtigung allein genügt nicht.
- Nur ein menschliches Konto kann Supportpunkte markieren oder entscheiden; die Bestellung muss zu Ihrer aktiven Rolle passen und innerhalb ihres Gültigkeitszeitraums liegen.
- Die Gate-Eingaben stammen von anderen Seiten; zum Beispiel werden die Gates Ship UAT und Tug UAT aus bestätigten Durchläufen auf der Seite **Rollen-UAT-Aufzeichnungen** berechnet.
- Das Erfassen benötigt eine aktive Verbindung; offline ist es deaktiviert.

## Schritt für Schritt
1. Lesen Sie in **Pilot-Gate-Prüfung** die Empfehlung (**Beratende Empfehlung**), die Anzahl **Offene P0** und die Gate-Tabelle: **Gate**, **Status**, **Quelle**, **Gründe**.
2. Wählen Sie in **Support- und Bereitschaftsfähigkeit** für jeden Punkt einen **Status**, geben Sie für **Bereit** eine **Nachweisreferenz** ein, schreiben Sie eine **Notiz** und drücken Sie **Markieren**.
3. Wenn Sie der Product Owner sind, wählen Sie in **Go/No-Go-Entscheidung erfassen** die Entscheidung (**NO-GO**, **Bedingtes GO**, **GO**), wählen Sie unter **Bestellung** Ihre Product-Owner-Bestellung und schreiben Sie die **Begründung**. Für **Bedingtes GO** füllen Sie **Bedingungen (eine pro Zeile)** aus.
4. Drücken Sie **Erfassen**. Die Entscheidung erscheint mit ihrer laufenden Nummer unter **Entscheidungen des Product Owners**.
5. Drücken Sie **Aktualisieren**, um die Daten neu zu laden.

## Was die Zustände bedeuten
- **Nachweisschwelle erreicht (PASS):** Die Quelle des Gates erfüllt die Nachweisschwelle. Das ist keine Genehmigung; ein Mensch entscheidet.
- **Nicht bestanden (FAIL)**, **Unbekannt (UNKNOWN)**, **Zurückgestellt (DEFERRED):** Das Gate ist nicht erfüllt. Ein unbekanntes oder zurückgestelltes Gate zählt nie als bestanden und empfiehlt NO-GO.
- **NO-GO empfohlen:** Mindestens ein Gate ist nicht PASS, oder die Zahl offener P0 ist unbekannt oder größer als null.
- **GO-fähig; menschliche Entscheidung ausstehend:** Jedes Gate ist PASS und offene P0 sind null. Das System entscheidet nie GO; nur der Product Owner erfasst es.
- **Nicht markiert / Bereit / Nicht bereit:** der zuletzt markierte Zustand eines Supportpunkts.
- **Aktuell / Veraltet: Prüfung geändert:** Eine Entscheidung ist an die Prüfung zum Zeitpunkt der Entscheidung gebunden. Hat sich die Prüfung später geändert, wird die Entscheidung als veraltet angezeigt und muss neu bewertet werden.

## Was Sie nicht tun können / Vorsicht
- „GO ist nur möglich, wenn alle Gates PASS sind und offene P0 null sind.“ Andernfalls ist die Option GO deaktiviert.
- Bedingtes GO erfordert denselben Zustand wie GO (jedes Gate PASS, offene P0 nachweislich null) und mindestens eine Bedingung. Es kann nicht erfasst werden, solange ein Gate nicht bestanden, unbekannt oder zurückgestellt ist oder solange P0 offen oder unbekannt ist. NO-GO kann immer erfasst werden, ohne Bedingungen.
- Das System, KI oder Automatisierung trifft nie die Pilotentscheidung und erfindet nie Gates oder Freigaben. Pilot-Gates und Entscheidungen sind menschliche Datensätze.
- Eine erfasste Entscheidung können Sie nicht ändern; eine neue Entscheidung wird mit einer neuen laufenden Nummer hinzugefügt.

## Wenn etwas schiefgeht
- **Sie haben keine aktuelle Product-Owner-Bestellung. Entscheidungen sind schreibgeschützt.** Bestellungen werden auf dem Server eingerichtet; wenden Sie sich an den Mandantenadministrator (13.03 §10).
- **Sie haben keinen Zugriff auf diese Datensätze.** Prüfen Sie Ihre Rolle und Ihren Mandantenbereich.
- **Der Datensatz hat sich geändert oder wurde bereits verwendet. Neu laden.** Die Prüfung hat sich geändert, während Sie entschieden haben; öffnen Sie die aktuelle Prüfung mit **Neu laden**. Dies wird auch angezeigt, wenn sich Gate-Zustände so geändert haben, dass die Entscheidung nicht mehr zulässig ist.
- **Das Ergebnis ist unsicher. Erneut versuchen sendet denselben Befehl.** Verwenden Sie **Erneut versuchen**, um denselben Befehl sicher erneut zu senden.
- **Durch die Regeln abgelehnt.** Ein Gate ist zurückgestellt.
- **Nicht erlaubt.** Ihre Bestellung ist nicht aktuell, oder Ihr Konto ist kein menschliches Konto.
- **Der Dienst ist nicht verfügbar. Versuchen Sie es erneut.** Hält das an, melden Sie dem Support die Korrelations-ID, die Uhrzeit und Ihre Schritte (13.03 §10).

## Verwandte Seiten
