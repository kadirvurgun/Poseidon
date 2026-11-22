---
key: role-uat
title: Rollen-UAT-Aufzeichnungen
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Rollen-UAT-Aufzeichnungen erfasst Durchläufe von Benutzerakzeptanztests (UAT) mit Schiffs- und Schlepperrollen, deren Szenarioergebnisse und Befunde als **menschliche Nachweise** und vergleicht sie mit dem vereinbarten Schwellenwert. Sie beantwortet: „Erfüllen die bestätigten UAT-Nachweise für jede Rolle und jedes Szenario den Schwellenwert, den die Menschen vereinbart haben?“ Es gilt der Hinweis oben: „Erfasst nur menschliche UAT-Nachweise. Das System nimmt UAT nie ab und entscheidet nie über ein Pilot-GO; automatisierte Testergebnisse sind Anhänge, kein PASS.“

## Wer sie verwendet
- **UAT-Moderator:** eröffnet einen Durchlauf, erfasst die beobachteten Szenarioergebnisse und Befunde des Teilnehmers und reicht den Durchlauf zur Bestätigung ein. Zu den Teilnehmerrollen auf dem Schiff gehören Kapitän, DPA, Leitender Ingenieur und Crew (13.03 §2, §4, §5, §6).
- **Bestätigender:** Eine andere Person als der Moderator bestätigt den Durchlauf.
- **UAT-Verantwortliche / Product Owner:** erfassen den vereinbarten Schwellenwert und verwenden das Ergebnis auf der Seite **Pilot-Go/No-Go-Prüfung**.

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie mandantenweiten Lesezugriff und Lesezugriff auf UAT. Das Erfassen von Durchläufen und Ergebnissen, das Erfassen des Schwellenwerts und das Bestätigen benötigen jeweils eine eigene Berechtigung.
- Die UAT-Verantwortlichen müssen den Schwellenwert vorab vereinbart haben; das System legt nie einen fest.
- Wählen Sie für den Teilnehmer ein Pseudonym. Verwenden Sie nie einen Namen, eine E-Mail-Adresse oder eine Crew-Nummer.
- Das Erfassen benötigt eine aktive Verbindung; offline ist es deaktiviert.

## Schritt für Schritt
1. Wählen Sie mit den Schaltflächen **Betriebsmodus** **Schiff** oder **Schlepper**.
2. Prüfen Sie in **UAT-Nachweise gegen den vereinbarten Schwellenwert** den Zustand, die Anzahlen **Offene Blocker**, **Offene schwere Befunde**, **Bestätigte Durchläufe** und **Warten auf Bestätigung** sowie die Szenariotabelle.
3. Gibt es keinen Schwellenwert, füllen Sie in **Vereinbarten Schwellenwert erfassen** **Mindest-Bestehensquote je Szenario**, **Höchstzahl offener schwerer Befunde**, **Designkonformität**, **Vereinbarungsreferenz** und **Begründung** aus und drücken **Erfassen**.
4. Geben Sie in **UAT-Durchläufe** im Formular **Neuer UAT-Durchlauf** **Rolle**, **Umgebung**, **Build-Commit** und **Teilnehmer-Pseudonym** ein und drücken Sie dann **Erfassen**.
5. Öffnen Sie den Durchlauf. Wählen Sie mit **Ergebnis erfassen** das **Szenario** und das **Ergebnis**; ergänzen Sie bei Bedarf **Hilfe geleistet** und eine **Referenz automatisierter Nachweis**. Für ein Problem verwenden Sie **Befund erfassen** mit **Schweregrad** und **Befundzusammenfassung**.
6. Wenn Sie die Szenarien erfasst haben, drücken Sie **Durchlauf zur Bestätigung einreichen** (nur Moderator). Szenarien ohne Ergebnis zählen als **Nicht ausgeführt**. Eine andere Person als der Moderator schreibt in **Durchlauf bestätigen** eine **Bestätigungserklärung** und drückt **Erfassen**.
7. Schließen Sie in **Offene Befunde** einen Befund mit einer **Lösung** und einer **Erledigungsnotiz** und drücken Sie dann **Erledigen**.

## Was die Zustände bedeuten
- **Schwellenwert nicht gesetzt:** „Kein von Menschen vereinbarter Schwellenwert erfasst. UAT kann erst nach dessen Erfassung bestehen.“
- **Kriterien nicht erfüllt / Nachweise unvollständig:** Die bestätigten Nachweise erfüllen den Schwellenwert nicht, oder nicht jede Rolle und jedes Szenario ist abgedeckt.
- **Kriterien erfüllt; menschliche Entscheidung ausstehend:** der bestmögliche Zustand. Er ist keine UAT-Abnahme und kein Pilot-GO; die Entscheidung trifft ein Mensch.
- **Nicht ausgeführt:** Für das Szenario gibt es kein Ergebnis. Ein nicht ausgeführtes Szenario zählt nie als bestanden.
- **Offen / Eingereicht / Bestätigt:** der Zustand des Durchlaufs. Nur bestätigte Durchläufe zählen für den Schwellenwert.
- **Bestanden / Fehlgeschlagen / Blockiert / Nicht ausgeführt:** das vom Teilnehmer beobachtete Szenarioergebnis.
- **Blocker / Schwer / Gering:** der Schweregrad eines Befunds. Ein offener Blocker lässt das Gate scheitern.
- **Fiktive Testdaten:** ein synthetischer Beispieldatensatz; er ist kein echter UAT-Nachweis.

## Was Sie nicht tun können / Vorsicht
- Einen Durchlauf, den Sie moderiert haben, können Sie nicht bestätigen: „Die Bestätigung muss von einer anderen Person als dem Moderator erfasst werden.“
- Einen Befund, den Sie gemeldet haben, können Sie nicht erledigen. Ein Blocker wird nur als **Erledigt (behoben und erneut getestet)** geschlossen; **Risiko akzeptiert** wird nicht angeboten.
- Ein automatisiertes (Playwright-)Ergebnis ist nur ein angehängter Nachweis; das Ergebnis ist das vom Teilnehmer beobachtete Ergebnis.
- Das System nimmt UAT nie ab, legt nie einen Schwellenwert fest und erfindet nie Bestätigungen oder Freigaben.

## Wenn etwas schiefgeht
- **Sie haben keinen Zugriff auf diese Datensätze.** Prüfen Sie Ihre Rolle und Ihren Mandantenbereich und wenden Sie sich an den Mandantenadministrator (13.03 §10).
- **Der Datensatz wurde in Ihrem Mandanten nicht gefunden.** Prüfen Sie den Link zum Durchlauf und den ausgewählten Modus.
- **Der Datensatz hat sich geändert oder wurde bereits verwendet. Neu laden.** Öffnen Sie den aktuellen Stand mit **Neu laden**.
- **Das Ergebnis ist unsicher. Erneut versuchen sendet denselben Befehl.** Verwenden Sie **Erneut versuchen**, um denselben Befehl sicher erneut zu senden.
- **Durch die Regeln abgelehnt.** Zum Beispiel gehört das Szenario nicht zur Rolle des Durchlaufs, oder ein Blocker kann nicht als Risiko akzeptiert geschlossen werden.
- **Nicht erlaubt.** Zum Beispiel haben Sie diesen Durchlauf moderiert und können ihn nicht bestätigen, oder Sie haben diesen Befund gemeldet und können ihn nicht erledigen.
- **Sie sind offline. Erfassen ist bis zur Wiederverbindung deaktiviert.** Wenn die Verbindung zurück ist, drücken Sie **Aktualisieren**.

## Verwandte Seiten
