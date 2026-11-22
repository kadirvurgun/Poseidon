---
key: tug-demo-journey
title: Percorso demo del rimorchiatore
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [tug-job-intake, tug-assignment, tug-readiness, towage-plan, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il percorso demo è una visita guidata delle vere schermate dei rimorchiatori con **dati sintetici, non firmati e non verificati**. Risponde alla domanda: "Attraverso quali schermate passa un lavoro di rimorchio dalla richiesta allo storico, e quale controllo applica il server a ogni passo?" La demo non crea alcuna decisione, rilascio o verifica; si limita a collegare le schermate reali e a spiegarle.

## Chi la usa
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per imparare il flusso di lavoro e il controllo a ogni passo (13.03 §7).
- **Comandante del rimorchiatore:** per vedere dove si collocano, dall'inizio alla fine, i passi decisionali di bordo (GO di prontezza, esecuzione) (13.03 §3).

## Prima di iniziare
- Selezionate il rimorchiatore; senza un rimorchiatore selezionato la pagina mostra **Seleziona un rimorchiatore per continuare.**
- Se aprite la pagina con un lavoro, i collegamenti dei passi portano con sé quel lavoro.
- Ogni collegamento apre la schermata reale; si applicano le regole di autorizzazione proprie di quella schermata.

## Passo dopo passo
1. Seguite i passi in ordine: **Acquisizione missione**, **Assegnazione del rimorchiatore**, **Prontezza di tiro al punto fisso, equipaggio e attrezzature**, **Piano di rimorchio**, **Valutazione del rischio e briefing**, **Master release (record umano GO)**, **Esecuzione**, **Evidenze e verifica**, **Storico delle operazioni**, **Continuità offline**.
2. A ogni passo, usate **Apri schermata** per andare alla schermata reale.
3. Aprite **Perché** per leggere il controllo che il server applica a quel passo; ad esempio, il piano viene approvato da un Comandante del rimorchiatore o da un Comandante che non lo ha redatto, e ogni partecipante conferma esattamente la revisione emessa.

## Cosa significano gli stati
- Questa pagina non ha un proprio badge di stato. Il significato degli stati sulle schermate che aprite è descritto nella guida di quella schermata.
- Come spiega il passo di prontezza, UNKNOWN resta REVIEW_REQUIRED, mai READY.
- GO è una registrazione umana legata alla valutazione di prontezza attuale; il sistema non la crea mai.

## Cosa non si può fare / attenzione
- I dati demo sono fittizi e non verificati. Rilasci, decisioni GO e verifiche sono sempre registrati da persone autorizzate, mai dalla demo.
- Un risultato visto sulle schermate demo non sostituisce mai una decisione di sicurezza per un'operazione reale; la decisione resta al Comandante del rimorchiatore (13.03 §3).
- Un'assegnazione da terra non prevale mai sulla decisione di sicurezza del Comandante del rimorchiatore a bordo (13.03 §7).

## Se qualcosa va storto
- **Messaggio "This page address is not valid.":** riaprite la pagina dal menu.
- **La schermata di un passo non si apre o manca l'accesso:** controllate il vostro ruolo e il contesto del rimorchiatore per quella schermata; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).
- **Risultato di prontezza inatteso:** aprite la vista Perché della schermata collegata ed esaminatela con il responsabile del controllo (13.03 §10).

## Pagine correlate
