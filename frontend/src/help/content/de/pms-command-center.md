---
key: pms-command-center
title: PMS-Leitstand
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Der PMS-Leitstand sammelt die Instandhaltungs-**Ausnahmen** des ausgewählten Fahrzeugs auf einem Bildschirm. Er beantwortet: „Hat dieses Fahrzeug kritische überfällige Aufgaben, offene Arbeitsaufträge, Mängelwarnungen oder kritische Ersatzteillücken, und warum?“ Die Seite ist eine beratende Ausnahmeansicht; sie ist keine Entscheidung über Tauglichkeit, Bereitschaft oder Sicherheit. Unauffällige Punkte werden nicht aufgelistet, nur gezählt.

## Wer sie verwendet
- **Leitender Ingenieur / Technischer Inspektor:** um überfällige Aufgaben, offene Arbeitsaufträge, Mängelwarnungen und kritische Ersatzteillücken zu verfolgen (13.03 §5).
- **Kapitän:** um die betrieblichen Einschränkungen aus PMS- und Mängeldatensätzen zu sehen (13.03 §2).

## Bevor Sie beginnen
- Zum Anzeigen der Seite benötigen Sie die Leseberechtigung für Ausrüstung (`equipment.read`).
- Jeder Abschnitt hängt von der Leseberechtigung seines eigenen Moduls ab. Ohne Leseberechtigung für Mängel bleibt zum Beispiel der Abschnitt **Zustandswarnungen** auf **Zugriff erforderlich: nicht angezeigt**.
- Wählen Sie zuerst ein Fahrzeug. Ohne Fahrzeug zeigt die Seite nur die Liste **Wählen Sie ein Schiff.**

## Schritt für Schritt
1. Wählen Sie das Fahrzeug aus der Liste. Ist keines in Ihrem Zugriffsbereich, zeigt die Seite „In Ihrem Bereich ist kein Schiff.“
2. Lesen Sie oben das Kennzeichen **Ausnahmeübersicht** und die Zeit **Zuletzt aktualisiert**.
3. Prüfen Sie die Abschnitte: **Überfällige kritische Aufgaben**, **Fällige, überfällige und zu prüfende Aufgaben**, **Offene Arbeitsaufträge**, **Zustandswarnungen** und **Kritische Ersatzteillücken**.
4. Öffnen Sie bei einem Punkt **Warum**, um die erfassten Begründungscodes und, falls vorhanden, die Zeit **Beobachtet** zu sehen.
5. Mit **Quelldatensatz öffnen** gelangen Sie zur Quelle. Ein Mängelpunkt öffnet [Mängel und vorläufige Maßnahmen](/app/defects), ein Arbeitsauftragspunkt öffnet die [Arbeitsauftragszentrale](/app/work-order-center).
6. Drücken Sie **Aktualisieren**, um die Daten erneut zu lesen.

## Was die Zustände bedeuten
- **Blockiert / Prüfung erforderlich / Achtung:** der Zustand der Ausnahmeübersicht. Einzelheiten stehen in den Punkten der Abschnitte.
- **Keine offenen Ausnahmen (beratend):** Die gelesenen Quellen lieferten keine offene Ausnahme. Dies ist kein Bereitschafts- oder Tauglichkeitsergebnis.
- **Kritisch / Achtung / Prüfung erforderlich:** der Schweregrad eines Punkts.
- **Quelle nicht verfügbar: unbekannt, nicht null:** Die Quelle des Abschnitts konnte nicht gelesen werden. Unbekannt bedeutet weder „null“ noch „kein Problem“ noch „Bereit“.
- **Zugriff erforderlich: nicht angezeigt:** Sie haben für diesen Abschnitt keine Berechtigung; weder Inhalt noch Anzahl werden angezeigt.
- **Keine Ausnahmen aus dieser Quelle:** Der Abschnitt wurde gelesen und lieferte keine Ausnahme. Die Anzahl unauffälliger Punkte wird getrennt angezeigt.
- **Veraltete Daten / Aktualität unbekannt:** Die Ablesung hinter dem Punkt ist nicht aktuell, oder ihre Aktualität ist unbekannt.

## Was Sie nicht tun können / Vorsicht
- Die Seite ändert keinen Datensatz; sie liest nur. Arbeitsaufträge, Mängel und Ersatzteile werden auf ihren eigenen Seiten bearbeitet.
- Die Seite erklärt ein Fahrzeug nie für „bereit“ oder „sicher“. Die Abfahrtsentscheidung liegt beim Kapitän (13.03 §2).
- Ein Mangel gilt nur durch einen menschlichen Abschlussdatensatz als geschlossen, und nur für die Revision der Angabe, die abgeschlossen wurde. Ändert sich die Angabe später, kehrt der Punkt in die Prüfung zurück.
- Offline angezeigte Daten können veraltet sein, und **Aktualisieren** ist nicht verfügbar.

## Wenn etwas schiefgeht
- **Der Datensatz konnte nicht geladen werden:** Prüfen Sie Ihre Verbindung und verwenden Sie **Neu laden**.
- **Ihnen fehlt die Serverberechtigung für diese Aktion oder diesen Datensatz:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Unerwartete Ausnahme:** Öffnen Sie **Warum** und prüfen Sie den Quelldatensatz mit dem zuständigen Verantwortlichen oder Fachexperten (13.03 §10).
- **Anwendungsfehler:** Melden Sie dem Support die Correlation ID, den Bildschirm, die Uhrzeit und Ihre Schritte (13.03 §10).

## Verwandte Seiten
