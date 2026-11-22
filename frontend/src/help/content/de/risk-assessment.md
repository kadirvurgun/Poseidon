---
key: risk-assessment
title: Risikobeurteilung
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Risikobeurteilung erfasst für eine Arbeit oder einen Betriebsvorgang auf einem Fahrzeug die Gefährdungen, möglichen Folgen, Schutzmaßnahmen, das Anfangs- und Restrisiko sowie die Bedingungen für einen Arbeitsstopp. Sie beantwortet: „Wurden die Gefährdungen und Maßnahmen für diese Arbeit berücksichtigt, geprüft und von den Beteiligten bestätigt?“ Die Seite ist **nur ein Entwicklungsziel**: Eine Freigabe hier ist ein erfasster menschlicher Prüfnachweis, keine Arbeitserlaubnis, keine fachliche Risikoakzeptanz und kein bestandenes HSE-Gate (13.03 §2, §4).

## Wer sie verwendet
- **Kapitän und Schlepperkapitän:** lassen die Beurteilung für Arbeiten auf dem Schiff oder Schlepper erstellen und treffen die Entscheidung über einen Arbeitsstopp. Die Entscheidungsbefugnis bleibt bei ihnen (13.03 §2, §3).
- **DPA / HSEQ:** beaufsichtigen die Risikosteuerung und erfassen die Prüfentscheidung (13.03 §4).
- **Crew / OOW / Ingenieur:** bestätigen als Beteiligte die freigegebene Beurteilung für sich selbst oder widersprechen ihr (13.03 §6).

## Bevor Sie beginnen
- Wählen Sie ein Fahrzeug, um seine Risikobeurteilungen zu öffnen. Zum Lesen benötigen Sie die Leseberechtigung für Risiken; zum Schreiben eines Entwurfs die Schreibberechtigung für Risiken und Bearbeitungsrechte für dieses Fahrzeug. Die Prüfentscheidung benötigt die Freigabeberechtigung für Risiken; die Bestätigung die Bestätigungsberechtigung für Risiken.
- Bewertungen benötigen eine im Mandanten veröffentlichte Risikomethode (Matrix). Ohne sie bleiben Bewertungen **Nicht bewertet**. Ein HSEQ-Verantwortlicher des Mandanten konfiguriert die Matrix, und eine andere Person veröffentlicht sie (Seite Risikovorlagen-Bibliothek).
- Risikobefehle benötigen eine aktuelle Online-Verbindung; offline wird nichts in eine Warteschlange gestellt oder freigegeben.

## Schritt für Schritt
1. Wählen Sie das Fahrzeug und öffnen Sie eine Beurteilung aus der Liste, oder drücken Sie **Neue Beurteilung**.
2. Geben Sie eine **Beurteilungsnummer** ein. Optional geben Sie eine **Arbeitsart** ein und wählen mit **Mit veröffentlichter Vorlage beginnen (nur Kopie)** eine passende Vorlage; die Vorlage wird nur in den neuen Entwurf kopiert.
3. Füllen Sie **Titel**, **Betriebs-/Arbeitskontext**, **Arbeitsart**, **Arbeitsumfang** und **Risikomethode** aus. Wählen Sie für **Arbeitsstopp erforderlich** **Ja**, **Nein** oder **Unbekannt** und schreiben Sie bei Bedarf die **Arbeitsstopp-Bedingungen**.
4. Geben Sie mit **Gefährdung hinzufügen** für jede Gefährdung den **Arbeitsschritt**, die **Mögliche Folgen**, das **Anfangsrisiko** und das **Restrisiko** ein (**Wahrscheinlichkeit**, **Schweregrad**, **Begründung**). Mit **Maßnahme hinzufügen** ergänzen Sie **Schutzmaßnahmen**, den **Maßnahmenverantwortlicher** und **Barriere-Referenzen**.
5. Schreiben Sie unter **Beteiligte** pro Zeile eine Akteurs-ID und eine Rolle. Verknüpfen Sie bei Bedarf zugehörige Datensätze mit **Verknüpfung hinzufügen**.
6. Drücken Sie **Entwurf speichern** und dann **Zur Prüfung einreichen**. **Blockierende Punkte** listet, was fehlt.
7. Ein Prüfer schreibt eine **Begründung** mit mindestens 10 Zeichen und drückt **Freigeben (menschlicher Prüfnachweis)** oder **Zurückgeben**.
8. Nach der Freigabe drückt jeder Beteiligte für sich selbst **Bestätigen** oder, mit Begründung, **Widersprechen**. Ist eine Änderung nötig, verwenden Sie **Ändern (öffnet neuen Entwurf)**.

## Was die Zustände bedeuten
- **Entwurf / In Prüfung / Zurückgegeben / Freigegeben (menschlicher Nachweis):** der Lebenszyklus der Beurteilung. Freigegeben ist ein menschlicher Prüfnachweis, keine Arbeitserlaubnis.
- **Nicht bewertet / Methode nicht konfiguriert:** Es gibt keine Bewertung. Das System verwendet keine Standardmatrix, keine Multiplikation und keine Schwelle für niedrig/mittel/hoch.
- **Arbeitsstopp erforderlich — Unbekannt:** Es wurde noch keine Entscheidung getroffen; dies wird nie als „Nein“ gelesen.
- **Bestätigung vor Freigabe nicht anwendbar / Bestätigungen ausstehend / Alle Beteiligten haben bestätigt / Widerspruch – ungelöst:** der Bestätigungszustand der Beteiligten. Ein Widerspruch wird nie durch die Bestätigung einer anderen Person geschlossen.
- **Seit Fixierung geändert / Nicht verfügbar:** Ein verknüpfter Datensatz hat sich nach der fixierten Revision geändert oder kann nicht mehr gelesen werden; prüfen Sie ihn erneut.

## Was Sie nicht tun können / Vorsicht
- Ein Verfasser oder der Einreicher des Prüfzyklus kann dieselbe Beurteilung nicht freigeben (Funktionstrennung).
- Jeder bestätigt nur für sich selbst und nur genau die freigegebene Revision.
- Eine Freigabe hier ist keine Arbeitserlaubnis, keine fachliche Risikoakzeptanz und kein HSE-Gate. Die Entscheidung, eine Arbeit zu beginnen oder zu stoppen, bleibt beim Kapitän oder Schlepperkapitän.
- Ein aus einer Vorlage begonnener Entwurf enthält keine Bewertung, keine Unterschrift, keine Bestätigung der Beteiligten und keine Freigabe.
- Offline kann eine Beurteilung weder eingereicht noch freigegeben noch bestätigt werden.

## Wenn etwas schiefgeht
- **Gesperrt, bis die aufgeführten Punkte erledigt sind.** Erledigen Sie die blockierenden Punkte und reichen Sie erneut ein.
- **This record changed elsewhere.** Drücken Sie **Neu laden**, um die aktuelle Revision zu sehen, und wiederholen Sie dann die Aktion.
- **Sie dürfen diese Aktion nicht ausführen (Funktionstrennung oder Geltungsbereich).** Bitten Sie eine andere berechtigte Person um die Prüfung, oder prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).
- **Der Risikobereich ist mit Ihrem aktuellen Zugriff nicht verfügbar.** Prüfen Sie Ihre Berechtigungen.
- Wirkt eine Bewertung unerwartet, prüfen Sie die Risikomethode und ihre Revision; fragen Sie den Kontrollverantwortlichen oder den HSEQ-Verantwortlichen (13.03 §10).

## Verwandte Seiten
