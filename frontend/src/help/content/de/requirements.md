---
key: requirements
title: Anforderungsentwürfe
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Anforderungsentwürfe führt die aus einer Quellrevision abgeleiteten **Anforderungen** als versionierte Entwürfe und bindet jede Anforderung an eine exakte Quellrevision. Sie beantwortet: „Auf welcher Revision welcher Quelle beruht diese Anforderung, und wie ist ihr erfasster Lebenszyklus?“ Eine Anforderung ist das erste Glied der Kette Anforderung → Kontrolle → Nachweis → Verifizierung. Kein Datensatz in dieser Kette sollte unverknüpft (verwaist) bleiben (13.03 §8).

## Wer sie verwendet
- **Benutzer Regulatory / Assurance:** bindet die Anforderung an ihre Quelle, bewertet die Anwendbarkeit mit Kontexteingaben und hält die Kette vollständig (13.03 §8).
- **DPA / HSEQ:** liest bei Feststellungs- und Auditarbeit die Grundlage einer Anforderung (13.03 §4).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie mandantenweiten Lesezugriff und Lesezugriff auf regulatorische Entwürfe; zum Bearbeiten zusätzlich Schreibzugriff auf regulatorische Entwürfe.
- Teilansichten benötigen eigene Berechtigungen, stets mit mandantenweitem Lesebereich. **Lebenszyklus und Anwendbarkeit** benötigt Lesezugriff auf Anwendbarkeit; das Bearbeiten von Regeln benötigt Schreibzugriff auf Regeln, das Anfordern einer Prüfung die Regelprüfung und das Bewerten die Anwendbarkeitsbewertung; Qualifizierung und Bestätigung von Fakten benötigen die regulatorische Qualifizierungs- oder Faktenbestätigungsberechtigung sowie eine auf dem Server erfasste Bestellung. **Kontrollen und Nachweise** benötigt Lesezugriff auf Kontrollen, Nachweise, Dateien und Anwendbarkeit; das Verknüpfen benötigt Schreibzugriff auf Kontrollen oder Nachweise mit Bearbeitungsrechten für das Fahrzeug, und die Genehmigung benötigt die Berechtigung zur Kontrollgenehmigung.
- Erfassen Sie die Quelle zuerst auf der Seite **Quellenentwürfe**. Eine Anforderung kann nur an eine vorhandene Quellrevision gebunden werden.
- Das Speichern benötigt eine aktive Serververbindung; offline bleiben Bearbeitungen in der Registerkarte erhalten.

## Schritt für Schritt
1. Sehen Sie die Anforderungsentwürfe in der Liste; suchen Sie mit **Search drafts** und **Suchen**.
2. Für eine neue Anforderung drücken Sie **Neuer Entwurf**.
3. Wählen Sie in **Draft fields** für jedes Feld einen **Status** (**Fehlend**, **Unbekannt**, **Known (declared)**) und geben Sie die bekannten Werte ein: **Regelungsinstrument**, **Versions- / Änderungsangabe**, **Angegebenes Wirksamkeitsdatum**, **Angabe / Herkunft**, **Quelle beobachtet am (mit UTC-Offset)**.
4. Tippen Sie in **Immutable source citation** in **Find a source** und drücken Sie **Search sources**. Wählen Sie die Quelle unter **Source draft** und dann die exakte Revision unter **Exact revision**. Prüfen Sie den Inhalt der gewählten Revision mit **Selected immutable source content**.
5. Drücken Sie **Entwurf speichern**. Wenn der Datensatz geöffnet ist, erscheinen die Abschnitte **Erfasster Lebenszyklus** und **Unveränderliche Historie**.
6. Verwenden Sie **Lebenszyklus und Anwendbarkeit** für die Anwendbarkeit und **Kontrollen und Nachweise** für Kontroll- und Nachweisverknüpfungen. Der **Bericht kritischer Einträge ohne Zuordnung** in **Kontrollen und Nachweise** listet kritische Angaben ohne Anforderungsverknüpfung; verknüpfen Sie diese Datensätze mit einer Anforderung.

## Was die Zustände bedeuten
- **Erfasster Lebenszyklus:** der Zustand, den der Server aus den gebundenen Regeln ableitet: **Entwurf**, **Übernommen**, **Künftig wirksam**, **In Kraft**, **Ersetzt**. Kann er nicht gelesen werden, wird **Nicht verfügbar** angezeigt; das System nimmt weder Entwurf noch In Kraft an.
- „Gebundene Regeln erfassen unterschiedliche Lebenszykluszustände. Der angezeigte Wert ist eine Software-Rangfolge; eine Expertenprüfung ist erforderlich.“ Der angezeigte Wert ist dann keine Entscheidung; eine Expertenprüfung ist nötig.
- **Unbekannt:** Die Information ist nicht bekannt. Sind kritische Daten unbekannt, bleibt das Anwendbarkeitsergebnis **Prüfung erforderlich** (REVIEW_REQUIRED); es wird nie als „nicht zutreffend“ oder „nein“ gezählt (13.03 §8).
- **Anwendbarkeitsprüfung erforderlich / Anforderungsverknüpfung fehlt / Verifizierung nicht durchgeführt:** offene Punkte in der Kontroll-Nachweis-Kette.
- **Abschluss unbekannt · ungeprüft · Wirksamkeit unbekannt:** Abschluss, Verifizierung und Wirksamkeit sind getrennte Zustände. Ein abgeschlossener Datensatz ist nicht verifiziert, und ein verifizierter Datensatz ist nicht wirksam.

## Was Sie nicht tun können / Vorsicht
- Sie können eine Anforderung nicht an „die neueste“ Quelle binden; es wird immer eine **Exact revision** gewählt. Erhält die Quelle eine neue Revision, bleibt die alte Bindung im Verlauf erhalten.
- Eine Anwendbarkeitsvorschau begründet keine Konformität, Bereitschaft, Erlaubnis oder rechtliche Genehmigung: „PREVIEW / DRAFT. Predictions do not establish compliance, readiness, permits or legal approval.“
- Eine Lebenszyklusänderung läuft über **Menschliche Prüfung anfordern**; Quellenzulassung und ein qualifizierter unabhängiger Prüfer entscheiden. Das System schließt sie nie selbst ab.
- Gehen Sie nicht davon aus, dass Klassen-, Flaggen-, Unternehmens- und lokale Regelquellen einander ersetzen (13.03 §8).

## Wenn etwas schiefgeht
- **Source selection unavailable.** Drücken Sie **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Öffnen Sie die aktuelle Revision mit **Gespeicherten Entwurf neu laden**.
- **Check field states, bounds, dates and source revision.** Wenn Sie eine Quelle gewählt haben, wählen Sie auch ihre exakte Revision.
- **History unavailable. Editor retained.** Drücken Sie **Retry history**.
- Ist der Lebenszyklus unerwartet, prüfen Sie die gebundenen Regeln und Quellrevisionen; wenden Sie sich bei Bedarf an den Kontrollverantwortlichen oder Fachexperten (SME) (13.03 §10).
- Fehlt eine Berechtigung, prüfen Sie Ihre Rolle und Ihren Mandantenbereich und fragen Sie den Mandantenadministrator (13.03 §10).

## Verwandte Seiten
