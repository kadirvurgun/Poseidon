---
key: towage-plan
title: Schleppplan
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Der Schleppplan erfasst für einen eingereichten Schlepperauftrag die **Schlepperrollen und Konfiguration, den Informationsaustausch, die Einsatzgrenzen, die Kommunikation, die Notauslösung, die Notfallplanung und die Kontrollen der Snap-back-Zone**. Er beantwortet: „Wie wird dieser Auftrag ausgeführt, innerhalb welcher Grenzen und mit welchen Notfallvorkehrungen?“ Die Genehmigung des Plans ist eine menschliche Aufzeichnung des bestellten Kapitäns oder Schlepperkapitäns; sie ist weder eine GO-Entscheidung noch eine Erlaubnis. Grenzwerte sind Planeingaben mit Angabe ihrer Quelle; das System liefert keine Standardwerte.

## Wer sie verwendet
- **Koordinator Schlepperbetrieb / Planer:** um den Plan vorzubereiten und zur Freigabe einzureichen (13.03 §7).
- **Schlepperkapitän / Kapitän:** um Konfiguration, Leine, BP/Leistungsfähigkeit, Kommunikation, Grenzwerte, Notfallplanung und Stop-Work-Bedingungen zu bestätigen und den Plan zu genehmigen oder zurückzugeben (13.03 §3).

## Bevor Sie beginnen
- Öffnen Sie den Schlepper über **Schlepperbetrieb** und wählen Sie den Auftrag unter **Eingereichten Schlepperauftrag auswählen**.
- Zum Bearbeiten des Plans benötigen Sie Schreibzugriff für Schlepper und Plan. Die Schlepper im Plan werden aus den Schleppern mit einem aktuellen Zuweisungsvorschlag für diesen Auftrag gewählt.
- Genehmigen oder Zurückgeben erfordert eine vom Server erfasste **Bestellung** (Schlepperkapitän oder Kapitän) für den koordinierenden Schlepper.

## Schritt für Schritt
1. Lesen Sie **Status** und **Revision**, dann die Liste **Vor der Übermittlung fehlend**.
2. Verwenden Sie unter **Schlepperrollen und Konfiguration** für jeden Schlepper **Hinzufügen**; geben Sie **Position**, **Schleppmethode**, **Koordinierender (führender) Schlepper**, **Herkunft der Schleppleine**, **Anschlusspunkt**, **Ausrüstungs-ID der Schleppleine** und **Ausrüstungs-ID der Notfreigabe** ein.
3. Schreiben Sie den Text für **Informationsaustausch Kapitän / Lotse / Schlepperkapitän**.
4. Geben Sie unter **Einsatzgrenzen (Planeingaben)** für jeden **Parameter** einen **Wert** und die **Quelle des Grenzwerts** ein. Ein neuer Grenzwert beginnt leer.
5. Füllen Sie **Kommunikationsplan** (Haupt- und Reservekanal, Arbeitssprache, Verfahren bei Kommunikationsverlust), **Notauslösung**, **Notfallplanung** und **Kontrollen der Snap-back-Zone** aus.
6. Speichern Sie mit **Entwurf erstellen** oder **Entwurf speichern**; sobald nichts mehr fehlt, drücken Sie **Zur Freigabe einreichen**.
7. Der Genehmigende wählt eine **Bestellung**, schreibt eine **Begründung** von mindestens 10 Zeichen und drückt **Genehmigen** oder **Zurückgeben**.
8. Muss ein genehmigter Plan geändert werden, schreiben Sie eine Begründung und verwenden Sie **Ändern**. Die Listen **Freigabeaufzeichnungen** und **Revisionen** zeigen die Historie.

## Was die Zustände bedeuten
- **Entwurf:** Der Plan kann bearbeitet werden.
- **In Prüfung:** Der Plan wurde zur Freigabe eingereicht; eine Entscheidung wird erwartet.
- **Zurückgegeben:** Der Genehmigende hat den Plan mit einer Begründung zurückgeschickt; korrigieren Sie ihn und reichen Sie ihn erneut ein.
- **Genehmigt:** Der Kapitän oder Schlepperkapitän hat diese Revision des Plans genehmigt. Dies ist keine GO-Entscheidung; Bereitschaftsprüfung und Briefing sind weiterhin erforderlich.
- **Unbekannt:** Für das Feld wurde kein Wert gewählt. „Unbekannt“ bei Snap-back-Zonen steht niemals für „Nein“.
- **Die Schlepperanforderung wurde nach dem Speichern dieses Plans geändert.:** Der Plan beruht auf einer älteren Revision der Anforderung und muss geprüft werden.

## Was Sie nicht tun können / Vorsicht
- Die Person, die den Plan verfasst oder eingereicht hat, kann ihn nicht genehmigen.
- Ein Plan mit fehlenden Pflichtabschnitten kann nicht eingereicht werden; ein unvollständiger Entwurf kann gespeichert werden.
- Das System schlägt keinen Grenzwert, Kanal oder Schwellenwert vor; jeder Wert stammt vom Planer mit seiner Quelle.
- Ein genehmigter Plan ist keine Erlaubnis zum Start; der Start erfordert aktuelle Bestätigungen des Briefings und den GO-Eintrag des Schlepperkapitäns (13.03 §3).
- Offline sind Befehle deaktiviert.

## Wenn etwas schiefgeht
- **Meldung „Required sections are missing.“:** Vervollständigen Sie die Codes unter **Vor der Übermittlung fehlend**.
- **Meldung „The record changed“:** Verwenden Sie **Neu laden**, um den aktuellen Plan zu prüfen.
- **Meldung „The outcome is unknown“:** **Erneut versuchen** sendet dieselbe Anfrage erneut.
- **Keine Berechtigung:** Prüfen Sie Ihre Rolle, Ihren Schlepper- und Bestellungskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
