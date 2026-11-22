---
key: tug-readiness
title: Verifica prontezza rimorchiatore
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La verifica di prontezza valuta la prontezza operativa di un'assegnazione di rimorchiatore a partire dai controlli delle fonti, **solo come parere**, e consente al Comandante del rimorchiatore o al Comandante designato di registrare una **decisione GO/NO-GO** motivata. Risponde alla domanda: "Cosa dicono i controlli su equipaggio, attrezzature, PMS, certificati, comunicazione, meteo, margine di tiro a punto fisso, assegnazione e briefing, e quali blocchi esistono?" La valutazione non è una decisione: "Il sistema non emette mai GO; solo il comandante o comandante del rimorchiatore designato registra una decisione GO/NO-GO motivata."

## Chi la usa
- **Comandante del rimorchiatore / Comandante:** per esaminare la valutazione e i blocchi e registrare una decisione GO o NO-GO (13.03 §3).
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per seguire prontezza e blocchi; non decidono (13.03 §7).

## Prima di iniziare
- Aprite la pagina da un'assegnazione: in **Assegnazione e conflitti**, usate il collegamento **Verifica di prontezza operativa** sotto una proposta. Senza un'assegnazione selezionata la pagina mostra **Seleziona un'assegnazione dall'elenco del lavoro.**
- La registrazione di una decisione richiede l'accesso in scrittura ai rimorchiatori e una **Designazione** registrata dal server (Comandante del rimorchiatore o Comandante) per quel rimorchiatore, e l'assegnazione deve essere ancora Proposed.
- Senza briefing non c'è READY: **Rischi e briefing di rimorchio** deve essere emesso e confermato da ogni partecipante (13.03 §3).

## Passo dopo passo
1. Leggete il badge di stato sulla riga **Valutazione**. La nota accanto indica **Indicativo, non una decisione GO.**
2. Controllate **Tiro a punto fisso richiesto (t)**, **Tiro a punto fisso disponibile verificato (t)** e **Margine di tiro a punto fisso (t)**. Un valore mancante mostra "Sconosciuto".
3. Esaminate i codici sotto **Blocchi critici**, **Motivi di revisione** e **Condizioni**.
4. Nella tabella delle categorie, vedete il risultato di ogni categoria (Equipaggio, Competenza di rimorchio, Attrezzature, Attrezzature di rimorchio, PMS e difetti, Certificati, Comunicazione, Meteo, Margine di tiro a punto fisso, Assegnazione, Rischi e briefing di rimorchio). Per i dettagli, leggete i codici motivo nella tabella **Verifiche delle fonti**.
5. Se avete l'autorità decisionale, scegliete **NO-GO** o **GO** nel modulo **Registra la decisione del Comandante**, inserite la vostra **Designazione** e un **Motivo**, e premete **Registra**.
6. Consultate le decisioni registrate nell'elenco **Decisioni del Comandante**.

## Cosa significano gli stati
- **Pronto (READY):** ogni controllo delle fonti è superato (nessun blocco, valore sconosciuto o condizione). È un'informazione, non una decisione; la decisione GO viene comunque registrata dal Comandante del rimorchiatore o dal Comandante.
- **Conditional (CONDITIONAL):** si applicano le condizioni indicate in **Condizioni**. GO è disattivato e la pagina spiega: "Questa valutazione è CONDITIONAL. GO richiede una valutazione READY: nessuna fonte definisce quali condizioni consentirebbero GO, quindi il server lo rifiuta. Risolvete le condizioni e rivalutate, oppure registrate NO-GO."
- **Not ready (NOT_READY):** esiste almeno un blocco critico.
- **Review required (REVIEW_REQUIRED):** alcune fonti sono sconosciute o in attesa di revisione. Un controllo sconosciuto non viene mai conteggiato come "Pronto".
- **Pass / Fail / Unknown:** il risultato di una singola categoria o di un singolo controllo.
- **GO / NO-GO:** la decisione umana e il motivo registrati dal Comandante del rimorchiatore o dal Comandante.

## Cosa non si può fare / attenzione
- GO può essere registrato solo con una valutazione READY; il server rifiuta GO con CONDITIONAL, NOT_READY o REVIEW_REQUIRED. La pagina attiva GO solo con una valutazione READY; altrimenti indica **GO può essere registrato solo con una valutazione PRONTO.** NO-GO può sempre essere registrato con un motivo.
- Non c'è READY finché rischio e briefing sono incompleti o mancano conferme dei partecipanti; l'opzione GO è allora disattivata (13.03 §3).
- Una proposta di assegnazione di un utente a terra non prevale mai sulla decisione di sicurezza del Comandante del rimorchiatore a bordo (13.03 §7).
- Se la valutazione cambia prima della vostra decisione, la decisione viene rifiutata; esaminate di nuovo la valutazione attuale.
- I comandi sono disattivati offline.

## Se qualcosa va storto
- **Messaggio "The assessment changed":** usate **Ricarica**, esaminate la valutazione attuale e decidete di nuovo.
- **Messaggio "The outcome is uncertain":** **Riprova** invia lo stesso comando.
- **Prontezza inattesa:** aprite la tabella delle categorie e i motivi dei controlli delle fonti; esaminateli con il responsabile del controllo o con un esperto della materia (13.03 §10).
- **Nessuna autorizzazione:** controllate il vostro ruolo, il rimorchiatore e il contesto di designazione; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).

## Pagine correlate
