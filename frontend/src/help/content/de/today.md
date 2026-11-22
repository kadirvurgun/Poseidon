---
key: today
title: Heute
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## Wofür diese Seite dient
„Heute“ sammelt für das ausgewählte Schiff oder den ausgewählten Schlepper die **Warnungen, die jetzt Ihre Aufmerksamkeit brauchen**, in einer Liste. Die Seite beantwortet: „Welcher Datensatz dieses Fahrzeugs wartet heute auf eine Entscheidung oder Prüfung, und welche Quelle sagt das?“ Die Seite entscheidet nichts; sie zeigt die Warnung, ihre Quelle und den nächsten Schritt.

## Wer sie verwendet
- **Kapitän** und **Schlepperkapitän:** um die Ausnahmen des Tages, ihren Verantwortlichen und den nächsten Schritt zu sehen (13.03 §2, §3).
- **Leitender Ingenieur:** um Warnungen aus dem Instandhaltungsplan zu verfolgen (13.03 §5).
- **Crew / OOW:** um Arbeiten zu sehen, die ihnen zufallen (13.03 §6).

## Bevor Sie beginnen
- Prüfen Sie in der oberen Kontextleiste, dass das **richtige Unternehmen und Fahrzeug** ausgewählt sind (13.03 §1). Eine angezeigte Warnung gehört zum ausgewählten Fahrzeug.
- Zum Öffnen dieser Seite benötigen Sie Lesezugriff auf den Arbeitsbereich.
- Ist kein Fahrzeug ausgewählt, zeigt die Seite zuerst die Liste **Schiff auswählen**.

## Schritt für Schritt
1. Wählen Sie das Schiff oder den Schlepper, an dem Sie arbeiten. Das ausgewählte Fahrzeug wird als **Aktuelles Schiff** angezeigt; mit **Schiff wechseln** ändern Sie es.
2. Sehen Sie sich oben den Abschnitt **Quellenabdeckung** an. Er zeigt, welche Quellen (Vorschriften, geplante Instandhaltung, Schlepperbetrieb) gelesen werden konnten.
3. Lesen Sie die Warnkarten von oben nach unten. Jede Karte stammt aus einer Quelle und schlägt einen nächsten Schritt vor (zum Beispiel **Entwurf prüfen**, **Instandhaltungsplan prüfen**, **Schlepperauftrag anzeigen**).
4. Drücken Sie **Warum?**, um den Grund hinter einer Karte zu sehen. Der Abschnitt zeigt die Regel, den Quelldatensatz, seine Revision, die Lesezeit und alle unbekannten Eingaben.
5. Folgen Sie dem Link der Karte zur eigenen Seite des Datensatzes und handeln Sie dort im Rahmen Ihrer Berechtigungen.
6. Mit **Aktualisieren** lesen Sie die Daten erneut.

## Was die Zustände bedeuten
- **Verfügbar (ungeprüft):** Die Quelle wurde gelesen. Das bedeutet nicht, dass ihr Inhalt verifiziert wurde.
- **Fehlend / In diesem Kontext nicht verfügbar:** Die Quelle konnte für dieses Fahrzeug nicht gelesen werden. Eine Warnung, die Sie nicht sehen, bedeutet nicht, dass alles in Ordnung ist.
- **Teilansicht:** Einige Quellen konnten nicht gelesen werden. Eine Anzahl von null bedeutet nicht „kein Problem“.
- **Alle aufgeführten Quellen wurden gelesen und lieferten keine Einträge:** Es kam keine Warnung zurück. Dies ist keine Sicherheitsfreigabe.
- **Unbekannt:** Der Wert fehlt in der Quelle oder konnte nicht bestimmt werden. Unbekannt bedeutet weder „Nein“ noch „Bereit“ noch „Nicht zutreffend“.
- **Prüfung erforderlich / Planungsbedarf / Hinweis:** die Priorität der Karte. Eine Karte mit „Prüfung erforderlich“ wartet darauf, dass eine berechtigte Person sie ansieht.
- **Abgeschlossen ≠ Verifiziert:** Dass ein Datensatz abgeschlossen ist, bedeutet nicht, dass ein berechtigter Prüfer ihn bestätigt hat (13.03 §1).

## Was Sie nicht tun können / Vorsicht
- Diese Seite trifft keine Abfahrts-, Betriebs- oder Sicherheitsentscheidung. Diese Entscheidung liegt beim berechtigten Kapitän oder Schlepperkapitän.
- Auf dieser Seite wird kein Befehl ausgeführt; Aktionen finden auf der eigenen Seite des Datensatzes statt.
- Offline angezeigte Daten sind veraltet und ungeprüft. Die Details unter **Warum?** öffnen sich offline nicht.
- Eine Lese- oder Speicherzeit beweist nicht, dass die Quelle korrekt ist.

## Wenn etwas schiefgeht
- **Offline:** Setzen Sie Ihre kritische Arbeit fort und beobachten Sie den Synchronisationsstatus. Erscheint ein Konflikt, gehen Sie zum Konfliktzentrum (13.03 §10).
- **Zugriff verweigert / Schiff in Ihrem Zugriffsbereich nicht verfügbar:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator.
- **Unerwartete Warnung:** Öffnen Sie **Warum?**; wirken Regel oder Quelle falsch, geben Sie es an den Kontrollverantwortlichen oder einen Fachexperten weiter.
- **Quelle nicht verfügbar / Zu viele Anfragen:** Warten Sie einen Moment und verwenden Sie **Erneut versuchen**.
- **Die Serverantwort hat die Prüfung nicht bestanden:** Nichts davon wird angezeigt; melden Sie es dem Support mit einem Screenshot, der Uhrzeit und Ihren Schritten.

## Verwandte Seiten
