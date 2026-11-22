---
key: tug-job-intake
title: Acquisizione missione
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
L'acquisizione missione struttura una nuova **richiesta di lavoro di rimorchio come bozza** nel contesto di rimorchiatore selezionato. Risponde alla domanda: "Per questo lavoro, quali sono la nave assistita, il tipo di missione, la finestra temporale, il luogo e le risorse richieste (numero di rimorchiatori, tiro a punto fisso)?" La richiesta salvata è una dichiarazione in bozza; non è un'assegnazione operativa, un'idoneità, un permesso né un'approvazione di sicurezza.

## Chi la usa
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per strutturare la richiesta e inserire nave assistita, tipo di missione, finestra temporale e capacità richiesta (13.03 §7).

## Prima di iniziare
- Nella barra di contesto in alto, selezionate la **società e il rimorchiatore corretti**.
- Serve l'accesso in scrittura ai rimorchiatori. Senza di esso il modulo non si apre e viene mostrato **L’accesso non è disponibile.**
- Le bozze incomplete possono essere salvate. I dati operativi obbligatori e la revisione autorizzata devono essere risolti prima dell'invio.

## Passo dopo passo
1. In **Richiesta e nave assistita**, compilate **Fonte della richiesta**, **Riferimento del cliente**, **Nave assistita**, **IMO della nave assistita**, **Tipo di missione**, **Priorità** e **Responsabile della richiesta**. Gli elenchi senza una scelta restano "Sconosciuto".
2. In **Ora e luogo richiesti**, inserite **Inizio richiesto (UTC)**, **Fine richiesta (UTC, condizionale)**, **Area operativa**, **Punto di prelievo** e **Punto di destinazione**. Gli orari sono in UTC.
3. In **Risorse necessarie e note**, inserite **BP richiesta (tonnes, decimale esatto)**, **Numero di rimorchiatori richiesto**, **Requisiti speciali** e **Note**.
4. Premete **Salva bozza**. In caso di successo si apre il dettaglio del lavoro con **Bozza salvata. L’approvazione operativa resta non disponibile.**
5. Per abbandonare le modifiche, usate **Scarta modifiche**.
6. Nel dettaglio del lavoro, usate **Invia richiesta** per registrare la richiesta per la pianificazione. Se mancano dati obbligatori, l'invio è bloccato e i campi mancanti vengono elencati.

## Cosa significano gli stati
- **Bozza:** la richiesta è salvata e modificabile; non è ancora inviata.
- **Inviato:** la richiesta è registrata per la pianificazione. Non è un'approvazione, un'assegnazione né una decisione di prontezza.
- **Annullato:** la richiesta è chiusa; non può essere riavviata.
- **Sconosciuto:** per il campo non è stato scelto alcun valore. Sconosciuto non è un valore predefinito e non sostituisce mai "No" o "Non applicabile".
- **Controlla questo valore.:** il server ha rifiutato questo campo; correggetelo e salvate di nuovo.

## Cosa non si può fare / attenzione
- Questa pagina non assegna alcun rimorchiatore e non dichiara mai pronto il lavoro. La proposta di assegnazione è un passo separato e non prevale mai sulla decisione di sicurezza del Comandante del rimorchiatore a bordo (13.03 §7).
- Una richiesta non può essere inviata finché mancano dati obbligatori; una bozza incompleta può comunque essere salvata.
- I campi di una richiesta inviata non possono essere modificati da questo modulo; una richiesta annullata non può essere riavviata, createne invece una nuova.
- Offline la bozza è conservata solo in questa pagina e non può essere salvata.

## Se qualcosa va storto
- **Messaggio "Save outcome uncertain":** il tentativo esatto viene conservato; dopo la riconnessione usate **Riprova** per inviare lo stesso comando. Scartare i dati locali non annulla un comando già inviato.
- **Messaggio "Conflict":** la vostra bozza viene conservata; scartatela e ricaricate prima di un nuovo tentativo.
- **Controlla campi:** correggete i campi contrassegnati.
- **L'accesso non è disponibile:** controllate il vostro ruolo e il contesto del rimorchiatore; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).

## Pagine correlate
