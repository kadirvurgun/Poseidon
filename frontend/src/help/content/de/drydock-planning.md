---
key: drydock-planning
title: Trockendockplanung
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## Wofür diese Seite dient
Die Trockendockplanung enthält die **Trockendockpakete** des ausgewählten Schiffs (Projekt, Werft, geplante Termine und Umfangspositionen). Sie beantwortet: „Mit welchem Arbeitsauftrag, Mangel, Zeugnis oder welcher Besichtigungscheckliste ist jede Umfangsposition des Trockendocks verknüpft, und sind diese Verknüpfungen aktuell?“ Der Umfang ist mit diesen Datensätzen verknüpft (13.03 §5). **Verknüpfungsbereitschaft** bezeichnet nur die Vollständigkeit der Verknüpfungen; sie ist kein Ergebnis einer Klassen- oder Flaggenbesichtigung und keine Aussage zur Seetüchtigkeit.

## Wer sie verwendet
- **Technischer Inspektor:** bereitet ein Trockendockpaket vor, verknüpft den Umfang mit Datensätzen, plant es und schließt es ab (13.03 §5).
- **Leitender Ingenieur:** verfolgt, wie der Umfang mit Mängel- und Arbeitsauftragsdatensätzen verknüpft ist, und verifiziert mit Berechtigung ein Paket, das jemand anderes abgeschlossen hat (13.03 §5).

## Bevor Sie beginnen
- Wählen Sie das richtige Schiff. Ist kein Schiff ausgewählt, zeigt die Seite **Wählen Sie im Arbeitsbereich ein Schiff aus, um fortzufahren.**
- Um Pakete zu sehen, benötigen Sie Lesezugriff auf Trockendockdaten. Erstellen, Bearbeiten und Statusänderungen erfordern Schreibzugriff; das Verifizieren erfordert Verifizierungszugriff.
- Halten Sie die Kennungen und Revisionen der Arbeitsauftrags-, Mängel-, Zeugnis- und Besichtigungschecklisten-Datensätze bereit, die Sie verknüpfen werden.

## Schritt für Schritt
1. Öffnen Sie ein Paket aus der Liste, oder legen Sie mit **Trockendockpaket anlegen** einen leeren Entwurf an.
2. Füllen Sie **Trockendockprojekt**, **Werft**, **Geplanter Beginn** und **Geplantes Ende** aus.
3. Fügen Sie mit **Umfangsposition hinzufügen** Positionen hinzu; geben Sie Typ der **Umfangsposition**, **Titel**, **Arbeitspaket**, **Klassenanforderung**, **Gesetzliche Anforderung** und **Priorität** ein.
4. Verwenden Sie bei jeder Position **Verknüpfung hinzufügen** und geben Sie **Datensatztyp**, **Datensatzkennung** und **Fixierte Revision** ein.
5. Für den Abschluss fügen Sie unter **Abschlussnachweis** mit **Hinzufügen** eine Nachweisreferenz hinzu. Drücken Sie dann **Speichern**.
6. Prüfen Sie die Liste **Planungshindernisse** oder **Abschlusshindernisse**. Wählen Sie unter **Status ändern** eine **Aktion** (**Planen**, **Zurück zum Entwurf**, **Dockung beginnen**, **Dockung abschließen**, **Paket stornieren**) und bestätigen Sie sie. Zum Stornieren ist eine Notiz erforderlich. Es werden nur die Aktionen angeboten, die für den aktuellen Zustand gültig sind; ein Paket kann nur im Zustand Entwurf oder Geplant storniert werden.
7. Schreiben Sie bei einem abgeschlossenen Paket, das Sie nicht abgeschlossen haben, eine **Begründung** von mindestens 10 Zeichen und verwenden Sie **Datensatz verifizieren** oder **Verifizierung ablehnen**.
8. Verfolgen Sie im **Zeitverlauf** die geplanten und tatsächlichen Termine und jede Revision.

## Was die Zustände bedeuten
- **Draft / Planned / In Bearbeitung / Completed / Verified / Cancelled:** der Lebenszyklus des Pakets. Abgeschlossen und Verifiziert sind nicht dasselbe.
- **Links current / Attention / Review required / Incomplete:** die Verknüpfungsbereitschaft. Auch bei aktuellen Verknüpfungen ist dies kein Besichtigungsergebnis.
- **Linked / Unlinked / Overdue / Review required:** der Verknüpfungszustand einer Umfangsposition (Prüfung erforderlich, wenn eine Verknüpfung veraltet oder nicht verfügbar ist).
- **Current / Stale / Unavailable:** ein verknüpfter Datensatz im Vergleich zu seiner fixierten Revision. Prüfen Sie eine veraltete Verknüpfung und fixieren Sie sie mit **Erneut fixieren auf** auf die neue Revision.
- **Unbekannt / nicht gesetzt:** Es wurde keine Priorität eingegeben. Unbekannt bedeutet weder „Normal“ noch „Niedrig“.

## Was Sie nicht tun können / Vorsicht
- Die Seite entscheidet nicht, dass ein Trockendock sicher oder das Schiff seetüchtig ist.
- Ein Paket, das Sie abgeschlossen haben, können Sie nicht verifizieren. Das Ablehnen der Verifizierung setzt das Paket zurück auf **In Bearbeitung**.
- Kosten, Währung, Lieferant und Budget sind nur externe Referenzen; hier wird nichts summiert, genehmigt oder bestellt.
- Ein abgeschlossenes, verifiziertes oder storniertes Paket kann nicht bearbeitet werden.
- Offline werden keine Befehle gesendet.

## Wenn etwas schiefgeht
- **Der Server hat fehlende oder blockierende Punkte aufgeführt:** Lösen Sie die Hindernisliste auf und versuchen Sie es erneut.
- **Der Datensatz hat sich geändert oder der Befehl steht im Konflikt:** Verwenden Sie **Neuesten Stand laden** und prüfen Sie.
- **Das Ergebnis ist unbekannt:** **Erneut versuchen** sendet den identischen Befehl erneut.
- **Sie haben keinen Zugriff auf diesen Datensatz:** Prüfen Sie Ihre Rolle und Ihren Schiffskontext; wenden Sie sich bei Bedarf an Ihren Mandantenadministrator (13.03 §10).
- **Sie können diese verknüpften Datensatztypen nicht lesen:** Klären Sie die Berechtigung mit dem Verantwortlichen des Datensatzes oder Ihrem Mandantenadministrator.

## Verwandte Seiten
