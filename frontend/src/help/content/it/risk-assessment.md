---
key: risk-assessment
title: Valutazione dei rischi
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Valutazione dei rischi registra pericoli, conseguenze possibili, misure di controllo, rischio iniziale e residuo e condizioni di arresto del lavoro per un lavoro o un'operazione su una nave. Risponde alla domanda: "I pericoli e i controlli per questo lavoro sono stati considerati, rivisti e confermati dai partecipanti?" La pagina è **solo un obiettivo di sviluppo**: un'approvazione qui è una revisione umana registrata, non un permesso di lavoro, un'accettazione del rischio da parte di un esperto né il superamento di un gate HSE (13.03 §2, §4).

## Chi la usa
- **Comandante e Comandante del rimorchiatore:** fanno preparare la valutazione per i lavori sulla nave o sul rimorchiatore e prendono la decisione di arresto del lavoro. L'autorità decisionale resta a loro (13.03 §2, §3).
- **DPA / HSEQ:** supervisionano la governance del rischio e registrano la decisione di revisione (13.03 §4).
- **Equipaggio / OOW / Macchinista:** come partecipanti, confermano o contestano per sé stessi la valutazione approvata (13.03 §6).

## Prima di iniziare
- Scegliete una nave per aprirne le valutazioni dei rischi. La lettura richiede il permesso di lettura dei rischi; scrivere una bozza richiede il permesso di scrittura dei rischi e l'ambito di modifica su quella nave. La decisione di revisione richiede il permesso di approvazione dei rischi; la conferma richiede il permesso di presa visione dei rischi.
- Le valutazioni numeriche richiedono un metodo di rischio (matrice) pubblicato nel tenant. Senza di esso, le valutazioni restano **Non valutato**. Un responsabile HSEQ del tenant configura la matrice e un'altra persona la pubblica (pagina Libreria modelli di rischio).
- I comandi sui rischi richiedono una connessione online attiva; offline nulla viene messo in coda o approvato.

## Passo dopo passo
1. Scegliete la nave e aprite una valutazione dall'elenco, oppure premete **Nuova valutazione**.
2. Inserite un **Numero di valutazione**. Facoltativamente inserite un **Tipo di lavoro** e usate **Parti da un modello pubblicato (solo copia)** per scegliere un modello corrispondente; il modello viene soltanto copiato nella nuova bozza.
3. Compilate **Titolo**, **Contesto operativo / di lavoro**, **Tipo di lavoro**, **Ambito del lavoro** e **Metodo di rischio**. Per **Arresto del lavoro richiesto** scegliete **Sì**, **No** o **Sconosciuto** e, se necessario, scrivete le **Stop-work conditions**.
4. Con **Aggiungi pericolo**, inserite per ogni pericolo la **Fase di lavoro**, le **Conseguenze possibili**, il **Rischio iniziale** e il **Rischio residuo** (Probabilità, Gravità, Motivazione). Con **Aggiungi misura**, aggiungete le **Misure di controllo**, il **Responsabile della misura** e i **Riferimenti alle barriere**.
5. In **Partecipanti** scrivete un ID attore e un ruolo per riga. Se necessario collegate record correlati con **Aggiungi collegamento**.
6. Premete **Salva bozza**, poi **Invia in revisione**. **Elementi bloccanti** elenca ciò che manca.
7. Un revisore scrive un **Motivo** di almeno 10 caratteri e preme **Approva (registrazione di revisione umana)** o **Restituisci**.
8. Dopo l'approvazione ogni partecipante preme per sé stesso **Conferma** oppure, con un motivo, **Contesta**. Se serve una modifica usate **Modifica (apre una nuova bozza)**.

## Cosa significano gli stati
- **Bozza / In revisione / Restituito / Approvato (registrazione umana):** il ciclo di vita della valutazione. Approvato è un record di revisione umana, non un permesso di lavoro.
- **Non valutato / Metodo non configurato:** non esiste alcuna valutazione numerica. Il sistema non usa alcuna matrice predefinita, moltiplicazione o soglia basso/medio/alto.
- **Arresto del lavoro richiesto — Sconosciuto:** non è ancora stata presa alcuna decisione; non viene mai letto come "No".
- **Conferma non applicabile prima dell’approvazione / Conferme in attesa / Tutti i partecipanti hanno confermato / Contestato – non risolto:** lo stato di conferma dei partecipanti. Una contestazione non viene mai chiusa dalla conferma di un'altra persona.
- **Modificato dopo il fissaggio / Non disponibile:** un record collegato è cambiato dopo la revisione fissata o non può più essere letto; esaminatelo di nuovo.

## Cosa non si può fare / attenzione
- Un autore o chi ha inviato il ciclo di revisione non può approvare la stessa valutazione (separazione dei compiti).
- Ognuno conferma solo per sé stesso e solo la revisione approvata esatta.
- Un'approvazione qui non è un permesso di lavoro, un'accettazione del rischio da parte di un esperto né un gate HSE. La decisione di avviare o fermare il lavoro resta al Comandante o al Comandante del rimorchiatore.
- Una bozza avviata da un modello non contiene valutazioni numeriche, firme, conferme dei partecipanti né approvazioni.
- Offline una valutazione non può essere inviata, approvata né confermata.

## Se qualcosa va storto
- **Bloccato finché gli elementi elencati non sono completati.** Completate gli elementi bloccanti e inviate di nuovo.
- **This record changed elsewhere.** Premete **Ricarica** per vedere la revisione corrente, poi ripetete l'azione.
- **Non sei autorizzato a questa azione (separazione dei compiti o ambito).** Chiedete a un'altra persona autorizzata di eseguire la revisione, oppure controllate il vostro ruolo e il contesto della nave; se necessario contattate l'amministratore del tenant (13.03 §10).
- **L’area rischi non è disponibile con l’accesso attuale.** Controllate i vostri permessi.
- Se una valutazione numerica appare inattesa, controllate il metodo di rischio e la sua revisione; rivolgetevi al responsabile del controllo o al responsabile HSEQ (13.03 §10).

## Pagine correlate
