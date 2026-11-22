---
key: tug-job-intake
title: Auftragserfassung
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Auftragserfassung strukturiert eine neue **Schlepperauftragsanfrage als Entwurf** im ausgewählten Schlepperkontext. Sie beantwortet: „Was sind für diesen Auftrag das unterstützte Schiff, der Auftragstyp, das Zeitfenster, der Ort und die erforderlichen Ressourcen (Schlepperanzahl, Pfahlzug)?“ Die gespeicherte Anfrage ist eine Entwurfsangabe; sie ist weder eine betriebliche Zuweisung noch eine Eignung, Erlaubnis oder Sicherheitsfreigabe.

## Wer sie verwendet
- **Koordinator Schlepperbetrieb / Landbetrieb:** um die Anfrage zu strukturieren und unterstütztes Schiff, Auftragstyp, Zeitfenster und erforderliche Leistungsfähigkeit einzugeben (13.03 §7).

## Bevor Sie beginnen
- Wählen Sie in der oberen Kontextleiste das **richtige Unternehmen und den richtigen Schlepper**.
- Sie benötigen Schreibzugriff auf Schlepper. Ohne ihn öffnet sich das Formular nicht, und **Zugriff ist nicht verfügbar.** wird angezeigt.
- Unvollständige Entwürfe können gespeichert werden. Erforderliche Betriebsdaten und die berechtigte Prüfung müssen vor der Übermittlung geklärt sein.

## Schritt für Schritt
1. Füllen Sie unter **Anfrage und unterstütztes Schiff** die Felder **Anfragequelle**, **Kundenreferenz**, **Unterstütztes Schiff**, **IMO des unterstützten Schiffs**, **Auftragstyp**, **Priorität** und **Anfrageverantwortung** aus. Listen ohne Auswahl bleiben „Unbekannt“.
2. Geben Sie unter **Angeforderte Zeit und Ort** **Angeforderter Beginn (UTC)**, **Angefordertes Ende (UTC, bedingt)**, **Einsatzgebiet**, **Übernahmepunkt** und **Zielpunkt** ein. Zeiten sind in UTC.
3. Geben Sie unter **Ressourcenbedarf und Hinweise** **Erforderlicher BP (Tonnen, exakte Dezimalzahl)**, **Erforderliche Schlepperanzahl**, **Besondere Anforderungen** und **Notizen** ein.
4. Drücken Sie **Entwurf speichern**. Bei Erfolg öffnen sich die Auftragsdetails mit **Entwurf gespeichert. Betriebliche Freigabe bleibt nicht verfügbar.**
5. Um Ihre Bearbeitungen zu verwerfen, verwenden Sie **Änderungen verwerfen**.
6. Verwenden Sie in den Auftragsdetails **Anfrage übermitteln**, um die Anfrage für die Planung zu erfassen. Fehlen erforderliche Daten, wird die Übermittlung blockiert und die fehlenden Felder werden aufgeführt.

## Was die Zustände bedeuten
- **Entwurf:** Die Anfrage ist gespeichert und bearbeitbar; sie ist noch nicht übermittelt.
- **Eingereicht:** Die Anfrage ist für die Planung erfasst. Sie ist weder eine Genehmigung noch eine Zuweisung oder Bereitschaftsentscheidung.
- **Storniert:** Die Anfrage ist geschlossen; sie kann nicht neu gestartet werden.
- **Unbekannt:** Für das Feld wurde kein Wert gewählt. Unbekannt ist kein Standardwert und steht niemals für **Nein** oder **Nicht zutreffend**.
- **Prüfen Sie diesen Wert.:** Der Server hat dieses Feld abgelehnt; korrigieren Sie es und speichern Sie erneut.

## Was Sie nicht tun können / Vorsicht
- Diese Seite weist keinen Schlepper zu und erklärt den Auftrag niemals für bereit. Der Zuweisungsvorschlag ist ein eigener Schritt und setzt niemals die Sicherheitsentscheidung des Schlepperkapitäns an Bord außer Kraft (13.03 §7).
- Eine Anfrage kann nicht übermittelt werden, solange erforderliche Daten fehlen; ein unvollständiger Entwurf kann dennoch gespeichert werden.
- Felder einer übermittelten Anfrage können über dieses Formular nicht geändert werden; eine stornierte Anfrage kann nicht neu gestartet werden, legen Sie stattdessen eine neue an.
- Offline wird der Entwurf nur auf dieser Seite gehalten und kann nicht gespeichert werden.

## Wenn etwas schiefgeht
- **Meldung „Save outcome uncertain“:** Der genaue Versuch wird zurückgehalten; verwenden Sie nach der Wiederverbindung **Erneut versuchen**, um denselben Befehl zu senden. Das Verwerfen lokaler Eingaben storniert keinen bereits gesendeten Befehl.
- **Meldung „Conflict“:** Ihr Entwurf wird zurückgehalten; verwerfen und neu laden Sie vor einem neuen Versuch.
- **Felder prüfen:** Korrigieren Sie die markierten Felder.
- **Zugriff ist nicht verfügbar:** Prüfen Sie Ihre Rolle und Ihren Schlepperkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
