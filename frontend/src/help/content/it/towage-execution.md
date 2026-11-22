---
key: towage-execution
title: Esecuzione del rimorchio
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La console di esecuzione registra come cronologia l'**esecuzione effettiva** di un lavoro di rimorchio inviato: l'avvio, le osservazioni, le azioni **Suspend / Resume / Abort / Complete** e la successiva verifica. Risponde alla domanda: "Quando è iniziata l'operazione, quando è stato collegato e sganciato il cavo di rimorchio, quali osservazioni sono state registrate, in quale stato si trova l'operazione e il record è stato verificato?" L'avvio e le transizioni sono registrazioni umane del Comandante del rimorchiatore o del Comandante sul rimorchiatore che esegue il lavoro; la console non emette mai un GO.

## Chi la usa
- **Comandante del rimorchiatore / Comandante:** per avviare l'operazione e registrare la cronologia Start/Suspend/Resume/Abort/Complete con le relative evidenze (13.03 §3).
- **Verificatore (una persona con autorità di verifica dell'esecuzione):** per verificare un record completato o interrotto, oppure rifiutarne la verifica (13.03 §1).
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per seguire la completezza delle evidenze e lo storico dei lavori completati; non possono avviare né interrompere un'operazione (13.03 §7).

## Prima di iniziare
- Aprite il rimorchiatore da **Operazioni rimorchiatore** e scegliete il lavoro da **Seleziona un lavoro di rimorchiatore inviato**.
- Per avviare, tutti questi elementi devono essere attuali: l'assegnazione attiva del rimorchiatore, un **Piano di rimorchio** approvato (il rimorchiatore deve far parte del piano), un **Rischi e briefing di rimorchio** emesso e confermato da ogni partecipante, e una registrazione **GO** nella **Verifica di prontezza operativa** legata alla valutazione attuale.
- L'avvio e le transizioni richiedono una **Designazione** registrata dal server (Comandante del rimorchiatore o Comandante) per quel rimorchiatore.

## Passo dopo passo
1. Nella sezione **Controllo di avvio**, leggete la **Decisione di prontezza** e i **Blocchi** di ciascun rimorchiatore.
2. Se non ci sono blocchi, leggete il testo di conferma come persona designata a bordo, scegliete la vostra **Designazione**, inserite facoltativamente una **Posizione di partenza** e premete **Avvia operazione**.
3. Una volta aperta l'esecuzione, seguite **Stato**, **Verifica** e i campi del registro (cavo di rimorchio collegato/sganciato, carico di tiro massimo osservato, comunicazione, verricello, prova di sgancio di emergenza, minuti attivi).
4. In **Registra osservazione**, scegliete il tipo di osservazione (ad esempio cavo di rimorchio collegato, carico di tiro, controllo comunicazioni, meteo, stato del verricello, prova di sgancio di emergenza, posizione, evidenza, nota), inserite i valori e premete **Registra osservazione**.
5. In **Azioni del comandante del rimorchiatore**, scegliete l'**Azione** (Suspend, Resume, Abort, Complete) e inserite la vostra **Designazione** e un **Motivo**. L'interruzione e il completamento richiedono un motivo di almeno 20 caratteri e aprono il campo **Posizione finale**; il completamento non è accettato senza una posizione finale.
6. Se sono elencati **Blocchi al completamento**, risolveteli; ad esempio, se il cavo di rimorchio è ancora collegato, registratene prima lo sgancio.
7. Come verificatore, scrivete un motivo e premete **Verifica record** o **Rifiuta verifica**.
8. Consultate tutti gli eventi in ordine nella **Cronologia**.

## Cosa significano gli stati
- **Active / Suspended / Completed / Aborted:** lo stato dell'operazione. Completato non significa che il record sia verificato.
- **Pending / Verified / Rejected:** lo stato della verifica. Completato ≠ Verificato; la verifica è una registrazione di un'altra persona.
- **Good / Lost / Pass / Fail / Conditional / Unknown:** il risultato delle osservazioni su comunicazione, verricello e sgancio di emergenza. Unknown indica che non è stato registrato nulla; non sostituisce mai "Buona" o "Superato".
- **Safety events — Unknown:** questa console non conta gli eventi di sicurezza; non si trae alcuna conclusione di "nessun evento".
- **GO (human record) / NO GO (human record):** la decisione registrata dal Comandante del rimorchiatore o dal Comandante nella verifica di prontezza.

## Cosa non si può fare / attenzione
- Gli utenti a terra, l'IA e le identità di sistema non possono avviare né interrompere un'operazione. Un'assegnazione da terra non prevale mai sulla decisione di sicurezza del Comandante del rimorchiatore a bordo (13.03 §7).
- Solo il Comandante del rimorchiatore o il Comandante designato può sospendere, riprendere, interrompere o completare. L'interruzione è sempre disponibile mentre l'operazione è in corso.
- La persona che ha avviato o concluso l'operazione non può verificare lo stesso record; la verifica avviene dopo la conclusione dell'operazione.
- Il registro è di sola aggiunta; gli eventi registrati non vengono mai cancellati né modificati.
- Offline non viene inviato alcun comando. Le osservazioni possono essere messe in coda in **Operazioni offline del rimorchiatore**; avvio, transizioni e verifica restano online.

## Se qualcosa va storto
- **Messaggio "The server listed missing or blocking items.":** risolvete i codici in **Blocchi** o **Blocchi al completamento** (ad esempio piano non approvato, conferma del briefing in sospeso, registrazione GO mancante o non attuale).
- **Messaggio "The record changed or the command conflicts.":** usate **Ricarica la versione più recente** per esaminare il record.
- **Messaggio "The outcome is unknown.":** **Riprova** invia di nuovo lo stesso identico comando.
- **Nessuna autorizzazione:** controllate il vostro ruolo, il rimorchiatore e il contesto di designazione; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).

## Pagine correlate
