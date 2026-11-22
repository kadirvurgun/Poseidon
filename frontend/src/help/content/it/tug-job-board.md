---
key: tug-job-board
title: Tabellone missioni
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [canRead, canWrite]
related: [tug-command, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 7]
reviewed: false
---
## A cosa serve questa pagina
Il tabellone missioni elenca le **richieste di lavoro di rimorchio in bozza** del contesto di rimorchiatore selezionato, ai fini della pianificazione. Risponde alla domanda: "Quali richieste esistono, quale finestra temporale chiedono, quanti rimorchiatori e quale tiro a punto fisso (BP) richiedono, e cosa manca ancora per la pianificazione?" Il tabellone mostra solo dichiarazioni in bozza; non è un'assegnazione operativa, un'idoneità, un permesso né un'approvazione di sicurezza.

## Chi la usa
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per vedere le richieste, seguire le informazioni su conflitti e prontezza e preparare una proposta di assegnazione (13.03 §7).
- **Comandante del rimorchiatore:** per vedere in anticipo il lavoro in arrivo per il proprio rimorchiatore (13.03 §3).

## Prima di iniziare
- Nella barra di contesto in alto, selezionate la **società e il rimorchiatore corretti**. Il tabellone mostra solo le richieste di questo contesto.
- Per vedere il tabellone serve l'accesso in lettura. La creazione di una richiesta richiede l'accesso in scrittura; senza di esso il pulsante **Nuova richiesta** non viene mostrato.

## Passo dopo passo
1. Nel riepilogo in alto, vedete il numero di richieste in questa pagina, i lavori con un blocco registrato, quelli in attesa di revisione della prontezza e quelli con prontezza sconosciuta. **I conteggi riguardano solo questa pagina.**
2. Se necessario, digitate un numero di lavoro o il nome di una nave assistita in **Cerca richieste**, scegliete i filtri **Priorità**, **Stato** e **Finestra temporale** e premete **Applica filtri**.
3. Su ogni riga, leggete la finestra richiesta (UTC), le assegnazioni in bozza, il numero di rimorchiatori richiesto, la BP richiesta rispetto alla capacità di pianificazione, la prontezza e il conflitto operativo.
4. Aprite **Motivo / prossima azione** su una riga per vedere i riferimenti dei rimorchiatori assegnati, l'ambito del confronto e i motivi registrati.
5. Usate **Apri lavoro** per andare ai dettagli della richiesta.
6. Per una nuova richiesta, premete **Nuova richiesta** per aprire **Acquisizione missione** (richiede l'accesso in scrittura).
7. Usate **Aggiorna bacheca** per rileggere i dati e **Precedente** / **Successivo** per le altre pagine.

## Cosa significano gli stati
- **Draft / Submitted / Cancelled:** lo stato del ciclo di vita della richiesta. L'invio registra la richiesta per la pianificazione; non è un'approvazione, un'assegnazione né una decisione di prontezza.
- **Bloccato:** la richiesta ha un blocco registrato. Il motivo si trova sotto **Motivo / prossima azione**.
- **Revisione necessaria:** le informazioni di prontezza attendono la revisione di una persona autorizzata.
- **Sconosciuto:** non ci sono dati di origine sufficienti per prontezza o conflitto. Sconosciuto non significa "Pronto" né "nessun conflitto", e il tabellone non ne trae alcuna conclusione di prontezza.
- **Authorized human review required / Boundary touch review required:** due richieste possono sovrapporsi nel tempo o toccarsi alle estremità. Decide una persona.
- **BP del profilo (non verificata):** il tiro a punto fisso mostrato è un valore dichiarato, non misurato né verificato.

## Cosa non si può fare / attenzione
- Il tabellone non dichiara mai una richiesta "pronta" o "sicura". Una proposta di assegnazione da terra non toglie l'autorità di sicurezza a bordo del Comandante del rimorchiatore (13.03 §7).
- I campi di una richiesta inviata o annullata non si modificano dal tabellone; una richiesta annullata non può essere riavviata, createne invece una nuova.
- Il filtro della finestra temporale usa l'ora del server. Le richieste senza inizio restano nell'elenco come "Sconosciuto".
- I dati mostrati offline possono essere vecchi e il tabellone non può essere aggiornato.

## Se qualcosa va storto
- **Tabellone attuale non disponibile:** controllate la connessione e usate **Aggiorna bacheca**.
- **Nessuna richiesta in bozza:** se avete applicato dei filtri, provate una ricerca o una priorità diversa; le nuove richieste compaiono in questo contesto.
- **Accesso non disponibile:** controllate il vostro ruolo e il contesto del rimorchiatore; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).
- **Blocco o revisione inattesi:** aprite **Motivo / prossima azione** ed esaminate i motivi registrati con il responsabile della richiesta.

## Pagine correlate
