---
key: sources
title: Quellenentwürfe
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Quellenentwürfe führt die regulatorischen und Regelquellen, auf die sich Ihre Organisation stützt (Übereinkommen, Flagge, Klasse, Unternehmens- oder lokale Regel), als **versionierte Entwurfsdatensätze**. Sie beantwortet: „Auf welcher Revision welcher offiziellen Quelle beruht diese Anforderung?“ Es gilt der Hinweis oben: „Tenant-local DRAFT · Declared and unverified. Dates and hashes confer no legal authority. Source references are stored as text.“ (13.03 §8).

## Wer sie verwendet
- **Benutzer Regulatory / Assurance:** registriert die offizielle, versionierte Quelle und gibt ihr Wirksamkeitsdatum und ihren Lebenszyklus an (13.03 §8).
- **DPA / HSEQ:** liest sie, um zu sehen, auf welche Quellrevision sich ein Audit oder eine Feststellung bezieht (13.03 §4).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie mandantenweiten Lesezugriff und Lesezugriff auf regulatorische Entwürfe. Anlegen und Speichern von Entwürfen benötigt zusätzlich Schreibzugriff auf regulatorische Entwürfe; ohne ihn wird die Schaltfläche **Neuer Entwurf** nicht angezeigt.
- Teilansichten benötigen eigene Berechtigungen, stets mit mandantenweitem Lesebereich. **Lebenszyklus und Anwendbarkeit** benötigt Lesezugriff auf Anwendbarkeit; das Bearbeiten von Regeln benötigt Schreibzugriff auf Regeln, das Anfordern einer Prüfung die Regelprüfung und das Bewerten die Anwendbarkeitsbewertung; Qualifizierung und Bestätigung von Fakten benötigen die regulatorische Qualifizierungs- oder Faktenbestätigungsberechtigung sowie eine auf dem Server erfasste Bestellung. **Kontrollen und Nachweise** benötigt Lesezugriff auf Kontrollen, Nachweise, Dateien und Anwendbarkeit; das Verknüpfen benötigt Schreibzugriff auf Kontrollen oder Nachweise mit Bearbeitungsrechten für das Fahrzeug, und die Genehmigung benötigt die Berechtigung zur Kontrollgenehmigung.
- Halten Sie den offiziellen Text der Quelle und ihre Versions-/Änderungsangaben bereit. POSEIDON lädt den Quelltext weder herunter noch verifiziert es ihn; Sie geben ihn an.
- Das Speichern benötigt eine aktive Serververbindung. Offline bleiben Ihre Bearbeitungen in der Registerkarte erhalten, werden aber nicht gespeichert.

## Schritt für Schritt
1. Sehen Sie die erfassten Quellenentwürfe in der Liste. Zum Suchen tippen Sie in **Search drafts** und drücken **Suchen**. Mit **Zurück** und **Weiter** wechseln Sie zwischen den Seiten.
2. Für eine neue Quelle drücken Sie **Neuer Entwurf**.
3. Wählen Sie in **Draft fields** für jedes Feld zuerst einen **Status**: **Fehlend**, **Unbekannt** oder **Known (declared)**. Einen Wert können Sie erst eingeben, nachdem Sie **Known (declared)** gewählt haben. Die Felder sind **Titel**, **Angegebene Befugnis**, **Quellenreferenz (nur Text)**, **Versions- / Änderungsangabe**, **Angegebenes Wirksamkeitsdatum**, **Angabe / Herkunft** und **Quelle beobachtet am (mit UTC-Offset)**.
4. Drücken Sie **Entwurf speichern**. Bei Erfolg sehen Sie „Draft saved. Unverified.“; öffnen Sie den Datensatz mit **Open saved draft and history**.
5. Öffnen Sie einen vorhandenen Entwurf aus der Liste, ändern Sie ihn und speichern Sie erneut. Jedes Speichern erzeugt eine neue **Revision**; ältere Revisionen bleiben mit ihrem Inhalt und **Integritätshash (keine Befugnis)** in **Unveränderliche Historie** erhalten.
6. Um zu Anwendbarkeitsregeln oder zur Kontroll-Nachweis-Kette zu wechseln, verwenden Sie **Lebenszyklus und Anwendbarkeit** oder **Kontrollen und Nachweise** über der Liste. Mit **Back to register** kehren Sie zurück.

## Was die Zustände bedeuten
- **ENTWURF:** der gespeicherte Zustand, der in der Liste und im Datensatzkopf angezeigt wird. Jeder Quelldatensatz auf dieser Seite ist ein Entwurf; er ist weder eine offizielle Übernahme noch eine Inkraftsetzungsentscheidung.
- **Fehlend:** Für das Feld wurde noch keine Information eingegeben. Spätere Schritte (zum Beispiel eine Anwendbarkeitsbewertung) sind blockiert, solange erforderliche Daten fehlen.
- **Unbekannt:** Die Information wurde gesucht, ist aber nicht bekannt. Das System liest Unbekannt nie als „nein“ oder „nicht zutreffend“; sind kritische Daten unbekannt, bleibt das Ergebnis in Prüfung (REVIEW_REQUIRED).
- **Known (declared):** Sie haben den Wert angegeben. Eine Angabe ist keine verifizierte Information.
- **Integritätshash (keine Befugnis):** zeigt, dass sich der Inhalt einer Revision nicht geändert hat; er ist kein Nachweis rechtlicher Gültigkeit.

## Was Sie nicht tun können / Vorsicht
- Eine gespeicherte Revision können Sie nicht ändern. Wenn sich die Quelle ändert, speichern Sie eine neue Revision; Anforderungen bleiben an die ältere Revision gebunden, und der Verlauf bleibt erhalten (13.03 §1).
- Gehen Sie nicht davon aus, dass Klassen-, Flaggen-, Unternehmens- und lokale Regelquellen einander ersetzen; erfassen Sie jede als eigene Quelle (13.03 §8).
- Diese Seite lässt keine Quelle offiziell zu und liefert niemandem ein Konformitäts-, Erlaubnis- oder Bereitschaftsergebnis. Quellenzulassung und unabhängige Prüfung liegen in menschlicher Befugnis.
- Wirksamkeitsdaten und Hashes verleihen keine rechtliche Befugnis.

## Wenn etwas schiefgeht
- **Permission denied.** Prüfen Sie Ihre Rolle und Ihren Mandantenbereich; fragen Sie bei Bedarf den Mandantenadministrator (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Jemand hat den Datensatz vor Ihnen geändert. Öffnen Sie die aktuelle Revision mit **Gespeicherten Entwurf neu laden** und nehmen Sie Ihre Änderung erneut vor.
- **Check field states, bounds, dates and source revision.** Ein Feldwert oder ein Datumsformat ist ungültig.
- **Service unavailable. Your draft is retained.** Wenn die Verbindung zurück ist, senden Sie denselben Befehl mit **Retry frozen command** erneut.
- **Sign in to continue; retain your draft.** Ihre Sitzung ist beendet; verwenden Sie den Link **Anmelden**.
- Hält ein Anwendungsfehler an, melden Sie dem Support die Korrelations-ID, den Bildschirm, die Uhrzeit und Ihre Schritte (13.03 §10).

## Verwandte Seiten
