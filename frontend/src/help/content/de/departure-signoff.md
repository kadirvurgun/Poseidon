---
key: departure-signoff
title: Abfahrtsbereitschaft und Kapitänsfreigabe
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: [1, 2, 3, 4, 5, 6, 7, 9]
reviewed: false
---
## Wofür diese Seite dient
Diese Seite zeigt eine **beratende Bereitschaftsbewertung** für das ausgewählte Schiff und den geplanten Abfahrtszeitraum und erfasst die Entscheidung des bestellten Kapitäns. Sie beantwortet: „Was sagen die Quellen zu Besatzung, Technik, Vorschriften, Notfall und Sicherheitsereignissen für diesen Zeitraum, und warum?“ Die Entscheidung, die Abfahrt freizugeben, anzuhalten oder zu stoppen, liegt beim **Kapitän** (13.03 §2). Das System gibt nie eine Abfahrt frei; eine erfasste Freigabe ist keine Abfahrtsgenehmigung.

## Wer sie verwendet
- **Kapitän:** prüft die Bewertung und die Liste „Warum“ und erfasst dann Freigeben, Anhalten oder Ablehnen mit einer Begründung (13.03 §2).
- **Landbetrieb / Technischer Inspektor:** verfolgt die Bewertung und die erfassten Kapitänsentscheidungen schreibgeschützt. Landpersonal kann keine Entscheidung an Stelle des Kapitäns erfassen.

## Bevor Sie beginnen
- Prüfen Sie in der oberen Kontextleiste, dass das **richtige Unternehmen** ausgewählt ist. Die Seite listet nur Schiffe in Ihrem Zugriffsbereich auf (13.03 §1).
- Um die Seite zu sehen, benötigen Sie Lesezugriff auf die Abfahrtsbereitschaft.
- Die Entscheidungsberechtigung allein reicht zum Erfassen einer Entscheidung nicht aus: Sie benötigen zusätzlich eine **aktuelle Bestellung als Kapitän** für dieses Schiff. Ohne sie zeigt die Seite „You hold no current Master appointment for this ship. Decisions are read-only.“

## Schritt für Schritt
1. Wählen Sie das Schiff aus der Liste „Schiff wählen“.
2. Füllen Sie unter **Geplanter Abfahrtszeitraum** die Felder „Abfahrtsdatum“ und „Reiseende“ aus und drücken Sie **Bereitschaft bewerten**.
3. Lesen Sie in der Zeile **Bewertung** das Zustandsabzeichen, den Modus „Aktuell“ oder „Prognose“, „Bewertet am“ und „Gültig bis“.
4. Prüfen Sie in der Kategorientabelle die Ergebnisse für Besatzung, Technik, Vorschriften, Notfall und Sicherheitsereignisse.
5. Lesen Sie die Liste **Warum**. Jede Zeile zeigt eine nicht bestandene Beobachtung, ihren Grundcode und ihre Quelle. Prüfen Sie eine unerwartete Zeile auf der eigenen Seite der Quelle.
6. Ist die Bewertung nicht aktuell und Bereit, lesen Sie **Warum Freigeben nicht verfügbar ist**. Der Abschnitt sagt, wann der Zeitraum eine Prognose ist, listet die noch nicht bestandenen Kategorien mit ihren Ergebnissen auf und erklärt, welcher Datensatz die Quellenlücken bei Notfall (noch keine 61.02-Übungsquelle angebunden) und Vorschriften (kein Satz erforderlicher Zeugnisse definiert) schließen würde. Er erklärt nur die Kategorien des Servers; er ändert die Freigabeschranke nicht.
7. Unter **Kapitänsentscheidungen** sehen Sie frühere erfasste Entscheidungen und ihre Gültigkeit.
8. Wenn Sie der Kapitän sind, wählen Sie unter **Kapitänsentscheidung erfassen** Freigeben, Anhalten oder Ablehnen, wählen Ihre Bestellung unter „Bestellung“, schreiben eine „Begründung“ (10–2000 Zeichen) und drücken **Erfassen**.
9. Mit **Aktualisieren** lesen Sie die Bewertung erneut.

