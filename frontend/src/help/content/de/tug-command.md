---
key: tug-command
title: Schlepperzentrale
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Wofür diese Seite dient
Diese Seite, im Menü als **Schlepperzentrale** angezeigt, zeigt den **Quellenkontext** des ausgewählten Schleppers. Sie beantwortet: „Welche Angaben zu Rolle, Typ, Pfahlzug (BP) und Ausrüstung sind für diesen Schlepper erfasst, woher stammen sie und wie aktuell sind sie?“ Die Seite zeigt nur Angaben und ihre Quellen; sie erteilt keine Einsatzbereitschaft, Eignung, Erlaubnis oder Sicherheitsfreigabe. Das Kennzeichen oben sagt es: **Quellenangaben · Einsatzbereitschaft unbekannt**.

## Wer sie verwendet
- **Schlepperkapitän:** um vor Arbeitsbeginn Rolle, BP und Ausrüstungskontext des eigenen Schleppers zu bestätigen (13.03 §3).
- **Koordinator Schlepperbetrieb / Landbetrieb:** um sicherzustellen, dass sie im richtigen Schlepperkontext sind, bevor sie zur Auftragsübersicht und Auftragserfassung wechseln (13.03 §7).
- **Besatzung:** um ihre Rollenansicht und die für sie geöffneten Schiffsmodus-Bildschirme zu sehen (13.03 §6).

## Bevor Sie beginnen
- Sie benötigen den Schleppermodus und Lesezugriff auf Schlepper. Ohne ihn zeigt die Seite **Zugriff ist nicht verfügbar.**
- Ist kein Kontext ausgewählt, listet die Seite die berechtigten Schlepper unter **Schlepper auswählen** auf; öffnen Sie einen davon.
- Die Ausrüstungsliste erfordert zusätzlich Lesezugriff auf Ausrüstung; ohne ihn wird die Ausrüstungsquelle als „unavailable or not granted“ angezeigt.

## Schritt für Schritt
1. Öffnen Sie den Schlepper aus der Liste **Schlepper auswählen**. Die Zeile **Schlepperkontext** unter der Überschrift zeigt Code, Name und Modus des Schleppers.
2. Lesen Sie unter **Rolle (Serverzuweisung)** und **Menüansicht Schleppermodus** die Rolle und Menüansicht, die der Server Ihnen zugewiesen hat.
3. Sehen Sie unter **Modussichtbarkeit**, ob Schiffsmodus-Bildschirme geöffnet sind. Der Abschnitt **Schiffsmodus-Bildschirme** verlinkt nur auf Bildschirme, die Ihnen gewährt wurden.
4. Prüfen Sie anhand von **Schleppertyp**, **Typquelle / Revision** und **Typquellenbeobachtung / Regel**, aus welcher Quelle und Revision der Typ stammt.
5. Lesen Sie **BP im Profil (ungeprüft)**, **BP-Quelle / Version / Herkunft** und **BP-Aktualität / Beobachtung**. Dies ist ein angegebener, kein gemessener Wert.
6. Sehen Sie unter **Ausrüstungskontext** die Ausrüstungsangaben, ihre Stufen, Revisionen und Herkunft.
7. Wechseln Sie über die Seitenregisterkarten zu **Auftragsübersicht** oder (mit Schreibzugriff) **Auftragserfassung**, oder drücken Sie (mit Schreibzugriff) **Neue Anfrage**.

## Was die Zustände bedeuten
- **Unbekannt — keine Rollenzuweisung:** Der Server hat für Sie auf diesem Schlepper keine Rollenzuweisung. Das bedeutet nicht „keine Rolle, kein Problem“; eine Zuweisung durch einen Administrator ist erforderlich.
- **Role labels unavailable / Menu lens unavailable:** Die Quelle kann gerade nicht gelesen werden. Fehlende Informationen werden nicht als leer gewertet.
- **Keine Ansicht für den Schleppermodus konfiguriert:** Für Sie ist in diesem Modus keine Menüansicht definiert.
- **Im Schleppermodus ausgeblendet. Eine modusübergreifende Rolle ist erforderlich.:** Schiffsmodus-Bildschirme erfordern eine ausdrücklich berechtigte modusübergreifende Rolle.
- **BP im Profil (ungeprüft):** der angegebene Pfahlzug. Er ist kein verifiziertes Testergebnis.
- **Keine Ausrüstungsangaben zurückgegeben. Verifizierte Betriebstauglichkeit bleibt unbekannt.:** Eine leere Liste bedeutet niemals, dass die Ausrüstung tauglich ist.
- **Unknown / Unavailable:** Der Quellwert fehlt oder konnte nicht gelesen werden. Unbekannt steht niemals für **Bereit** oder **Nein**.

## Was Sie nicht tun können / Vorsicht
- Auf dieser Seite wird kein Feld bearbeitet und kein Bereitschaftsergebnis erzeugt. Die Betriebsentscheidung und die Sicherheitsbefugnis bleiben beim Schlepperkapitän an Bord (13.03 §3).
- Ein Zuweisungsvorschlag von Land setzt niemals die Sicherheitsentscheidung des Schlepperkapitäns an Bord außer Kraft (13.03 §7).
- Auch mit einer angegebenen modusübergreifenden Berechtigung erfordert jeder Schiffsmodus-Bildschirm weiterhin seine eigene Serverberechtigung.
- Offline werden keine aktuellen Profilquellen angezeigt; stellen Sie die Verbindung wieder her, um Zugriff und Quellversionen erneut zu prüfen.

## Wenn etwas schiefgeht
- **Zugriff ist nicht verfügbar:** Prüfen Sie Ihre Rolle, Ihren Schlepper- und Objektkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Warnung „Current identity and vessel access must be checked“:** Drücken Sie **Zugriff erneut prüfen**.
- **Daten nicht verfügbar:** Prüfen Sie Ihre Verbindung und drücken Sie **Erneut versuchen**.
- **Unerwartete BP- oder Ausrüstungswerte:** Notieren Sie die Felder für Quelle und Version und prüfen Sie sie mit dem Verantwortlichen der Quelle; kann ein Datensatz nicht verifiziert werden, prüfen Sie seinen Nachweis und seine Herkunft (13.03 §10).

## Verwandte Seiten
