---
key: high-risk-work
title: Lavori ad alto rischio
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Lavori ad alto rischio mostra in un unico elenco **di sola lettura** le eccezioni per i lavori della flotta come lavori a caldo, spazi confinati, lavori in quota, isolamento elettrico, manutenzione critica, rimorchio e bunkeraggio. Risponde alla domanda: "Quale lavoro ad alto rischio ha una questione aperta su rischio, permesso, barriera critica, equipaggio o attrezzatura, e chi deve compiere l’azione successiva?" Ogni dimensione è mostrata separatamente; non esiste un punteggio aggregato. Le azioni aprono il record di riferimento; questa pagina non modifica alcun record e non prende alcuna decisione di inizio lavori (13.03 §4).

## Chi la usa
- **DPA / HSEQ:** nell’ambito della governance del rischio, segue le eccezioni dei lavori ad alto rischio in tutta la flotta (13.03 §4).
- **Comandante e Comandante del rimorchiatore:** vedono gli ostacoli ai lavori ad alto rischio sulla propria unità e passano al record di riferimento. La decisione di iniziare o interrompere il lavoro resta loro (13.03 §2, §3).

## Prima di iniziare
- La visualizzazione richiede i permessi di lettura del rischio e di lettura dei lavori ad alto rischio. Senza di essi vedi "Non hai accesso alla vista dei lavori ad alto rischio."
- L’elenco copre solo le flotte e le unità nel tuo ambito.
- Offline l’elenco mostrato potrebbe non essere aggiornato.

## Passo dopo passo
1. Scegli i filtri **Flotta**, **Nave** e **Tipo di lavoro**. **Tutti** rimuove un filtro.
2. Nella tabella leggi separatamente gli stati **Rischio**, **Permesso**, **Barriera critica**, **Equipaggio** e **Attrezzatura** di ogni lavoro. I badge **Blocchi critici** ed **Elementi da rivedere** mostrano i conteggi.
3. Usa le colonne **Azione successiva** e **Responsabile / scadenza** per vedere chi deve fare che cosa.
4. Per vedere perché una riga si trova nel suo stato premi **Perché**. Si apre una tabella **Dimensione**, **Stato** e **Motivo**; chiudila con **Nascondi**.
5. Per modificare qualcosa segui il link del lavoro o **Apri record**; l’azione si svolge nella pagina del record di riferimento.
6. Sfoglia le pagine con **Precedente** e **Successivo**. **Valutato il** e **Copertura delle fonti** indicano quando e da quali fonti è stato calcolato l’elenco.

## Cosa significano gli stati
- **Ready / Conditional / Attention / Not ready / Blocked:** lo stato della dimensione nella sua fonte. Una dimensione Ready non significa che il lavoro possa iniziare; la decisione resta alla persona autorizzata.
- **Revisione necessaria:** una persona deve esaminarlo.
- **Sconosciuto:** la fonte non è disponibile; non è mai pronto. POSEIDON non dispone ancora di una fonte di permessi di lavoro, quindi la colonna **Permesso** mostra sempre **Sconosciuto**.
- **Healthy / Degraded / Failed:** lo stato della barriera critica.
- **Non classificato:** il tipo di lavoro non è noto o la sua criticità non è determinata; non viene escluso dall’elenco.
- **Waiting for source / Resolve stop or dispute / Renew expired item / Complete required item / Reassess / Open owner record:** l’azione successiva, mostrata in un ordine di priorità fisso.
- **Due unresolved / Not planned:** la scadenza o l’inizio pianificato non sono noti.

## Cosa non si può fare / attenzione
- Questa pagina è di sola lettura; non modifica, non approva e non autorizza nulla.
- Non esiste un punteggio aggregato; un lavoro con un blocco critico è sempre ordinato in cima, e il buono stato delle altre dimensioni non lo nasconde mai.
- Non interpretare **Sconosciuto** come pronto. Un permesso attivo storico non è mai un permesso valido attuale.
- La decisione di iniziare o interrompere il lavoro resta al Comandante o al Comandante del rimorchiatore.

## Se qualcosa va storto
- **Il servizio non è disponibile. Riprova.** Premi **Riprova**.
- **Sei offline. L'elenco mostrato potrebbe non essere aggiornato.** Quando sei online premi **Ricarica**.
- Se uno stato sembra inatteso, apri la tabella **Perché** e chiedi al responsabile del record o al responsabile del controllo (13.03 §10).
- Se non hai accesso, controlla il tuo ruolo e il contesto dell’unità; se necessario contatta l’amministratore del tenant (13.03 §10).

## Pagine correlate