## Was die Zustände bedeuten
- **Bereit:** Keine erfasste Quelle hat eine nicht bestandene Beobachtung. Dies ist nur eine Information; die Abfahrtsentscheidung bleibt beim Kapitän.
- **Bedingt:** Mindestens eine Quelle meldet eine Bedingung. Freigeben wird für diese Bewertung nicht angeboten.
- **Nicht bereit:** Mindestens eine Quelle liefert „Nicht bestanden“. Dass andere Kategorien bestehen, gleicht das nicht aus.
- **Prüfung erforderlich:** Mindestens eine Eingabe ist „Unbekannt“. Eine berechtigte Person muss sie ansehen.
- **Bestanden / Condition / Nicht bestanden / Unbekannt:** das Kategorieergebnis in der Kategorientabelle. **Unbekannt** bedeutet, dass die Quelle nicht gelesen werden konnte oder keine Daten hat; es bedeutet weder „Bestanden“ noch „Bereit“. In dieser Version ist für die Kategorie Notfall keine Quelle zur Notfallvorsorge angebunden, daher zeigt diese Zeile Unbekannt. Vorschriften kann auch dann Unbekannt bleiben, wenn jedes erfasste Zeugnis gültig ist, weil keine Quelle den Satz der für das Schiff erforderlichen Zeugnisse definiert. Vorschriften umfasst auch Datenvertrauenssignale (DATA_TRUST_*); ohne Lesezugriff auf das Datenvertrauen zeigt Vorschriften Unbekannt.
- **Prognose:** Der Zeitraum schließt den heutigen Tag nicht ein. Es ist nur eine Planungsprognose und kann nicht freigegeben werden.
- **Aktuell / Stale — inputs changed:** die Gültigkeit einer erfassten Entscheidung. Ändern sich die Eingaben, wird die frühere Entscheidung veraltet und nicht gelöscht.

## Was Sie nicht tun können / Vorsicht
- Das System gibt nie eine Abfahrt frei und zeigt keinen Zustand als Abfahrtsgenehmigung. Eine erfasste Entscheidung ist die eigene menschliche Aufzeichnung des bestellten Kapitäns.
- Freigeben kann nur für eine aktuelle READY-Bewertung erfasst werden; jede Kategorie muss bestehen. Da die Kategorie Notfall in dieser Version Unbekannt bleibt, kann die Bewertung Bereit nicht erreichen, und Freigeben wird nicht angeboten; **Warum Freigeben nicht verfügbar ist** zeigt den Grund. Auch bei Bereit liegt die Freigabeentscheidung beim Kapitän; das System genehmigt nichts. „Anhalten und Ablehnen können jederzeit mit Begründung erfasst werden.“
- Für jede Entscheidung ist eine Begründung erforderlich. Erfasste Entscheidungen werden nicht geändert; eine neue Entscheidung ist ein neuer Datensatz.
- Solange die Bewertung veraltet ist oder Sie offline sind, können Sie keine Entscheidung erfassen; aktualisieren Sie zuerst.

## Wenn etwas schiefgeht
- **Sie sind offline:** Die Bewertung ist möglicherweise veraltet, und die Freigabe ist deaktiviert. Aktualisieren Sie, sobald Sie wieder online sind (13.03 §10).
- **Sie haben keinen Zugriff auf die Abfahrtsbereitschaft dieses Schiffs:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator.
- **Unerwartete Bereitschaft:** Öffnen Sie die Liste „Warum“; wirkt eine Quelle falsch, geben Sie es an den Kontrollverantwortlichen oder einen Fachexperten weiter.
- **Das Ergebnis ist unsicher:** „Erneut versuchen“ sendet denselben Befehl. Schreiben Sie keine neue Entscheidung.
- **Der Entscheidungsverlauf hat sich geändert / Die Bewertung hat sich geändert:** Verwenden Sie „Neu laden“, prüfen Sie die aktuelle Bewertung und entscheiden Sie dann.
- **Der Befehl wurde nicht angenommen:** Melden Sie es dem Support mit einem Screenshot, der Uhrzeit und Ihren Schritten.

## Verwandte Seiten
