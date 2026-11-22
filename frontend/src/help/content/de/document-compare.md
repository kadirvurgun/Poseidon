---
key: document-compare
title: Vergleich von Dokumentrevisionen
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Diese Seite zeigt für das ausgewählte Schiff **zwei Revisionen eines gelenkten Dokuments nebeneinander und schreibgeschützt**. Sie beantwortet: „Was unterscheidet diese beiden Revisionen auf Datei- und Metadatenebene, und wie sieht der Veröffentlichungs-, Verifizierungs- und Verwahrungsverlauf jeder Revision aus?“ Die Seite zeigt Unterschiede; sie genehmigt, veröffentlicht oder bewertet keine Änderungen. Sie setzt keine Revision in Kraft und lässt sie keine frühere ersetzen. Originale von Nachweisen werden nie stillschweigend geändert; ersetzt eine neue Revision eine frühere, wird der Veröffentlichungszustand der früheren Revision zu Ersetzt, und das Ereignis erscheint in ihrem Verwahrungsverlauf (13.03 §1).

## Wer sie verwendet
- **Kapitän / Leitender Ingenieur / Besatzung:** vergleichen die gültige Revision mit der vorherigen und erfassen eine Lesebestätigung (13.03 §2, §5, §6).
- **DPA / Regulatory- und Assurance-Nutzer:** prüfen den Revisionsverlauf und die Verifizierungsdatensätze (13.03 §4, §8).

## Bevor Sie beginnen
- Ist kein Schiff ausgewählt, zeigt die Seite **Wählen Sie ein Schiff in Ihrem Bereich.** und listet die Schiffe auf. Wählen Sie das richtige Schiff.
- Um die Seite zu sehen, benötigen Sie Lesezugriff auf Dateien. Beschränkte Inhalte und ISPS-beschränkte Dokumente erfordern zusätzlich eine eigene Berechtigung.
- Das Laden von Inhalten und das Erfassen einer Kenntnisnahme funktionieren nur online.

## Schritt für Schritt
1. Öffnen Sie ein Dokument aus der Liste **Gelenkte Dokumente**. Klassifizierung und die Kennzeichnung **ISPS-beschränkt** werden in der Zeile angezeigt.
2. Wählen Sie unter **Zwei Revisionen wählen** die Revisionen **Links (Basis)** und **Rechts (geprüft)**. Die Seite schlägt zunächst rechts die neueste Revision und links die davor vor.
3. Drücken Sie **Vergleichen**. Mit **Seiten tauschen** vertauschen Sie die Seiten.
4. Lesen Sie die Byte-Aussage und die Tabelle **Metadatenvergleich**: **Dateityp**, **Größe (Bytes)**, **Inhalts-SHA-256**, **Eingang**, **Veröffentlichung**, **Verifizierung** und die Zeitfelder.
5. Prüfen Sie auf jeder Seite den **Freigabe- und Verwahrungsverlauf**.
6. Um den Inhalt zu sehen, drücken Sie **Inhalt laden (Zugriff protokolliert)**. Der Inhalt wird mit einem Zuordnungswasserzeichen angezeigt.
7. Wenn Sie nach **Load content** die gültige Revision gelesen haben, kreuzen Sie **Ich habe diese Revision gelesen und konnte auf ihren Inhalt zugreifen.** an und drücken **Kenntnisnahme erfassen**.

## Was die Zustände bedeuten
- **Unpublished / Effective / Superseded:** der Veröffentlichungszustand der Revision. Eine nicht gültige Revision zeigt **Nicht in Kraft**.
- **Unverified / Verified valid / Verified invalid / Verification inconclusive:** der Verifizierungsdatensatz der Revision. Unverifiziert bedeutet nicht „gültig“.
- **Gleich / Changed / Not comparable:** das Ergebnis einer Metadatenzeile. Nicht vergleichbar bedeutet nicht „Gleich“.
- **Byte-identical files:** nur identische Bytes; dies ist keine inhaltliche Prüfung.
- **Files differ at byte level:** Welche Regel sich geändert hat und welche Bedeutung das hat, muss eine Person prüfen.
- **Inhalt für Ihre Rolle gesperrt; nur Metadaten.** Der Inhalt wird für diese Rolle nicht geöffnet.

## Was Sie nicht tun können / Vorsicht
- Auf dieser Seite können Sie kein Dokument veröffentlichen, genehmigen, in Kraft setzen oder ersetzen.
- Die Seite interpretiert inhaltliche Unterschiede nicht automatisch; eine berechtigte Person entscheidet über ihre Bedeutung.
- Eine Kenntnisnahme wird nur für die gültige Revision erfasst, einmal pro Person und Revision. Sie ist weder eine Genehmigung noch eine Verifizierung.
- Eine gedruckte Kopie ist nicht gelenkt. Inhalte werden nicht zwischengespeichert und nicht offline vorgehalten.
- Offline ist der Vergleich veraltet, und neue Inhalte können nicht geladen werden.

## Wenn etwas schiefgeht
- **Dieses Dokument ist für Sie nicht verfügbar:** Prüfen Sie Ihren Schiffskontext und Ihre Berechtigung; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Sie haben keine Berechtigung für diese Aktion:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext (13.03 §10).
- **Der Datensatz hat sich zwischenzeitlich geändert:** Verwenden Sie **Neu laden**, um die neueste Version zu öffnen.
- **Eine Revision kann nicht verifiziert werden:** Prüfen Sie den Nachweis und den Herkunftsverlauf; sprechen Sie mit einem berechtigten Verifizierer (13.03 §10).
- **Die Anfrage ist fehlgeschlagen:** Senden Sie Bildschirm, Uhrzeit und Schritte an den Support (13.03 §10).

## Verwandte Seiten
