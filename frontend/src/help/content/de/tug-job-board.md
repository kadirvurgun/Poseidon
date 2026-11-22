---
key: tug-job-board
title: Auftragsübersicht
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [canRead, canWrite]
related: [tug-command, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 7]
reviewed: false
---
## Wofür diese Seite dient
Die Auftragsübersicht listet die **Entwürfe von Schlepperauftragsanfragen** des ausgewählten Schlepperkontexts für die Planung auf. Sie beantwortet: „Welche Anfragen gibt es, welches Zeitfenster verlangen sie, wie viele Schlepper und wie viel Pfahlzug (BP) benötigen sie, und was fehlt noch für die Planung?“ Die Übersicht zeigt nur Entwurfsangaben; sie ist weder eine betriebliche Zuweisung noch eine Eignung, Erlaubnis oder Sicherheitsfreigabe.

## Wer sie verwendet
- **Koordinator Schlepperbetrieb / Landbetrieb:** um Anfragen zu sehen, Konflikt- und Bereitschaftsinformationen zu verfolgen und einen Zuweisungsvorschlag vorzubereiten (13.03 §7).
- **Schlepperkapitän:** um anstehende Arbeit für den eigenen Schlepper im Voraus zu sehen (13.03 §3).

## Bevor Sie beginnen
- Wählen Sie in der oberen Kontextleiste das **richtige Unternehmen und den richtigen Schlepper**. Die Übersicht zeigt nur Anfragen dieses Kontexts.
- Zum Anzeigen der Übersicht benötigen Sie Lesezugriff. Zum Anlegen einer Anfrage benötigen Sie Schreibzugriff; ohne ihn wird die Schaltfläche **Neue Anfrage** nicht angezeigt.

## Schritt für Schritt
1. Sehen Sie in der Zusammenfassung oben die Anzahl der Anfragen auf dieser Seite, die Aufträge mit erfasstem Hindernis, die auf Bereitschaftsprüfung wartenden und die mit unbekannter Bereitschaft. **Die Zählungen gelten nur für diese Seite.**
2. Geben Sie bei Bedarf eine Auftragsnummer oder den Namen des unterstützten Schiffs unter **Anfragen suchen** ein, wählen Sie die Filter **Priorität**, **Status** und **Zeitfenster** und drücken Sie **Filter anwenden**.
3. Lesen Sie in jeder Zeile das angeforderte Zeitfenster (UTC), Zuweisungsentwürfe, die erforderliche Schlepperanzahl, den erforderlichen BP im Vergleich zur Planungskapazität, die Bereitschaft und betriebliche Konflikte.
4. Öffnen Sie in einer Zeile **Begründung / nächste Aktion**, um die zugewiesenen Schlepperreferenzen, den Vergleichsumfang und die erfassten Gründe zu sehen.
5. Verwenden Sie **Auftrag öffnen**, um zu den Details der Anfrage zu wechseln.
6. Drücken Sie für eine neue Anfrage **Neue Anfrage**, um die **Auftragserfassung** zu öffnen (erfordert Schreibzugriff).
7. Verwenden Sie **Übersicht aktualisieren**, um die Daten erneut zu lesen, und **Zurück** / **Weiter** für andere Seiten.

## Was die Zustände bedeuten
- **Draft / Submitted / Cancelled:** der Lebenszyklusstatus der Anfrage. Das Einreichen erfasst die Anfrage für die Planung; es ist weder eine Genehmigung noch eine Zuweisung oder Bereitschaftsentscheidung.
- **Blockiert:** Für die Anfrage ist ein Hindernis erfasst. Der Grund steht unter **Begründung / nächste Aktion**.
- **Prüfung erforderlich:** Die Bereitschaftsinformation wartet auf die Prüfung durch eine berechtigte Person.
- **Unbekannt:** Für Bereitschaft oder Konflikt gibt es nicht genügend Quelldaten. Unbekannt bedeutet weder **Bereit** noch „kein Konflikt“, und die Übersicht zieht daraus keinen Schluss auf die Bereitschaft.
- **Authorized human review required / Boundary touch review required:** Zwei Anfragen können sich zeitlich überschneiden oder direkt aneinandergrenzen. Eine Person entscheidet.
- **BP im Profil (ungeprüft):** Der angezeigte Pfahlzug ist ein angegebener, kein gemessener oder verifizierter Wert.

## Was Sie nicht tun können / Vorsicht
- Die Übersicht erklärt eine Anfrage niemals für „bereit“ oder „sicher“. Ein Zuweisungsvorschlag von Land hebt die Sicherheitsbefugnis des Schlepperkapitäns an Bord nicht auf (13.03 §7).
- Felder einer eingereichten oder stornierten Anfrage werden nicht über die Übersicht geändert; eine stornierte Anfrage kann nicht neu gestartet werden, legen Sie stattdessen eine neue an.
- Der Zeitfensterfilter verwendet die Serverzeit. Anfragen ohne Beginn bleiben als „Unbekannt“ in der Liste.
- Offline angezeigte Daten können veraltet sein, und die Übersicht kann nicht aktualisiert werden.

## Wenn etwas schiefgeht
- **Aktuelle Übersicht nicht verfügbar:** Prüfen Sie Ihre Verbindung und verwenden Sie **Übersicht aktualisieren**.
- **Keine Anfrageentwürfe:** Haben Sie Filter angewendet, versuchen Sie eine andere Suche oder Priorität; neue Anfragen erscheinen in diesem Kontext.
- **Zugriff nicht verfügbar:** Prüfen Sie Ihre Rolle und Ihren Schlepperkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Unerwartetes Hindernis oder unerwartete Prüfung:** Öffnen Sie **Begründung / nächste Aktion** und prüfen Sie die erfassten Gründe mit dem Verantwortlichen der Anfrage.

## Verwandte Seiten
