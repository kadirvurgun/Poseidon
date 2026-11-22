---
key: offline-conflict-center
title: Centro conflitti rilevanti
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il centro conflitti rilevanti elenca, per la nave o il rimorchiatore selezionato, i **conflitti rilevanti tra le modifiche fatte offline e il record attuale del server** e permette a una persona di risolverli con una decisione motivata. Risponde alla domanda: "Quali delle mie modifiche offline sono entrate in conflitto con un record più recente del server, e come, da chi e con quale motivo verranno risolte?" Qui nulla viene sovrascritto o risolto automaticamente.

## Chi la usa
- **Equipaggio / Comandante del rimorchiatore / Comandante / Direttore di macchina:** per vedere e risolvere i propri record in conflitto dopo aver lavorato offline. Quando compare un conflitto, non date per scontato che il vostro record prevalga automaticamente (13.03 §6).
- Un conflitto rilevante richiede una risoluzione umana; questa pagina è la destinazione del flusso "Conflitto rilevante → Centro conflitti" descritto in 13.03 §10.

## Prima di iniziare
- La pagina elenca solo i conflitti che avete registrato voi e richiede l'accesso in lettura ad almeno una fonte leggibile (per esempio assegnazione dell'equipaggio, operazioni del rimorchiatore, registro giornaliero, evidenze di ordini di lavoro (con lettura di apparecchiature e manutenzione), bozza di rinnovo dei certificati). Basta una sola fonte leggibile per aprire la pagina; i permessi elencati sono alternativi.
- L'elenco e le decisioni richiedono una connessione attuale al server. Offline, le modifiche locali restano conservate sul dispositivo.
- Scegliete la nave o il rimorchiatore dall'elenco **Nave**.

## Passo dopo passo
1. Scegliete una nave da **Nave**. La tabella si apre con le colonne **Fonte**, **Registra**, **Versioni**, **Acquisito**, **Ricevuto dal server** e **Stato**.
2. Se è presente una nota "Non inclusi:", le fonti per cui non avete il permesso di lettura non sono nell'elenco.
3. Su una riga in **Aperto — decisione richiesta**, premete **Esamina**.
4. In **Esame del conflitto**, confrontate per ogni campo i valori **Prima dell’acquisizione**, **Acquisito** e **Server**. Nessuna scelta è preselezionata.
5. Per ogni valore, scegliete esplicitamente **Mantieni valore del server** oppure **Usa il valore acquisito**, compilate **Motivo della risoluzione (obbligatorio)** e premete **Invia risoluzione**.
6. Per rimuovere soltanto la copia locale su questo dispositivo, spuntate la conferma in **Scarta la mia modifica locale** e premete **Scarta modifica locale**.
7. Usate **Chiudi esame** per tornare all'elenco.

## Cosa significano gli stati
- **Aperto — decisione richiesta:** il conflitto attende una decisione umana.
- **Sostituito da un conflitto più recente:** esiste un conflitto più recente per lo stesso record; esaminate quello nuovo.
- **Risolto con decisione esplicita:** qualcuno lo ha risolto con una decisione motivata. Questa non è un'approvazione operativa.
- **Non divulgato / Non fornito / Sconosciuto:** il valore è nascosto per autorizzazione, non è stato registrato o non è stato possibile leggerlo. Un valore sconosciuto non viene mai considerato vuoto o "No".
- **Ricevuta del server registrata per la tua decisione esplicita:** il server ha ricevuto la vostra decisione. Questa ricevuta non è un'approvazione operativa.

## Cosa non si può fare / attenzione
- I conflitti non vengono mai uniti o risolti automaticamente; ogni valore richiede una scelta umana esplicita e un motivo (13.03 §1).
- Il centro risolve direttamente i conflitti di assegnazione dell'equipaggio. Per le altre fonti mostra "Resolve this conflict from the owning record's offline panel."; per esempio i record del rimorchiatore si risolvono in **Operazioni offline del rimorchiatore**.
- Scartare una modifica locale rimuove solo la copia sul dispositivo; il record del server resta invariato e il conflitto rimane registrato fino a una decisione motivata.
- Una modifica acquisita su un altro dispositivo deve essere risolta su quel dispositivo; nulla viene scartato.
- L'invio ricontrolla il vostro accesso e le versioni del record; non concede alcuna approvazione operativa.

## Se qualcosa va storto
- **Messaggio "The submission outcome is unknown":** prima di inviare di nuovo, usate **Controlla esito dell’invio**.
- **Messaggio "The server record changed after your review" / "The server record changed again":** la vostra decisione non è stata applicata; esaminate di nuovo il conflitto.
- **"Impossibile caricare i conflitti rilevanti. Nulla è stato risolto.":** premete **Riprova**.
- **"Non hai accesso al centro conflitti." / "L’accesso ai conflitti non è disponibile per questa nave.":** controllate il vostro ruolo e il contesto della nave; se necessario, contattate l'amministratore del tenant (13.03 §10).

## Pagine correlate
