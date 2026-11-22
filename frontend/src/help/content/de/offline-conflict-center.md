---
key: offline-conflict-center
title: Zentrale für wesentliche Konflikte
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Zentrale für wesentliche Konflikte listet für das ausgewählte Schiff oder den ausgewählten Schlepper die **wesentlichen Konflikte zwischen Ihren Offline-Änderungen und dem aktuellen Serverdatensatz** auf und ermöglicht es einer Person, sie mit einer begründeten Entscheidung zu lösen. Die Seite beantwortet: „Welche meiner Offline-Änderungen kollidierte mit einem neueren Serverdatensatz, und wie, von wem und mit welcher Begründung wird sie gelöst?“ Hier wird nichts automatisch überschrieben oder gelöst.

## Wer sie verwendet
- **Crew / Schlepperkapitän / Kapitän / Leitender Ingenieur:** um nach der Offline-Arbeit die eigenen kollidierenden Datensätze zu sehen und zu lösen. Wenn ein Konflikt erscheint, gehen Sie nicht davon aus, dass Ihr eigener Datensatz automatisch gewinnt (13.03 §6).
- Ein wesentlicher Konflikt braucht eine menschliche Lösung; diese Seite ist das Ziel des Ablaufs „Wesentlicher Konflikt → Konfliktzentrale“ in 13.03 §10.

## Bevor Sie beginnen
- Die Seite listet nur Konflikte, die Sie erfasst haben, und benötigt Lesezugriff auf mindestens eine lesbare Quelle (zum Beispiel Crew-Zuweisung, Schlepperbetrieb, Tagesbericht, Arbeitsauftragsnachweis (mit Lesezugriff auf Ausrüstung und Instandhaltung), Entwurf einer Zeugniserneuerung). Eine einzige lesbare Quelle genügt, um die Seite zu öffnen; die aufgeführten Berechtigungen sind Alternativen.
- Liste und Entscheidungen benötigen eine aktuelle Serververbindung. Offline bleiben Ihre lokalen Änderungen auf dem Gerät erhalten.
- Wählen Sie das Schiff oder den Schlepper aus der Liste **Schiff**.

## Schritt für Schritt
1. Wählen Sie unter **Schiff** ein Fahrzeug. Die Tabelle öffnet sich mit den Spalten **Quelle**, **Datensatz**, **Versionen**, **Erfasst**, **Vom Server empfangen** und **Status**.
2. Erscheint ein Hinweis **Nicht einbezogen:**, sind Quellen, für die Sie keine Leseberechtigung haben, nicht in der Liste.
3. Drücken Sie in einer Zeile mit **Offen — Entscheidung erforderlich** auf **Prüfen**.
4. Vergleichen Sie in **Konfliktprüfung** für jedes Feld die Werte **Vor der Erfassung**, **Erfasst** und **Server**. Es ist keine Auswahl vorbelegt.
5. Wählen Sie für jeden Wert ausdrücklich **Serverwert behalten** oder **Erfassten Wert verwenden**, füllen Sie **Lösungsgrund (erforderlich)** aus und drücken Sie **Lösung einreichen**.
6. Um nur die lokale Kopie auf diesem Gerät zu entfernen, setzen Sie die Bestätigung in **Meine lokale Änderung verwerfen** und drücken Sie **Lokale Änderung verwerfen**.
7. Mit **Prüfung schließen** kehren Sie zur Liste zurück.

## Was die Zustände bedeuten
- **Offen — Entscheidung erforderlich:** Der Konflikt wartet auf eine menschliche Entscheidung.
- **Durch neueren Konflikt ersetzt:** Für denselben Datensatz gibt es einen neueren Konflikt; prüfen Sie den neuen.
- **Durch ausdrückliche Entscheidung gelöst:** Jemand hat ihn mit einer begründeten Entscheidung gelöst. Dies ist keine betriebliche Freigabe.
- **Zurückgehalten / Nicht angegeben / Unbekannt:** Der Wert ist aufgrund der Berechtigung verborgen, wurde nicht erfasst oder konnte nicht gelesen werden. Ein unbekannter Wert wird nie als leer oder „Nein“ gewertet.
- **Serverbeleg für Ihre ausdrückliche Entscheidung erfasst:** Der Server hat Ihre Entscheidung empfangen. Dieser Beleg ist keine betriebliche Freigabe.

## Was Sie nicht tun können / Vorsicht
- Konflikte werden nie automatisch zusammengeführt oder gelöst; jeder Wert braucht eine ausdrückliche menschliche Auswahl und Begründung (13.03 §1).
- Die Zentrale löst Konflikte bei Crew-Zuweisungen direkt. Für andere Quellen zeigt sie „Resolve this conflict from the owning record's offline panel.“; Schlepperdatensätze werden zum Beispiel unter **Schlepper-Offlinebetrieb** gelöst.
- Das Verwerfen einer lokalen Änderung entfernt nur die Kopie auf dem Gerät; der Serverdatensatz bleibt unverändert, und der Konflikt bleibt erfasst, bis eine begründete Entscheidung vorliegt.
- Eine auf einem anderen Gerät erfasste Änderung muss auf diesem Gerät gelöst werden; es wird nichts verworfen.
- Beim Einreichen werden Ihr Zugriff und die Datensatzversionen erneut geprüft; das Einreichen erteilt keine betriebliche Freigabe.

## Wenn etwas schiefgeht
- **Meldung „The submission outcome is unknown“:** Verwenden Sie vor einem erneuten Einreichen **Einreichungsergebnis prüfen**.
- **Meldung „The server record changed after your review“ / „The server record changed again“:** Ihre Entscheidung wurde nicht angewendet; prüfen Sie den Konflikt erneut.
- **„Wesentliche Konflikte konnten nicht geladen werden. Nichts wurde gelöst.“:** Drücken Sie **Erneut versuchen**.
- **„Sie haben keinen Zugriff auf die Konfliktzentrale.“ / „Konfliktzugriff ist für dieses Schiff nicht verfügbar.“:** Prüfen Sie Ihre Rolle und Ihren Fahrzeugkontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).

## Verwandte Seiten
