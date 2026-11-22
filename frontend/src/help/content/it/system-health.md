---
key: system-health
title: Stato del sistema
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
"Stato del sistema" mostra agli amministratori le **osservazioni dei servizi** in una sola schermata: API, scrittura dell'audit, sincronizzazione, processi di notifica, servizio file e attualità dei dati di ingresso. Risponde alla domanda: "Quali segnali di servizio sono stati osservati per la nave selezionata e per questo tenant, e quali no?" La configurazione e la telemetria mancante non stabiliscono che il servizio sia in buono stato né che la nave sia pronta.

## Chi la usa
- **Amministratore di sistema / del tenant:** per accorgersi dei problemi di servizio e di sincronizzazione e gestirli in modo controllato (13.03 §9).
- **Supporto / Engineering:** per esaminare le osservazioni quando viene segnalato un errore dell'applicazione (13.03 §10).

## Prima di iniziare
- La pagina è visibile solo agli utenti con lettura a livello di tenant, lettura della cronologia delle azioni e lettura dello stato del sistema.
- Nella barra di contesto in alto, controllate che sia selezionata la **società corretta**.
- Le osservazioni vengono richieste per nave; dovete prima selezionare una nave.

## Passo dopo passo
1. Scegliete la nave in **Ambito di osservazione della nave**. Per un elenco lungo, usate **Navi precedenti** / **Navi successive**.
2. Controllate l'ora "Letto il".
3. In **Disponibilità del servizio**, leggete l'ora di avvio del processo e il tempo di attività.
4. Nelle schede **API** e **Scrittura dell'audit**, leggete i valori di richieste, errori, record persistiti e latenza. Si riferiscono a questo tenant dall'avvio del processo.
5. In **Sincronizzazione**, vedete gli esiti confermati, i conflitti non risolti e l'ultima sincronizzazione confermata.
6. In **Processi di notifica**, vedete le notifiche aperte, le escalation scadute e lo stato del processo di valutazione.
7. Leggete le schede **Servizio file** e **Attualità dei dati di ingresso**.
8. In **Metriche del pilota**, esaminate le osservazioni dei client pilota registrati.
9. Premete **Aggiorna le osservazioni** per avere dati attuali.

## Cosa significano gli stati
- **Non osservato / Non disponibile:** non esiste alcuna osservazione per questo segnale. Questo non significa "in buono stato" né "nessun problema".
- **Configurato · non verificato attivamente:** il servizio file sembra configurato, ma non è stata eseguita alcuna verifica attiva dello storage o dello scanner.
- **Nei tempi previsti / In esecuzione / Scaduto / Pianificatore non abilitato / Esecuzione abbandonata (lease scaduto):** lo stato del processo di valutazione delle notifiche.
- **Conflitti non risolti:** conflitti offline in attesa di una risoluzione umana. Non vengono mai uniti automaticamente.
- **Attuale / Obsoleto / Sconosciuto / Non disponibile:** i conteggi delle valutazioni di attualità dei dati di ingresso. Sconosciuto non significa "Attuale".
- **This snapshot is older than two minutes:** aggiornate prima di farvi affidamento.

## Cosa non si può fare / attenzione
- Questa pagina è in sola lettura; non viene eseguita alcuna azione su servizi, navi o record.
- I record di notifica sono solo interni all'app; non sono una prova di consegna né di alcuna decisione di sicurezza.
- La disponibilità del servizio mostra solo che questo processo API ha risposto; non prova che altri servizi siano in esecuzione.
- Le metriche del pilota sono dichiarate e non verificate; non costituiscono una dichiarazione di accettazione.
- Non correggete i dati direttamente in produzione; pubblicate le modifiche di configurazione in modo controllato (13.03 §9).

## Se qualcosa va storto
- **L'accesso allo stato non è disponibile per questo contesto:** controllate il vostro ruolo e l'ambito del tenant.
- **Osservazioni dei servizi non disponibili:** non viene dedotto alcuno stato di buon funzionamento; dopo un po' usate "Aggiorna le osservazioni".
- **C'è un conflitto non risolto:** indirizzate l'utente interessato al Centro conflitti (13.03 §10).
- **Errore dell'applicazione:** segnalatelo al supporto/engineering con il Correlation ID, la schermata, l'ora e i passaggi eseguiti.

## Pagine correlate
