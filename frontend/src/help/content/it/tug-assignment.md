---
key: tug-assignment
title: Assegnazione del rimorchiatore
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina mostra le **proposte di assegnazione dei rimorchiatori** per un lavoro di rimorchio inviato e i loro **conflitti** con altri lavori. Risponde alla domanda: "Quale rimorchiatore è stato proposto per questo lavoro, con quale ruolo e finestra temporale, è in conflitto con altre attività, e il Comandante del rimorchiatore ha accettato la proposta?" Terra propone, il Comandante del rimorchiatore riscontra; la pagina non prende alcuna decisione di invio o di rilascio. L'avviso in alto lo indica: "The system never issues GO."

## Chi la usa
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per fare una proposta di assegnazione dopo aver esaminato conflitti e prontezza sul tabellone missioni (13.03 §7).
- **Comandante del rimorchiatore:** per confermare nel contesto una proposta per il proprio rimorchiatore e accettarla o rifiutarla (13.03 §3).

## Prima di iniziare
- Aprite il rimorchiatore da **Operazioni rimorchiatore**; se non è selezionato alcun rimorchiatore, la pagina vi rimanda lì.
- Per proporre servono l'accesso in scrittura ai rimorchiatori e al piano; il lavoro deve essere **Inviato**.
- Per rispondere serve una **Designazione** registrata dal server (Comandante del rimorchiatore o Comandante) per quel rimorchiatore. La persona che ha fatto una proposta non può rispondervi.

## Passo dopo passo
1. Aprite il numero del lavoro da **Seleziona un lavoro di rimorchiatore inviato**. Anche il collegamento **Assegnazioni e prontezza** nel dettaglio del lavoro apre questa pagina.
2. Per le proposte esistenti, leggete **Stato**, **Riscontro del comandante del rimorchiatore**, **Ruolo**, **Inizio pianificato** e **Fine pianificata**.
3. In **Conflitti**, vedete i conflitti di tempo e di risorse con altri lavori, il relativo badge di risultato e il codice motivo.
4. Per una nuova proposta, scegliete **Rimorchiatore**, **Inizio pianificato**, **Fine pianificata** e **Ruolo** (Lead, Support, Escort) nel modulo **Proponi assegnazione** e premete **Proponi assegnazione**.
5. Come Comandante del rimorchiatore, scegliete la vostra **Designazione**, aggiungete un **Motivo** se necessario e premete **Accetta** o **Rifiuta**. Per rifiutare il motivo è obbligatorio.
6. Per rimuovere una proposta, inserite un motivo e premete **Ritira la proposta**.
7. Usate i collegamenti **Verifica di prontezza operativa** e **Equipaggio e competenza di rimorchio** sotto ogni proposta per passare alle schermate di prontezza.

## Cosa significano gli stati
- **Proposed / Withdrawn:** il ciclo di vita della proposta. Proposto non significa che il rimorchiatore sia stato inviato.
- **Pending / Accepted / Rejected:** la risposta del Comandante del rimorchiatore alla proposta. Accettato conferma l'ambito della proposta; non è una decisione GO.
- **Pass / Fail / Unknown:** il risultato di un controllo di conflitto. Unknown indica che i dati di origine non erano sufficienti per il controllo; non significa mai "nessun conflitto".
- **Nessun conflitto rilevato; i dati potrebbero essere incompleti.:** non è stato rilevato alcun conflitto, ma questo non è un risultato completo di idoneità.
- **La richiesta è cambiata dopo questa proposta; serve una nuova proposta.:** la richiesta del lavoro è cambiata dopo la revisione su cui si basava la proposta.

## Cosa non si può fare / attenzione
- Una proposta di assegnazione da terra non prevale mai sulla decisione di sicurezza del Comandante del rimorchiatore a bordo (13.03 §3, §7).
- Chi propone non può accettare la propria proposta; la risposta spetta solo alla persona designata per quel rimorchiatore.
- Non si può fare alcuna proposta per un lavoro in bozza o annullato.
- I comandi sono disattivati offline.

## Se qualcosa va storto
- **Messaggio "The outcome is uncertain":** **Riprova** invia lo stesso comando; non create una nuova proposta.
- **Messaggio "The record changed":** usate **Ricarica** per esaminare il record attuale, poi agite di nuovo.
- **"You are not authorized for this action." / "You do not have access to this record.":** controllate il vostro ruolo, il rimorchiatore e il contesto di designazione; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).
- **Conflitto inatteso:** annotate il codice motivo ed esaminatelo con il responsabile dell'altro lavoro.

## Pagine correlate
