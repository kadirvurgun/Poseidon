---
key: renewal-workflow
title: Zeugniserneuerung
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite Zeugniserneuerung erfasst Schritt für Schritt die Erneuerungsvorbereitung und den internen Ablauf eines Zeugnisses: Vorbereitung, Antrag, Warten auf die Behörde, den ausgestellten Ersatz, Verifizierung und Abschluss. Sie beantwortet: „Wo steht die Erneuerung dieses Zeugnisses, und wer macht den nächsten Schritt?“ Sie ist nur ein interner Ablaufdatensatz; sie begründet weder rechtliche Gültigkeit noch behördliche Genehmigung (13.03 §1, §8).

## Wer sie verwendet
- **Kapitän, Leitender Ingenieur / Technischer Inspektor:** bereiten den Erneuerungsentwurf vor und erfassen Anforderungen und Antrag.
- **Zugewiesener Erneuerungsverantwortlicher:** treibt den Ablauf voran. Es ist eine Rolle von Mitgliedern mit der Berechtigung für Erneuerungsverantwortung; ohne sie kann der Ablauf nicht eröffnet oder validiert werden.
- **Verifizierer und Abschließender:** vergleicht den ausgestellten Ersatz mit seiner Quelle und verifiziert ihn, und – als andere Person – schließt die Erneuerung ab (13.03 §1, §8).

## Bevor Sie beginnen
- Zum Anzeigen benötigen Sie Lesezugriff auf Zeugnisse. Einen Entwurf vorbereiten benötigt Schreibzugriff auf Zeugnisse und auf Erneuerungen für dieses Fahrzeug; Verifizieren benötigt die Verifizierungsberechtigung für Erneuerungen; Abschließen die Abschlussberechtigung für Erneuerungen.
- Wählen Sie zuerst unter **Schiff auswählen** das richtige Fahrzeug (13.03 §1).
- Das zu erneuernde Zeugnis muss eine aktuelle gespeicherte Revision haben. Ein ersetztes Zeugnis kann nicht erneuert werden.
- Ablaufschritte benötigen eine aktive Verbindung. Offline sehen Sie „Sie sind offline. Ablaufschritte sind bis zur Wiederverbindung nicht verfügbar.“

## Schritt für Schritt
1. Öffnen Sie einen **Erneuerungsdatensatz** aus **Erfasste Erneuerungsentwürfe**. Für eine neue Erneuerung gelangen Sie mit **Zeugnis zur Erneuerung auswählen** zur [Zeugniszentrale](/app/certificate-command-center) und drücken dort beim Datensatz **Erneuerung vorbereiten**.
2. Füllen Sie das **Formular zur Zeugniserneuerung** aus: **Angegebenes Fälligkeitsdatum der Erneuerung**, das Besichtigungsfenster, **Geplanter Ort**, **Zugewiesene Rolle der Erneuerungsverantwortung** sowie **Angabe zur erforderlichen Besichtigung**, **Angabe zu erforderlichen Dokumenten** und **Angabe zu erforderlichen Maßnahmen**. Einen Wert, den Sie nicht kennen, belassen Sie auf **Unbekannt**.
3. Speichern Sie den Entwurf mit **Ungeprüften Entwurf speichern**.
4. Wählen Sie in **Verlängerungsablauf** unter **Verfügbare Schritte** den für Ihre Rolle offenen Schritt, zum Beispiel **Anforderungen als erfüllt bestätigen**, **Antrag erfassen** oder **Behördenentscheidung abwarten**. Füllen Sie die Pflichtfelder aus und drücken Sie **Schritt speichern**.
5. Wenn die Behörde das neue Zeugnis ausstellt, verwenden Sie **Ausgestellten Ersatz erfassen** und wählen es aus. Sein eigenes Dokument wird als Nachweis verwendet.
6. Der Verifizierer verwendet **Ersatz prüfen** und setzt durch Vergleich mit der Quelle **Aussteller stimmt überein**, **Zertifikatsnummer stimmt überein**, **Gegenstand stimmt überein**, **Daten stimmen überein** und **Geltungsbereich stimmt überein**.
7. Eine andere Person schließt die Erneuerung mit **Abschließen und ersetzen** ab. Hat die Behörde abgelehnt, verwenden Sie **Behördliche Ablehnung erfassen**; wird die Vorbereitung aufgegeben, verwenden Sie **Vorbereitung abbrechen**.

