---
key: tug-operation-history
title: Storico del rimorchiatore
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Lo storico delle operazioni mostra lo **storico in sola lettura** di lavori di rimorchio, esecuzioni, registri giornalieri ed evidenze. Risponde alla domanda: "Quali lavori si sono conclusi con quale esito, le esecuzioni sono state verificate, esiste un record di evidenza e quali registri giornalieri appartengono a questi lavori?" I record annullati e interrotti restano visibili; nulla viene cancellato o rietichettato.

## Chi la usa
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per seguire la completezza delle evidenze, gli eventi e i record storici dei lavori completati (13.03 §7).
- **Comandante del rimorchiatore:** per vedere i lavori passati e le cronologie di esecuzione del proprio rimorchiatore (13.03 §3).

## Prima di iniziare
- Aprite il rimorchiatore da **Operazioni rimorchiatore**; senza un rimorchiatore selezionato la pagina mostra **Seleziona un rimorchiatore per continuare.**
- È sufficiente l'accesso in lettura ai rimorchiatori; la pagina non modifica alcun dato.
- Lo storico viene letto dal server; non può essere aggiornato offline.

## Passo dopo passo
1. In **Filtri**, impostate facoltativamente **ID rimorchiatore**, **Da (data UTC)**, **A (data UTC)** ed **Esito**, poi premete **Applica filtri**. L'intervallo di date è al massimo di 400 giorni.
2. Su ogni riga della tabella, leggete **Lavoro** (numero del lavoro, nave assistita, tipo di lavoro, stato del piano e del briefing), **Ora attività**, **Esito** ed **Evidenza**.
3. In **Esecuzioni**, vedete per ogni esecuzione lo stato, lo stato di verifica, la durata, la registrazione del Comandante e il numero di voci di evidenza; usate **Apri cronologia di esecuzione** per passare alla console di esecuzione.
4. Usate i collegamenti in **Registri giornalieri** per aprire il registro relativo. La nota **non collegato dal registro** indica che il registro non collega questa esecuzione.
5. Usate **Carica altro** per i record più vecchi. **Letto il** mostra quando i dati sono stati letti.

## Cosa significano gli stati
- **Not started / In progress / Completed (recorded) / Aborted / Cancelled / Mixed:** l'esito complessivo del lavoro. Completed (recorded) non significa che il lavoro sia stato verificato.
- **Verified, evidence recorded / Verified, no evidence entry / Verification rejected / Evidence unverified / Unknown:** lo stato delle evidenze e della verifica.
- **verification pending / verified / verification rejected:** lo stato di verifica di una singola esecuzione.
- **Nessuno:** per questo lavoro non esiste alcun record collegato. Ciò non significa che non servisse alcun record.

## Cosa non si può fare / attenzione
- In questa pagina nessun record viene modificato, cancellato o verificato.
- Nessuna cancellazione silenziosa: i lavori annullati e le esecuzioni interrotte o rifiutate restano nello storico.
- Completato ≠ Verificato: la verifica è una registrazione di un'altra persona.
- I dati mostrati sono lo stato al momento di **Letto il**; non vengono aggiornati offline.

## Se qualcosa va storto
- **Messaggio "The filter is not valid":** verificate che l'ID del rimorchiatore sia un UUID e che l'intervallo di date non superi 400 giorni.
- **Messaggio "The history could not be loaded.":** controllate la connessione e premete **Riprova**.
- **"You do not have access to this tug scope.":** controllate il vostro ruolo e il contesto del rimorchiatore; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).
- **Il record non può essere verificato:** controllate il riferimento dell'evidenza e la sua fonte nella cronologia di esecuzione; contattate il verificatore (13.03 §10).

## Pagine correlate
