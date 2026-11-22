---
key: regulatory-review
title: Regulatorische SME-Prüfung
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die regulatorische SME-Prüfung erfasst Kampagnen, in denen ein menschlicher Experte die Quellen- und Anwendbarkeitspunkte eines Schiffs oder Schleppers prüft. Sie beantwortet: „Welche Quellen- und Anforderungspunkte für dieses Fahrzeug wurden von einem berechtigten Prüfer bestätigt, und welche sind noch offen und werden verfolgt?“ Sie ist nur ein menschlicher Prüfdatensatz: Das System genehmigt keine Anforderung, aktiviert keine Regel oder Erlaubnis und beansprucht keine Klassen- oder Typzulassung (13.03 §8).

## Wer sie verwendet
- **Benutzer Regulatory / Assurance:** eröffnet Prüfkampagnen, erfasst Urteile zu Punkten und schließt die Kampagne (13.03 §8).
- **Bestellter Prüfer (SME):** bestätigt oder verfolgt Punkte innerhalb seiner eigenen Quellenautorität.
- **DPA / HSEQ:** beobachtet offene und nicht verfolgte Punkte (13.03 §4).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie Lesezugriff auf die regulatorische Prüfung. Eröffnen und Schließen von Kampagnen benötigt die Verwaltungsberechtigung; das Erfassen von Urteilen benötigt die Urteilsberechtigung und eine aktuelle Prüferbestellung für diesen Bereich.
- Wählen Sie in den Registerkarten **Prüfumfang** **Schiff** oder **Schlepper** und öffnen Sie dann unter **Schiff auswählen** das richtige Fahrzeug (13.03 §1).
- Quellen sollten auf der Seite [Quellenentwürfe](/app/sources) als offiziell und versioniert registriert sein (13.03 §8).
- Offline können Daten veraltet sein, und Befehle sind deaktiviert.

## Schritt für Schritt
1. Wählen Sie Bereich und Fahrzeug. Gibt es keine Kampagne, drücken Sie **Prüfkampagne eröffnen**.
2. Prüfen Sie in der **Matrix der Quellenverantwortung** für jede Quellenautorität die Kategorie, die Anzahl offener und bestätigter Punkte und die verantwortlichen Rollen.
3. Wählen Sie einen Punkt unter **Prüfpunkte**.
4. Füllen Sie im Formular **Prüfurteil erfassen** **Prüferbestellung**, **Urteil**, **Quellenautorität** und **Verantwortliche Rolle** aus. Hat der Punkt keine registrierte Quelle, geben Sie **Maßgebliche Quellenreferenz** und **Quellversion / Ausgabe** ein. Für einen Punkt, der offen bleibt, geben Sie eine **Vorgangs-ID** ein.
5. Schreiben Sie in **Prüfernotiz** eine Begründung mit 10–2000 Zeichen und drücken Sie **Erfassen**.
6. Wenn alle Punkte geklärt sind, wählen Sie in **Kampagne schließen** ein **Abschlussergebnis**, schreiben die **Abschlussbegründung** und drücken **Kampagne schließen**.

## Was die Zustände bedeuten
- Kampagne: **Offen**, **In Prüfung**, **Mit offenen Punkten geschlossen**, **Abgeschlossen**.
- Urteil: **Bestätigt**, **Nicht anwendbar (bestätigt)**, **Prüfung erforderlich**, **Mehrdeutig**. Sind kritische Daten unbekannt, bleibt der Punkt **Prüfung erforderlich**; ein unbekannter Punkt wird nie als „Nicht anwendbar“ gezählt.
- **Kandidat (unbestätigt):** Die Quelle ist noch nicht an eine verifizierte offizielle Quelle gebunden.
- **Veraltet — Fixierung geändert** und **Schiffsprofil geändert:** Die Quelle oder das Schiffsprofil hat sich nach dem Urteil geändert; der Punkt muss erneut geprüft werden.
- **Nicht verfolgte offene Punkte:** offene Punkte ohne verfolgten Vorgang. Solange die Zahl größer als null ist, kann die Kampagne nicht geschlossen werden.
- Die Kategorien **Gesetzliches Recht**, **Klassenvorschrift**, **Unternehmens-SMS** und **Leitfaden — kein Recht** ersetzen einander nie (13.03 §8).

## Was Sie nicht tun können / Vorsicht
- Wer eine Kampagne eröffnet hat, kann darin keine Urteile erfassen; ohne Prüferbestellung sind Urteile schreibgeschützt.
- Ein Prüfer mit unbestätigter Qualifikation kann offene Punkte verfolgen, aber nicht bestätigen. Ein veralteter Punkt kann nicht bestätigt werden.
- Zum Bestätigen ist eine registrierte Quelle oder eine maßgebliche Quellenreferenz mit ihrer Ausgabe erforderlich. Hinterlassen Sie keinen verwaisten oder quellenlosen Datensatz in der Kette Anforderung → Kontrolle → Nachweis → Verifizierung (13.03 §8).
- **Abgeschlossen** erfordert, dass jeder Punkt bestätigt ist; andernfalls schließen Sie mit offenen Punkten, von denen jeder verfolgt wird.
- Geben Sie in der Notiz keine Klassen- oder Typzulassung an. Im Schlepperbereich gilt ein Schleppgrenzwert ohne kontrollierte Quelle nie als rechtlicher Standardwert.
- Urteile werden nur von menschlichen Benutzern erfasst; KI- und Automatisierungskonten können sie nicht erfassen.

## Wenn etwas schiefgeht
- **Der Datensatz hat sich geändert. Neu laden und prüfen, bevor Sie fortfahren.** Drücken Sie **Neu laden**.
- **Das Ergebnis ist unsicher. Erneut versuchen sendet denselben Befehl.** Verwenden Sie **Erneut versuchen**.
- **Die Prüfregeln haben diesen Befehl abgelehnt.** Prüfen Sie, ob der Punkt veraltet ist, sowie die Quellenreferenz und den verfolgten Vorgang.
- **Sie haben keinen Zugriff auf die regulatorische Prüfung dieses Schiffs.** oder **Sie sind für diese Aktion nicht berechtigt.** Prüfen Sie Ihre Rolle und Ihren Bereich; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).
- Wirkt die Anwendbarkeit unerwartet, prüfen Sie Quelle und Kontexteingaben und sprechen Sie mit dem Kontrollverantwortlichen oder Fachexperten (13.03 §10).

## Verwandte Seiten
