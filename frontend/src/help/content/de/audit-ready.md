---
key: audit-ready
title: Auditfähige Nachverfolgung
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Seite „Auditfähige Nachverfolgung“ zeigt die Ihnen für ein Schiff zugewiesenen Nachverfolgungsketten Stufe für Stufe: Anforderung → Kontrolle → Nachweis → Verifizierung → Entscheidung, mit Zuständen, Zeitstempeln und deklarierten Quellenfundstellen. Sie beantwortet: „Auf welcher Anforderung beruht dieser Nachweis, wer hat ihn verifiziert und welche Entscheidung wurde erfasst?“ Eine erfasste Kette ist eine menschliche Aufzeichnung, keine Freigabe und keine Bereitschaftserklärung (13.03 §4, §8).

## Wer sie verwendet
- **Regulatory-/Assurance-Nutzer:** prüft, dass in der Kette Anforderung→Kontrolle→Nachweis→Verifizierung kein verwaister Datensatz zurückbleibt (13.03 §8).
- **DPA / HSEQ:** zeigt bei der Vorbereitung auf Audit, PSC oder Inspektion die Spur Feststellung→Maßnahme→Verifizierung mit ihren Nachweisen (13.03 §4).

## Bevor Sie beginnen
- Die Seite benötigt Audit-Lesezugriff und Lesezugriff auf die menschliche Nachverfolgung sowie eine aktuelle Bestellung als Nachverfolgungsleser für dieses Schiff. Die Berechtigung allein reicht nicht aus; ohne Bestellung wird keine Kette aufgeführt.
- Wählen Sie das richtige Schiff. Die Liste zeigt nur die Ihnen zugewiesenen Ketten.
- Die Seite lädt über eine Live-Verbindung. Offline können die angezeigten Daten veraltet sein.

## Schritt für Schritt
1. Wählen Sie unter **Schiff** ein Schiff (**Schiff auswählen**).
2. Öffnen Sie eine Kette unter **Zugewiesene Ketten**.
3. Klappen Sie unter **Nachverfolgungsstufen** jede Stufe auf (Anforderung, Kontrolle, Nachweis, Verifizierung, Entscheidung) und prüfen Sie **Status**, **Erfasst am**, **Revision**, **Inhalts-Hash** und **Quellenfundstelle (deklariert, nicht verifiziert)**.
4. Sind **Offene Punkte** aufgeführt, geben Sie die Codes an den Verantwortlichen des betreffenden Datensatzes weiter.
5. Wenn lesbare zugewiesene Ketten vorhanden sind, erstellen Sie unter **Auditpaket** mit **Neues Paket erstellen** ein Paket, öffnen es unter **Erstellte Pakete** und verwenden bei Bedarf **JSON exportieren**.
6. Drücken Sie in der Kopfzeile **Erneut versuchen**, um die Daten zu aktualisieren.

## Was die Zustände bedeuten
- **Erfasste Kette — nur menschliche Aufzeichnung, keine Freigabe:** Jede Stufe ist erfasst. Damit wird das Schiff oder der Betrieb nicht als bereit oder konform erklärt.
- **Prüfung erforderlich:** Mindestens eine Stufe fehlt, ist unbekannt oder nicht verfügbar. Eine unbekannte oder nicht lesbare Eingabe wird nie als vollständig angezeigt.
- **Nicht erfasst:** Für diese Stufe existiert kein Datensatz; zum Beispiel wurde noch keine Verifizierung oder Entscheidung erfasst.
- **Nicht verfügbar / UNKNOWN:** Der Datensatz kann derzeit nicht gelesen werden; sein Inhalt wird nicht angenommen.
- **Veraltet: Eine Eingabe wurde ersetzt oder ist abgelaufen; Prüfung erforderlich.** Ein Nachweis oder eine Quelle hat sich später geändert; prüfen Sie die Kette erneut.
- **Fundstelle nicht angegeben:** Es wurde keine Quellenfundstelle eingegeben; eine Fundstelle ist eine Deklaration, keine verifizierte Tatsache.
- Ein verifizierter Nachweis bedeutet nicht, dass die Maßnahme wirksam war: Verifiziert ≠ Wirksam (13.03 §4).
- **Auditpaket:** „Systemzusammenstellung; kein amtlicher Nachweis“. Einträge außerhalb Ihrer Bestellungen werden nur gezählt, nicht angezeigt; Einträge mit **Ersetzt (für die Rückverfolgung erhalten)** werden nie als aktuell behandelt.

## Was Sie nicht tun können / Vorsicht
- Auf dieser Seite können Sie keine Datensätze bearbeiten, nichts verifizieren und nichts entscheiden; sie ist schreibgeschützt. Verifizierungen und Entscheidungen erfasst die bestellte Person auf deren eigenen Seiten.
- Ketten, die Ihnen nicht zugewiesen sind, können Sie nicht sehen; verborgene Ketten werden nie benannt, und nicht verfügbare zugewiesene Ketten werden nur als Anzahl angezeigt.
- Integritäts- und Kontext-Hashes sind weder Befugnis noch Freigabe.

## Wenn etwas schiefgeht
- **Sie sind für diese Ansicht nicht berechtigt.** Prüfen Sie Ihre Rolle, Ihr Schiff und Ihren Bestellungskontext; wenden Sie sich an den Mandantenadministrator (13.03 §10).
- **Für dieses Schiff sind Ihnen keine Ketten zugewiesen.** Prüfen Sie, ob Sie das richtige Schiff ausgewählt haben; fragen Sie die verantwortliche Person, falls eine Bestellung nötig ist.
- **Die Nachverfolgung konnte nicht geladen werden.** Drücken Sie **Erneut versuchen**.
- **Kette nicht gefunden oder für Sie nicht mehr sichtbar.** Ihre Bestellung ist möglicherweise beendet, oder der Datensatz hat Ihren Zugriffsbereich verlassen.
- Kann ein Datensatz nicht verifiziert werden, prüfen Sie seinen Nachweis und seine Herkunft und wenden Sie sich bei Bedarf an den Verifizierer (13.03 §10).

## Verwandte Seiten
