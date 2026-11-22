---
key: towage-plan
title: Piano di rimorchio
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il piano di rimorchio registra, per un lavoro di rimorchio inviato, **ruoli e configurazione dei rimorchiatori, scambio di informazioni, limiti operativi, comunicazione, sgancio di emergenza, contingenza e controlli della zona di snap-back**. Risponde alla domanda: "Come sarà eseguito questo lavoro, entro quali limiti e con quali disposizioni di emergenza?" L'approvazione del piano è una registrazione umana del Comandante o del Comandante del rimorchiatore designato; non è una decisione GO né un permesso. I limiti sono dati di ingresso del piano indicati con la loro fonte; il sistema non fornisce valori predefiniti.

## Chi la usa
- **Coordinatore / pianificatore delle operazioni dei rimorchiatori:** per preparare il piano e inviarlo per approvazione (13.03 §7).
- **Comandante del rimorchiatore / Comandante:** per confermare configurazione, cavo, BP/capacità, comunicazione, limiti, contingenza e condizioni di sospensione del lavoro, e approvare o restituire il piano (13.03 §3).

## Prima di iniziare
- Aprite il rimorchiatore da **Operazioni rimorchiatore** e scegliete il lavoro da **Seleziona un lavoro di rimorchiatore inviato**.
- La modifica del piano richiede l'accesso in scrittura ai rimorchiatori e al piano. I rimorchiatori del piano si scelgono tra quelli con una proposta di assegnazione attuale per questo lavoro.
- L'approvazione o la restituzione richiedono una **Designazione** registrata dal server (Comandante del rimorchiatore o Comandante) per il rimorchiatore coordinatore.

## Passo dopo passo
1. Leggete **Stato** e **Revisione**, poi l'elenco **Mancanti prima dell'invio**.
2. In **Ruoli e configurazione dei rimorchiatori**, usate **Aggiungi** per ogni rimorchiatore; inserite **Posizione**, **Metodo di rimorchio**, **Rimorchiatore coordinatore (principale)**, **Origine del cavo di rimorchio**, **Punto di collegamento**, **ID attrezzatura del cavo di rimorchio** e **ID attrezzatura di sgancio di emergenza**.
3. Scrivete il testo **Scambio di informazioni Comandante / pilota / Comandante del rimorchiatore**.
4. In **Limiti operativi (dati di ingresso del piano)**, inserite un **Valore** e la **Fonte del limite** per ogni **Parametro**. Un nuovo limite parte vuoto.
5. Compilate **Piano di comunicazione** (canale principale e di riserva, lingua di lavoro, procedura in caso di perdita di comunicazione), **Sgancio di emergenza**, **Contingenza** e **Controlli della zona di snap-back**.
6. Salvate con **Crea bozza** o **Salva bozza**; quando non manca più nulla, premete **Invia per approvazione**.
7. L'approvatore sceglie una **Designazione**, scrive un **Motivo** di almeno 10 caratteri e preme **Approva** o **Restituisci**.
8. Se un piano approvato deve cambiare, scrivete un motivo e usate **Modifica**. Gli elenchi **Registrazioni di approvazione** e **Revisioni** mostrano lo storico.

## Cosa significano gli stati
- **Bozza:** il piano può essere modificato.
- **In revisione:** il piano è stato inviato per approvazione; si attende una decisione.
- **Restituito:** l'approvatore ha rimandato indietro il piano con un motivo; correggetelo e inviatelo di nuovo.
- **Approvato:** il Comandante o il Comandante del rimorchiatore ha approvato questa revisione del piano. Non è una decisione GO; la verifica di prontezza e il briefing restano necessari.
- **Sconosciuto:** per il campo non è stato scelto alcun valore. "Sconosciuto" per le zone di snap-back non sostituisce mai "No".
- **La richiesta di rimorchiatore è cambiata dopo il salvataggio di questo piano.:** il piano si basa su una revisione precedente della richiesta e deve essere riesaminato.

## Cosa non si può fare / attenzione
- La persona che ha redatto o inviato il piano non può approvarlo.
- Un piano con sezioni obbligatorie mancanti non può essere inviato; una bozza incompleta può essere salvata.
- Il sistema non propone alcun valore di limite, canale o soglia; ogni valore proviene dal pianificatore con la sua fonte.
- Un piano approvato non è un'autorizzazione all'avvio; l'avvio richiede conferme attuali del briefing e la registrazione GO del Comandante del rimorchiatore (13.03 §3).
- I comandi sono disattivati offline.

## Se qualcosa va storto
- **Messaggio "Required sections are missing.":** completate i codici in **Mancanti prima dell'invio**.
- **Messaggio "The record changed":** usate **Ricarica** per esaminare il piano attuale.
- **Messaggio "The outcome is unknown":** **Riprova** invia di nuovo la stessa richiesta.
- **Nessuna autorizzazione:** controllate il vostro ruolo, il rimorchiatore e il contesto di designazione; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).

## Pagine correlate