## Was die Zustände bedeuten
- **Entwurf / In Vorbereitung:** Die Erneuerung wird vorbereitet; das Formular kann bearbeitet werden.
- **Bereit zur Antragstellung / Antrag erfasst / Wartet auf Behörde:** die Antragsphase. Die Behördenentscheidung erfolgt außerhalb des Systems.
- **Ausgestellt, ungeprüft:** Der Ersatz ist erfasst, wurde aber noch nicht von einer Person mit seiner Quelle verglichen.
- **Vom Prüfer bestätigt:** Der Verifizierer hat den Vergleich erfasst; die Erneuerung ist noch nicht abgeschlossen.
- **Abgeschlossen:** Der interne Ablauf ist geschlossen, und der Ersatz ersetzt das Original. Dies ist keine Entscheidung über die rechtliche Gültigkeit des Zeugnisses.
- **Storniert / Abgelehnt:** Der Ablauf wurde beendet.
- Eine Anforderung kann **Erfüllt / Nicht erforderlich / Unbekannt** sein. **Unbekannt** bedeutet nicht „Nicht erforderlich“; der Server bringt den Ablauf bei einer unbekannten Anforderung nicht weiter.

## Was Sie nicht tun können / Vorsicht
- Wer den ausgestellten Ersatz erfasst oder dieses Ersatzzeugnis eingegeben hat, kann ihn nicht verifizieren. Der Abschließende muss sich vom Verifizierer, von der Person, die den ausgestellten Ersatz erfasst hat, und vom Verfasser des Ersatzzeugnisses unterscheiden.
- Verifizierung und Abschluss sind ausschließlich menschliche Entscheidungen; Abgeschlossen ≠ Verifiziert.
- Planungsangaben begründen weder abgeschlossene Besichtigungen noch genehmigte Anträge, Reisetauglichkeit oder Verifizierung.
- Entwurfsverknüpfungen zu einem Ersatz ersetzen das Original nicht. Das alte Zeugnis wird nur durch den Abschlussschritt mit einer nachvollziehbaren Ersetzungsverknüpfung geschlossen (13.03 §1).
- Jeder Schritt wird als unveränderliche Revision erfasst; der Verlauf bleibt in **Erfasster Revisionsverlauf**.
- Ohne ausdrücklich zugewiesenen Verantwortlichen fällt das System nie auf einen Administrator zurück.

## Wenn etwas schiefgeht
- **Für Ihre Rolle ist in diesem Status kein Schritt verfügbar.** Der nächste Schritt gehört zu einer anderen Rolle; prüfen Sie Ihre Berechtigung und Bestellung und wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).
- **Die Verlängerung wurde nach dem Laden geändert.** oder **Dies ist nicht die aktuelle Revision.** Drücken Sie **Neu laden** und prüfen Sie den aktuellen Stand.
- **Das Ergebnis ist unbekannt. Erneutes Versuchen sendet dieselbe Anfrage noch einmal.** Verwenden Sie **Dieselbe Anfrage wiederholen**.
- **Wegen Funktionstrennung abgelehnt: Sie haben einen früheren Schritt dieser Erneuerung erfasst, daher muss eine andere Person diesen Schritt ausführen.** Der Datensatz wurde nicht geändert; Neuladen hilft nicht. Überlassen Sie den Schritt einer anderen berechtigten Person.
- **Der Schritt wurde nicht gespeichert. Angaben prüfen und erneut versuchen.** Prüfen Sie die Pflichtfelder und die Übereinstimmungsauswahl.
- Kann der Ersatz nicht verifiziert werden, prüfen Sie das Dokument und die Quellenherkunft und verweisen Sie ihn an den Verifizierer (13.03 §10).

## Verwandte Seiten
