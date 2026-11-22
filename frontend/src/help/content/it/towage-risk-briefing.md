---
key: towage-risk-briefing
title: Rischi e briefing di rimorchio
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina prepara ed emette il **record di rischio e briefing di rimorchio** per un lavoro di rimorchio inviato e raccoglie le **conferme di ricezione** dei partecipanti. Risponde alla domanda: "Quali condizioni, pericoli, barriere preventive e di mitigazione, autorità di sospensione del lavoro e disposizioni di emergenza sono stati illustrati nel briefing, qual è lo stato della valutazione del rischio collegata, e ogni partecipante ha confermato la revisione emessa?" L'emissione del briefing è una registrazione umana del Comandante o del Comandante del rimorchiatore designato; non è una decisione GO né un permesso. Un briefing incompleto blocca READY.

## Chi la usa
- **Comandante del rimorchiatore / Comandante:** per preparare ed emettere il briefing e assicurarsi che nessuna decisione READY venga presa prima che rischio e briefing siano completi (13.03 §3).
- **Equipaggio e altri partecipanti (pilota, ufficiale, personale di coperta, macchinista):** per confermare o contestare il briefing emesso nel giusto contesto di ruolo (13.03 §6).
- **Coordinatore delle operazioni dei rimorchiatori:** per seguire lo stato del briefing e delle conferme (13.03 §7).

## Prima di iniziare
- Aprite il rimorchiatore da **Operazioni rimorchiatore** e scegliete il lavoro da **Seleziona un lavoro di rimorchiatore inviato**.
- Il **Piano di rimorchio** del lavoro deve essere approvato e la valutazione del rischio collegata deve essere approvata; in caso contrario il briefing non può essere emesso.
- L'emissione richiede una **Designazione** registrata dal server (Comandante del rimorchiatore o Comandante) per il rimorchiatore coordinatore. Richiede inoltre l'accesso in lettura alla valutazione del rischio e l'accesso in scrittura al rimorchiatore coordinatore. La conferma è riservata alle persone presenti nell'elenco dei partecipanti.

## Passo dopo passo
1. Nella riga superiore, leggete **Stato**, **Revisione**, **Conferma di ricezione**, **Stato del rischio** e **Stato del piano**.
2. In **Dato di prontezza (un briefing incompleto blocca READY)**, vedete i risultati e i codici motivo inviati alla verifica di prontezza.
3. Compilate **Condizioni confermate al briefing** (meteo, corrente, visibilità), **Pericoli**, **Barriere preventive**, **Barriere di mitigazione**, **Autorità di sospensione del lavoro** e **Disposizioni di emergenza**.
4. Inserite l'**ID della valutazione del rischio collegata**; in **Partecipanti**, usate **Aggiungi** per inserire l'**ID persona** e il **Ruolo** di ogni partecipante.
5. Salvate con **Crea bozza** o **Salva bozza**; quando **Mancanti prima dell'invio** è vuoto, scegliete una **Designazione** e premete **Emetti briefing**.
6. Come partecipante, leggete la revisione emessa e premete **Conferma**. Se non siete d'accordo, scrivete un **Motivo** di almeno 10 caratteri e premete **Contesta**.
7. Se il briefing deve cambiare, scrivete un motivo e usate **Modifica**. Gli elenchi **Conferme di ricezione** e **Revisioni** mostrano lo storico.

## Cosa significano gli stati
- **Draft / Issued:** lo stato del briefing. Emesso significa che le conferme possono iniziare; di per sé non è un risultato di prontezza.
- **Not issued / Pending / Complete / Disputed:** lo stato delle conferme dei partecipanti. Diventa **Completata** solo quando ogni partecipante ha confermato la revisione emessa.
- **Approved and acknowledged / Attention / Blocked / Review required:** lo stato della valutazione del rischio collegata.
- **Pass / Fail / Unknown:** il risultato del controllo inviato alla verifica di prontezza. Un briefing mancante o non emesso e le conferme in sospeso sono un **Non superato** che blocca READY.

## Cosa non si può fare / attenzione
- Nessuna decisione READY viene presa prima che rischio e briefing siano completi e le conferme dei partecipanti siano registrate (13.03 §3).
- Una contestazione porta lo stato delle conferme a **Contestata** e invia un Fail alla verifica di prontezza, quindi READY e GO sono bloccati. Il motivo compare in **Conferme di ricezione**. Una conferma non può essere modificata per la stessa revisione; per risolvere una contestazione il briefing viene modificato (nuova revisione), emesso di nuovo e confermato di nuovo da ogni partecipante.
- Se il piano o la valutazione del rischio cambiano dopo il briefing, il briefing deve essere riesaminato; le conferme precedenti non valgono per una nuova revisione.
- Nessuno conferma per conto di un altro; ogni partecipante registra la propria conferma (13.03 §6).
- Offline, da questa pagina non viene inviato alcun comando. Una conferma può essere messa in coda in **Operazioni offline del rimorchiatore**.

## Se qualcosa va storto
- **Messaggio "Required sections are missing.":** completate i codici mancanti; verificate che il piano e la valutazione del rischio siano approvati.
- **Messaggio "The record changed":** usate **Ricarica** per esaminare il briefing attuale.
- **Messaggio "The outcome is unknown":** **Riprova** invia di nuovo la stessa richiesta.
- **Nessuna autorizzazione:** controllate il vostro ruolo, il rimorchiatore e il contesto di designazione; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).

## Pagine correlate
