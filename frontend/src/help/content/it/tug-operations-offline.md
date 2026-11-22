---
key: tug-operations-offline
title: Operazioni offline del rimorchiatore
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina conserva sul dispositivo uno **snapshot precaricato** di un lavoro di rimorchio e mette in una **coda durevole e cifrata** tre tipi di acquisizioni fatte senza connessione: conferme di briefing, osservazioni di esecuzione e voci del registro giornaliero. Risponde alla domanda: "Cosa posso acquisire in sicurezza senza connessione, cosa è in attesa nella coda e come l'ha accolto il server?" Il rilascio del Comandante, il GO di prontezza, l'avvio/le transizioni dell'esecuzione, la verifica e le revisioni restano registrazioni umane online.

## Chi la usa
- **Comandante del rimorchiatore:** per proseguire il lavoro critico dove la connessione è debole e seguire lo stato di sincronizzazione (13.03 §10).
- **Equipaggio:** per fare offline le conferme di briefing e le voci di guardia, e per risolvere i conflitti senza dare per scontato che il proprio record prevalga automaticamente (13.03 §6).

## Prima di iniziare
- Aprite la pagina da un lavoro di rimorchio; senza un lavoro la pagina mostra **Apri questa schermata da un lavoro del rimorchiatore per precaricarne lo snapshot.**
- La riga **Policy offline** deve mostrare una politica di archiviazione configurata (`CONFIGURED_UNVERIFIED`); senza una politica, il precaricamento dello snapshot e l'acquisizione offline non sono disponibili.
- Precaricate lo snapshot **mentre siete online**. Prima di acquisire, verificate che il lease dello snapshot non sia scaduto.

## Passo dopo passo
1. Mentre siete online, premete **Precarica snapshot del lavoro**. Compare il messaggio **Snapshot sigillato su questo dispositivo.**
2. In **Snapshot del lavoro**, controllate il numero del lavoro, il suo stato e **Lease fino a**.
3. Se siete partecipanti di un briefing emesso, scegliete **Conferma** o **Contesta** (con un motivo di almeno 10 caratteri) in **Conferma briefing** e premete **Sigilla e metti in coda**.
4. Per un'esecuzione in corso, scegliete l'esecuzione e il tipo di osservazione (Note, Towline connected, Towline released) in **Registra osservazione di esecuzione**, scrivete la nota e premete **Sigilla e metti in coda**.
5. Per un registro giornaliero in bozza o restituito, scegliete il registro in **Voce del registro giornaliero**, scrivete il **Riepilogo della giornata** e premete **Sigilla e metti in coda**.
6. Seguite lo stato di ogni acquisizione in **Acquisizioni in coda**. Le acquisizioni vengono inviate in ordine al ritorno della connessione; per inviarle manualmente usate **Invia acquisizioni in coda**.
7. Se un'acquisizione mostra **Conflitto sostanziale**, leggete **Modificato sul server**, scegliete esplicitamente **Mantieni il record del server** o **Applica il mio valore acquisito sul record corrente** e premete **Risolvi conflitto**.

## Cosa significano gli stati
- **In coda sul dispositivo (non inviato):** l'acquisizione è sigillata su questo dispositivo; in coda non significa inviato.
- **Accettato dal server:** il server ha ricevuto l'acquisizione. Non è un'approvazione né una verifica.
- **Conflitto sostanziale:** il record del server è cambiato dopo l'acquisizione; non viene mai unito automaticamente e attende la decisione di una persona.
- **Server value kept / Refused by server:** l'esito del server per l'acquisizione.
- **UNKNOWN (offline):** lo stato della politica non ha potuto essere letto offline; non si presume alcuna politica.

## Cosa non si può fare / attenzione
- Solo online: rilascio del Comandante, GO di prontezza, avvio e transizioni dell'esecuzione, emissione del briefing, verifica, invio e revisione del registro giornaliero.
- I conflitti sostanziali non vengono mai uniti automaticamente; la risoluzione è sempre una scelta umana esplicita (13.03 §1).
- Con un lease dello snapshot scaduto non si può fare alcuna nuova acquisizione; precaricate un nuovo snapshot.
- Nella riproduzione ogni acquisizione viene elaborata una sola volta con il suo id originale; non create di nuovo la stessa acquisizione.

## Se qualcosa va storto
- **Messaggio "The device key for this queue is unavailable.":** i record acquisiti vengono conservati ma non possono essere letti; non cancellateli e contattate il supporto.
- **Messaggio "Outcome unknown":** la stessa acquisizione verrà ritentata con il suo id originale; non createne una nuova.
- **Messaggio "The offline policy does not allow a snapshot.":** contattate l'amministratore del vostro tenant riguardo alla configurazione della politica (13.03 §10).
- **Conflitto sostanziale:** usate il modulo **Risolvi conflitto** in questa pagina; i conflitti sono elencati anche nel **Centro conflitti rilevanti** (13.03 §10).

## Pagine correlate
