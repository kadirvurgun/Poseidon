---
key: service-engineer-report
title: Service- und Technikerberichte
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Diese Seite erfasst und listet die **Berichte von Service- und Technikereinsätzen** auf dem ausgewählten Fahrzeug. Sie beantwortet: „Wer hat welche Arbeit an welcher Ausrüstung und für welchen Arbeitsauftrag ausgeführt, welche Teile und Messungen wurden erfasst, und ist der Bericht verifiziert?“ Ein Bericht ist mit einer Ausrüstungs-ID, einem Arbeitsauftrag und Mängeln verknüpft und damit an die Arbeitsauftrags- und Ausrüstungshistorie gebunden (13.03 §5). Ein verifizierter Bericht ist ein menschlicher Datensatz; er schließt weder einen Mangel noch einen Arbeitsauftrag.

## Wer sie verwendet
- **Leitender Ingenieur / Technischer Inspektor:** um Berichte mit der Ausrüstungs- und Arbeitsauftragshistorie zu verknüpfen, Fehlendes zu verfolgen und Berichte zu verifizieren, die sie nicht verfasst haben (13.03 §5).
- **Ingenieur:** um nach einem Service einen Bericht als Entwurf einzugeben und zur Verifizierung einzureichen (13.03 §6).

## Bevor Sie beginnen
- Ist kein Fahrzeug ausgewählt, zeigt die Seite **Wählen Sie ein Schiff.** und listet die Fahrzeuge in Ihrem Zugriffsbereich. Wählen Sie das richtige Fahrzeug.
- Zum Anzeigen von Berichten benötigen Sie Lesezugriff auf Serviceberichte und Ausrüstung. Die Schaltfläche **Neuer Bericht** wird nur mit Schreibzugriff und online angezeigt.
- Halten Sie die IDs von Ausrüstung, Arbeitsauftrag, Mangel und Nachweisdatei bereit. Die Felder für Arbeitsauftrag, Mangel und Nachweis werden nur angezeigt, wenn Sie diese Datensätze lesen können.

## Schritt für Schritt
1. Öffnen Sie einen Bericht aus der Liste **Berichte**, oder beginnen Sie mit **Neuer Bericht** einen neuen Entwurf.
2. Füllen Sie **Berichtsnr.**, **Dienstleister**, **Ingenieur**, **Servicedatum**, **Anlass und Umfang** und **Durchgeführte Arbeiten** aus.
3. Geben Sie bei Bedarf mit **Teil hinzufügen** und **Messung hinzufügen** die verwendeten Teile und die Messungen ein. Kennen Sie ein Messergebnis nicht, belassen Sie es auf **Unbekannt**.
4. Geben Sie unter **Verknüpfungen** die **Ausrüstungs-ID**, die **Arbeitsauftrags-ID** und die IDs der verknüpften Mängel und Nachweisdateien ein (eine pro Zeile).
5. Speichern Sie mit **Entwurf speichern**. Zeigt der Bericht eine Liste **Vor der Einreichung fehlend**, ergänzen Sie zuerst diese Punkte.
6. Senden Sie den Bericht mit **Zur Verifizierung einreichen**.
7. Bei einem eingereichten Bericht, den Sie nicht verfasst haben, schreiben Sie eine **Begründung** mit mindestens 10 Zeichen und verwenden **Verifizierung erfassen** oder **An Verfasser zurückgeben**.
8. Um einen verifizierten Bericht zu ändern, schreiben Sie eine Begründung und verwenden **Ändern (neue Revision)**. Jede Revision bleibt in **Unveränderliche Historie** erhalten.

## Was die Zustände bedeuten
- **Entwurf / Eingereicht / Zurückgegeben:** der Lebenszyklus des Berichts. Einreichen ist keine Verifizierung.
- **Verifiziert (erfasste menschliche Entscheidung) / Von einer Nicht-Verfasserin/einem Nicht-Verfasser verifiziert:** Eine berechtigte Person, die den Bericht weder verfasst noch eingereicht hat, hat eine Verifizierung erfasst. Abgeschlossen und Verifiziert sind nicht dasselbe.
- **Wartet auf Verifizierung:** Der Bericht wurde eingereicht und ist noch nicht verifiziert.
- **Eine frühere Revision wurde verifiziert, diese nicht:** Nach einer Änderung muss die neue Revision erneut verifiziert werden.
- **aktuelle Revision / seit der Verknüpfung geändert / nicht verfügbar:** der Zustand einer verknüpften Ausrüstung, eines Arbeitsauftrags oder Mangels im Vergleich zur verknüpften Revision.
- **Angenommene Übertragung, kein verifizierter Nachweis:** Eine angehängte Datei ist nur eine angenommene Übertragung.
- Ein **Unbekannt**-Messergebnis bedeutet nicht „Innerhalb der Grenzen“.

## Was Sie nicht tun können / Vorsicht
- Einen Bericht, den Sie verfasst oder eingereicht haben, können Sie nicht verifizieren; der Server lehnt dies ab.
- Ein verifizierter Bericht schließt weder einen Mangel noch einen Arbeitsauftrag, und er zeigt nicht, dass die Ausrüstung sicher ist.
- Originale von Nachweisen werden nicht geändert; eine Änderung eröffnet eine neue Revision, und die alte Revision bleibt im Verlauf (13.03 §1).
- Offline wird nichts erfasst. Wenn die Verbindung zurück ist, sendet **Denselben Befehl wiederholen** denselben Befehl erneut.

## Wenn etwas schiefgeht
- **Erforderliche Daten oder Nachweise fehlen:** Ergänzen Sie die Fehlliste und speichern Sie erneut.
- **Jemand anderes hat diesen Datensatz geändert:** Öffnen Sie mit **Neu laden** die neueste Version und speichern Sie dann erneut.
- **Ihnen fehlt die Serverberechtigung für diese Aktion oder diesen Datensatz:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Ein Bericht kann nicht verifiziert werden:** Prüfen Sie die verknüpften Datensätze und Nachweisdateien; sprechen Sie mit einem berechtigten Verifizierer (13.03 §10).
- **Der Datensatz konnte nicht geladen werden:** Senden Sie Bildschirm, Uhrzeit und Schritte an den Support (13.03 §10).

## Verwandte Seiten
