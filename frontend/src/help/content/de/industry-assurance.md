---
key: industry-assurance
title: Branchen-Assurance (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Branchen-Assurance“ importiert SIRE-2.0-Inspektionsergebnisse für einen Tanker aus einer Arbeitsmappe, legt die Änderungen zur menschlichen Prüfung vor, verknüpft Befunde mit Sicherheitsmaßnahmen und erfasst Vor-Ort-Nachweise. Sie beantwortet: „Welche Befunde dieser Inspektion sind offen, bei welchen ist eine Maßnahme verknüpft, und wo ist der Nachweis?“ Importe sind Entwürfe: Jede geänderte Position nimmt eine andere Person an. Befunde bleiben offen und unverifiziert; diese Seite schließt oder verifiziert sie nie. Die Kette lautet Befund → Maßnahme → Verifizierung, und Verifizierung und Wirksamkeit sind getrennte menschliche Aufzeichnungen an der Maßnahme unter „Sicherheitsereignisse und Beinahe-Unfälle“ (13.03 §4, §8).

## Wer sie verwendet
- **DPA / HSEQ:** verknüpft Inspektionsbefunde mit Maßnahmen und verfolgt die Kette Befund → Maßnahme → Verifizierung (13.03 §4).
- **Regulatory-/Assurance-Nutzer:** verwaltet Spaltenzuordnungen und bereitet Importe vor oder prüft sie (13.03 §8).
- **Kapitän und Schiffspersonal:** erfassen Vor-Ort-Nachweise (Fotos) für einen Befund.

## Bevor Sie beginnen
- Wählen Sie ein Schiff. SIRE 2.0 gilt nur für Tanker. Ist der Schiffstyp im Schiffsprofil nicht angegeben, bleiben Importe bei **Prüfung erforderlich**.
- Zum Lesen benötigen Sie die Berechtigung zum Lesen von Inspektionen. Importieren, Annehmen eines Imports, Schreiben von Nachweisen, Verknüpfen von Befunden und Schreiben von Zuordnungen sind getrennte Berechtigungen. Das Verknüpfen einer Maßnahme erfordert zusätzlich die Berechtigung zum Lesen von Sicherheitsereignissen; das Erfassen von Nachweisen zusätzlich Lesezugriff auf Dateien.
- Das Spaltenschema ist synthetisch; das lizenzierte OCIMF-Format wird nicht verwendet.
- Offline werden nur Fotoaufnahmen auf diesem Gerät in die Warteschlange gestellt; Importe und Entscheidungen benötigen eine Verbindung.

## Schritt für Schritt
1. **Schiff auswählen** und prüfen Sie die Zeile **Anwendbarkeit**.
2. Geben Sie bei Bedarf unter **Spaltenzuordnungen** einen **Zuordnungsschlüssel** ein, schreiben Sie eine Zeile pro Spalte (Kopfzeile=Feld) und drücken Sie **Als neue Version speichern**.
3. Wählen Sie unter **Import vorbereiten** die **Zuordnungsversion**, **Inspektionsreferenz**, **Inspektionsdatum** und **Arbeitsmappe (.xlsx)** und drücken Sie dann **Zur Prüfung vorlegen**.
4. Eine andere Person wählt unter **Importprüfung** für jede geänderte Position **Annehmen** oder **Aktuellen Wert behalten**, schreibt eine **Begründung** von mindestens 10 Zeichen und drückt **Import annehmen** oder **Import ablehnen**.
5. Öffnen Sie eine Inspektion aus der Liste **Inspektionen**. Prüfen Sie **Punkte** und **Befunde**.
6. Um einen Befund mit einer Maßnahme zu verknüpfen, geben Sie die **ID der Sicherheitsmaßnahme** ein und drücken **Maßnahme verknüpfen**.
7. Für einen Vor-Ort-Nachweis geben Sie **Medium**, **ID der sauberen Datei**, **Dateirevision** und **Datei-Prüfsumme (SHA-256)** ein und drücken **Vor-Ort-Nachweis erfassen**. Offline wird die Schaltfläche zu **Auf diesem Gerät speichern**; senden Sie den Nachweis online mit **Jetzt senden**.

## Was die Zustände bedeuten
- **Tanker: applicable / Not applicable / Ship type unknown: review required:** Bei unbekanntem Schiffstyp bleibt das Ergebnis **Prüfung erforderlich**; es wird nie als nicht anwendbar angenommen.
- **Awaiting review / Accepted / Rejected:** der Zustand des Imports.
- **New / Changed / Unchanged / Not in this import:** die Position im Vergleich zum zuvor angenommenen Import. Eine Position, die nicht in diesem Import enthalten ist, wird **Behalten (nie gelöscht)**.
- **Negative observation / Positive observation / No observation / Unknown:** die Beobachtung zu einer Position.
- **Open / Unverified:** Befunde werden hier immer als offen und unverifiziert angezeigt.
- **No action linked / Action linked:** ob der Befund mit einer Sicherheitsmaßnahme verknüpft ist. Eine verknüpfte Maßnahme bedeutet nicht, dass der Befund verifiziert oder die Maßnahme wirksam ist.
- **Captured online / Captured offline:** wie der Nachweis erfasst wurde.

## Was Sie nicht tun können / Vorsicht
- Die Person, die einen Import vorbereitet hat, kann ihn nicht prüfen: „Sie haben diesen Import vorbereitet; eine andere Person muss ihn prüfen.“
- Diese Seite schließt oder verifiziert keine Befunde. Verifiziert ist nicht Wirksam; die Wirksamkeit ist eine getrennte menschliche Prüfung an der Maßnahme.
- Videonachweise sind nicht verfügbar, bis der Betreiber einen geprüften Videodateityp freischaltet.
- Eine Position, die in einem späteren Import fehlt, wird nie gelöscht; der Verlauf bleibt erhalten.

## Wenn etwas schiefgeht
- **Die Anfrage wurde abgelehnt:** Beheben Sie die darunter aufgeführten Gründe.
- **Der Datensatz hat sich inzwischen geändert. Neu laden und erneut prüfen.** Drücken Sie **Neu laden**.
- **Das Ergebnis ist unbekannt (Verbindung verloren).** Verwenden Sie **Gleiche Anfrage erneut senden**.
- **Sie sind nicht berechtigt, Branchen-Assurance-Datensätze zu lesen.** oder **Sie dürfen das nicht tun.** Prüfen Sie Ihre Rolle und Ihren Schiffskontext; wenden Sie sich bei Bedarf an den Mandantenadministrator (13.03 §10).
- Kann ein Nachweis nicht verifiziert werden, prüfen Sie Datei-ID, Revision und SHA-256-Prüfsumme; wenden Sie sich bei Bedarf an den Verifizierer (13.03 §10).

## Verwandte Seiten
