---
key: voice-event-draft
title: Sprach-Ereignisentwurf
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Sprach-Ereignisentwurf“ macht aus einer kurzen gesprochenen oder getippten Notiz **Entwurfsvorschläge** für eine Meldung eines Sicherheitsereignisses. Sie beantwortet: „Wurden Ereignisart, Ort, Zeit, Hergang und Sofortmaßnahme korrekt aus dem übernommen, was ich gesagt oder geschrieben habe?“ Die Vorschläge stammen aus festen lokalen Regeln, jeweils mit Rückverweis auf Ihre eigenen Worte und einer Konfidenz. Eine Person bearbeitet und bestätigt den Entwurf; nichts wird automatisch eingereicht, abgeschlossen oder gemeldet (13.03 §1, §6).

## Wer sie verwendet
- **Crew / OOW / Engineer und Kapitän:** um vor Ort eine schnelle Notiz zu machen und eine Meldung eines Sicherheitsereignisses zu beginnen (13.03 §2, §6).
- **DPA / HSEQ:** um **Gemessene Nutzbarkeit (dieses Schiff)** zu beobachten. Diese Zahlen sind eine betriebliche Beobachtung, keine Aussage über die Genauigkeit.

## Bevor Sie beginnen
- Wählen Sie ein Fahrzeug in Ihrem Zugriffsbereich. Zum Anlegen eines Entwurfs benötigen Sie die Schreibberechtigung für Sprachentwürfe und Bearbeitungsrechte auf diesem Fahrzeug.
- Für die Übergabe eines bestätigten Entwurfs an einen Sicherheitsereignis-Entwurf benötigen Sie zusätzlich die Berechtigung zum Melden von Sicherheitsereignissen.
- Datenschutz: Audio wird nicht gespeichert. Die Spracherkennung verwendet nur eine lokale Engine dieser Installation; ist keine installiert, tippen oder fügen Sie die Notiz ein. Vermeiden Sie Namen und persönliche Gesundheitsangaben.
- Offline wird nichts gesendet.

## Schritt für Schritt
1. Drücken Sie **Neue Sprach- oder Textnotiz**.
2. Ist eine lokale Engine konfiguriert, drücken Sie **Mit lokaler Engine aufnehmen** und dann **Stoppen und transkribieren**. Andernfalls tippen Sie eine kurze Notiz in **Notiztext**: was, wo, wann, was Sie getan haben.
3. Prüfen Sie **Datum der Notiz**. Es wird nur verwendet, um Wörter wie „heute“ oder „gestern“ aufzulösen.
4. Drücken Sie **Entwurfsvorschläge erstellen**.
5. Prüfen Sie jedes Feld: **Ereignisart**, **Zeitpunkt (Ortszeit)**, **Ort an Bord**, **Ortsdetail**, **Was ist passiert**, **Sofortmaßnahme**. Unter jedem Feld sehen Sie die Konfidenz des Vorschlags, die Regel und den passenden Teil Ihrer Notiz. Lesen Sie auch **Hinweise der Regeln**.
6. Korrigieren Sie falsche Vorschläge und drücken Sie **Änderungen speichern**. Ist **Vor der Bestätigung noch erforderlich:** leer, drücken Sie **Entwurf bestätigen**. Das Bestätigen erfasst, dass Sie jedes Feld geprüft haben; es reicht kein Ereignis ein und schließt keines ab.
7. Wurde nach der Bestätigung ein Sicherheitsereignis-Entwurf angelegt, folgen Sie **Sicherheitsereignis-Entwurf öffnen** zur Seite „Safety events & near misses“ und vervollständigen und reichen Sie ihn dort ein. Wurde noch keiner angelegt und besitzen Sie die Berechtigung, verwenden Sie **Sicherheitsereignis-Entwurf jetzt erstellen**.
8. Ist die Notiz kein Ereignis oder falsch, wählen Sie eine **Begründung**, fügen Sie bei Bedarf **Notiz (bei Sonstiges Pflicht)** hinzu und drücken Sie **Entwurf ablehnen**.

## Was die Zustände bedeuten
- **Entwurf (unbestätigt):** Noch keine Person hat die Vorschläge bestätigt.
- **Von einer Person bestätigt:** Die Felder wurden geprüft; dies ist keine Meldung eines Sicherheitsereignisses, und nichts wurde eingereicht.
- **Abgelehnt:** Der Ablehnungsgrund wird für die Auswertung erfasst.
- **Suggested, unchanged / Changed by you / Entered by you / Empty:** die Herkunft eines Felds. Suggested, unchanged ist ein Vorschlag, den eine Person noch prüfen muss.
- **Typed or pasted / Local speech engine:** wie die Notiz eingegeben wurde. Wird Text der lokalen Engine bearbeitet, wird er zu einer getippten Notiz.
- Regelhinweise wie **Uhrzeit ohne Tag; kein Datum angenommen** zeigen fehlende Informationen; das System errät den fehlenden Wert nicht.
- Ein Hinweis wie **2 missing**: wie viele Pflichtfelder des Entwurfs in der Liste noch leer sind.

## Was Sie nicht tun können / Vorsicht
- Vorschläge sind nur ein Entwurf; eine Konfidenz ist keine Garantie für Richtigkeit. Prüfen Sie jedes Feld selbst, bevor Sie bestätigen.
- Diese Seite reicht kein Sicherheitsereignis ein, schließt keines ab und meldet keines. Die Meldung reichen Sie auf der Seite „Safety events & near misses“ ein.
- Wird ein Nachweis erwähnt, fügen Sie ihn selbst an; die Seite fügt keinen Nachweis an.
- **Entwurf bestätigen** ist nicht verfügbar, solange Änderungen nicht gespeichert sind; speichern Sie sie zuerst.
- Ohne die Berechtigung zum Melden von Sicherheitsereignissen wird aus einem bestätigten Entwurf kein Sicherheitsereignis-Entwurf angelegt.

## Wenn etwas schiefgeht
- **Speech capability unavailable: no local engine is configured.** oder **Transcription was not completed.** Tippen oder fügen Sie die Notiz ein.
- **Mikrofonzugriff wurde nicht erteilt.** Prüfen Sie die Browserberechtigung oder tippen Sie die Notiz.
- **The record changed meanwhile.** Drücken Sie **Neu laden** und wiederholen Sie Ihre Bearbeitung.
- **You are offline. Nothing was sent.** Verwenden Sie online **Denselben Befehl wiederholen**.
- **Sie haben keine Berechtigung für diese Aktion.** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).

## Verwandte Seiten
