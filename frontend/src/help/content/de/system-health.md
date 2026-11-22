---
key: system-health
title: Systemzustand
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
„Systemzustand“ zeigt Administratoren **Dienstbeobachtungen** auf einem Bildschirm: API, Audit-Schreibdienst, Synchronisierung, Benachrichtigungsaufträge, Dateidienst und Eingabeaktualität. Die Seite beantwortet: „Welche Dienstsignale wurden für das ausgewählte Fahrzeug und diesen Mandanten beobachtet, und welche nicht?“ Konfiguration und fehlende Telemetrie belegen weder einen einwandfreien Dienst noch die Bereitschaft eines Fahrzeugs.

## Wer sie verwendet
- **System- / Mandantenadministrator:** um Dienst- und Synchronisationsprobleme zu bemerken und kontrolliert zu behandeln (13.03 §9).
- **Support / Engineering:** um Beobachtungen zu prüfen, wenn ein Anwendungsfehler gemeldet wird (13.03 §10).

## Bevor Sie beginnen
- Die Seite ist nur für Benutzer mit mandantenweitem Lesezugriff, Lesezugriff auf den Aktionsverlauf und Lesezugriff auf den Systemzustand sichtbar.
- Prüfen Sie in der oberen Kontextleiste, dass das **richtige Unternehmen** ausgewählt ist.
- Beobachtungen werden pro Fahrzeug abgefragt; Sie müssen zuerst ein Fahrzeug auswählen.

## Schritt für Schritt
1. Wählen Sie das Fahrzeug in **Beobachtungsbereich des Schiffs**. Bei einer langen Liste verwenden Sie **Vorherige Schiffe** / **Nächste Schiffe**.
2. Prüfen Sie die Zeit **Gelesen am**.
3. Lesen Sie in **Dienstverfügbarkeit** die Startzeit und die Laufzeit des Prozesses.
4. Lesen Sie in den Karten **API** und **Audit-Schreibdienst** die Werte zu Anfragen, Fehlern, Persistierung und Latenz. Sie gelten für diesen Mandanten seit dem Prozessstart.
5. Sehen Sie in **Synchronisierung** die festgeschriebenen Ergebnisse, ungeklärte Konflikte und die letzte festgeschriebene Synchronisierung.
6. Sehen Sie in **Benachrichtigungsaufträge** offene Benachrichtigungen, überfällige Eskalationen und den Zustand des Bewertungsauftrags.
7. Lesen Sie die Karten **Dateidienst** und **Eingabeaktualität**.
8. Prüfen Sie unter **Pilotmetriken** die Beobachtungen von registrierten Pilot-Clients.
9. Drücken Sie **Beobachtungen aktualisieren** für aktuelle Daten.

## Was die Zustände bedeuten
- **Nicht beobachtet / Nicht verfügbar:** Für dieses Signal gibt es keine Beobachtung. Das bedeutet weder „einwandfrei“ noch „kein Problem“.
- **Konfiguriert · ungeprüft:** Der Dateidienst scheint konfiguriert zu sein, aber es wurde keine Live-Prüfung von Speicher oder Scanner durchgeführt.
- **Im Zeitplan / Läuft / Überfällig / Planer nicht aktiviert / Lauf abgebrochen (Sperre abgelaufen):** der Zustand des Bewertungsauftrags für Benachrichtigungen.
- **Ungeklärte Konflikte:** Offline-Konflikte, die auf eine menschliche Lösung warten. Sie werden nie automatisch zusammengeführt.
- **Aktuell / Veraltet / Unbekannt / Nicht verfügbar:** Anzahlen der Bewertungen der Eingabeaktualität. Unbekannt bedeutet nicht „Aktuell“.
- **Dieser Schnappschuss ist älter als zwei Minuten:** Aktualisieren Sie, bevor Sie sich darauf verlassen.

## Was Sie nicht tun können / Vorsicht
- Diese Seite ist schreibgeschützt; an Diensten, Fahrzeugen oder Datensätzen wird keine Aktion ausgeführt.
- Benachrichtigungsdatensätze existieren nur in der App; sie sind weder ein Zustellnachweis noch ein Nachweis irgendeiner Sicherheitsentscheidung.
- Die Dienstverfügbarkeit zeigt nur, dass dieser API-Prozess geantwortet hat; sie beweist nicht, dass andere Dienste laufen.
- Pilotmetriken sind angegeben und ungeprüft; sie sind keine Abnahmeaussage.
- Korrigieren Sie Daten nicht direkt in der Produktion; veröffentlichen Sie Konfigurationsänderungen kontrolliert (13.03 §9).

## Wenn etwas schiefgeht
- **Zugriff auf Systemzustand für diesen Kontext nicht verfügbar:** Prüfen Sie Ihre Rolle und Ihren Mandantenbereich.
- **Dienstbeobachtungen nicht verfügbar:** Es wird kein einwandfreier Zustand abgeleitet; verwenden Sie nach einer Weile **Beobachtungen aktualisieren**.
- **Es gibt einen ungeklärten Konflikt:** Verweisen Sie den betroffenen Benutzer an die Konfliktzentrale (13.03 §10).
- **Anwendungsfehler:** Melden Sie ihn dem Support/Engineering mit der Correlation ID, dem Bildschirm, der Uhrzeit und den Schritten.

## Verwandte Seiten
