---
key: tug-daily-log
title: Giornale del rimorchiatore
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## A cosa serve questa pagina
Il giornale del rimorchiatore registra, per una data e una guardia, **lavori, contaore dei motori, carburante e difetti**, e li confronta con i rimorchi eseguiti nella giornata per la **riconciliazione**. Risponde alla domanda: "Quanti lavori sono stati completati in questa giornata/guardia, le ore dei contaore corrispondono ai tempi di esecuzione, quanto carburante è stato consumato e quali difetti sono stati segnalati?" La riconciliazione è consultiva; la revisione del Comandante è una registrazione umana separata.

## Chi la usa
- **Equipaggio / ufficiale di guardia:** per tenere la registrazione della guardia in tempo reale e in ordine (13.03 §6).
- **Comandante del rimorchiatore / Comandante:** per esaminare un giornale inviato e verificarlo o restituirlo (13.03 §3).

## Prima di iniziare
- Aprite il rimorchiatore da **Operazioni rimorchiatore**.
- La creazione e la modifica di un giornale richiedono l'accesso in scrittura ai rimorchiatori.
- La revisione richiede una **Designazione** registrata dal server (Comandante del rimorchiatore o Comandante) per quel rimorchiatore. Chi ha scritto o inviato il giornale non può esaminarlo.

## Passo dopo passo
1. Aprite un giornale esistente dall'elenco, oppure scegliete una **Data del giornale** e una **Guardia** e premete **Crea giornale**.
2. Inserite il **Fuso orario (IANA)**.
3. In **Contaore dei motori**, inserite per ogni **Contaore** la **Lettura iniziale**, la **Lettura finale** e, se necessario, la **Prova di azzeramento o di superamento del fondo scala**.
4. In **Carburante consumato**, inserite il valore, l'unità e il **Riferimento della fonte del carburante**.
5. Spuntate le esecuzioni che appartengono a questo giornale in **Rimorchi eseguiti nella giornata**; compilate **Linked defect identifiers** e **Riepilogo giornaliero** (riepilogo di almeno 20 caratteri).
6. Premete **Salva**. Sulla riga **Riconciliazione**, controllate lavori completati, ore di esecuzione, ore dei contaore e difetti segnalati.
7. Quando l'elenco **Blocchi** è vuoto, premete **Invia alla revisione del comandante**.
8. Come Comandante, scegliete la vostra **Designazione**, scrivete un motivo e premete **Comandante: verifica giornale** o **Comandante: restituisci giornale**.

## Cosa significano gli stati
- **Draft / Submitted / Returned / Verified:** lo stato del giornale.
- **Master review — Pending / Attention / Reviewed:** Pending = in attesa di revisione; Attention = restituito, oppure inviato mentre la riconciliazione non è Reconciled; Reviewed = verificato dal Comandante. Un giornale inviato non è un giornale verificato.
- **Reconciled / Review required / Incomplete:** il risultato consultivo del confronto. I codici motivo sono elencati sotto.
- **intervallo aperto:** un'esecuzione è ancora in corso; le ore di esecuzione non sono ancora definitive.
- **Safety events — Unknown:** questa pagina non conta gli eventi di sicurezza; non si trae alcuna conclusione di "nessun evento".
- **Synced / Waiting to sync / Conflict / Failed:** lo stato di sincronizzazione. Waiting to sync indica che la modifica non ha ancora raggiunto il server.

## Cosa non si può fare / attenzione
- Chi ha scritto il giornale o lo ha inviato alla revisione del Comandante non può verificarlo.
- Il risultato della riconciliazione non è un'approvazione; la revisione la fa il Comandante.
- Completare un record e verificarlo non sono la stessa azione (13.03 §1).
- Offline, una modifica non salvata viene conservata nella memoria di questa pagina e inviata con la sua chiave originale al ritorno della connessione; tenete la pagina aperta. L'invio e la revisione avvengono online.
- Se compare un conflitto, non date per scontato che il vostro record prevalga automaticamente (13.03 §6).

## Se qualcosa va storto
- **Messaggio "The server listed missing or blocking items.":** risolvete i codici in **Blocchi**.
- **Messaggio "The record changed or the command conflicts.":** usate **Ricarica la versione più recente** per esaminare il giornale attuale; la modifica locale conservata viene scartata.
- **Messaggio "The outcome is unknown.":** **Riprova** invia di nuovo lo stesso identico comando.
- **Conflitto:** una modifica conservata in questa pagina non viene mai unita; usate **Ricarica la versione più recente**. Le voci giornaliere acquisite in **Operazioni offline del rimorchiatore** che sono in conflitto si risolvono lì; sono elencate anche nel **Centro conflitti rilevanti** (13.03 §10).

## Pagine correlate
