---
key: today
title: Oggi
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## A cosa serve questa pagina
"Oggi" raccoglie in un unico elenco, per la nave o il rimorchiatore selezionato, gli **avvisi che richiedono ora la vostra attenzione**. Risponde alla domanda: "Quale record di questa unità attende oggi una decisione o un controllo, e quale fonte lo indica?" La pagina non decide nulla; mostra l'avviso, la sua fonte e il passo successivo.

## Chi la usa
- **Comandante** e **Comandante del rimorchiatore:** per vedere le eccezioni del giorno, il loro responsabile e il passo successivo (13.03 §2, §3).
- **Chief Engineer:** per seguire gli avvisi del piano di manutenzione (13.03 §5).
- **Crew / OOW:** per vedere il lavoro che spetta loro (13.03 §6).

## Prima di iniziare
- Nella barra di contesto in alto, verificate che siano selezionate la **società e l'unità corrette** (13.03 §1). Un avviso visualizzato appartiene all'unità selezionata.
- Per aprire questa pagina serve l'accesso in lettura all'area di lavoro.
- Se non è selezionata alcuna unità, la pagina mostra prima l'elenco **Seleziona una nave**.

## Passo dopo passo
1. Selezionate la nave o il rimorchiatore su cui state lavorando. L'unità selezionata è indicata come **Nave corrente**; usate **Cambia nave** per modificarla.
2. Osservate la sezione **Copertura delle fonti** in alto. Mostra quali fonti (normative, manutenzione pianificata, operazioni dei rimorchiatori) è stato possibile leggere.
3. Leggete le schede di avviso dall'alto verso il basso. Ogni scheda proviene da una fonte e suggerisce un passo successivo (ad esempio **Esamina bozza**, **Esamina il piano di manutenzione**, **Visualizza lavoro rimorchiatore**).
4. Premete **Perché?** per vedere il motivo di una scheda. La sezione mostra la regola, il record di origine, la sua revisione, l'ora di lettura e gli eventuali dati sconosciuti.
5. Seguite il collegamento della scheda alla pagina propria del record e agite lì, nei limiti delle vostre autorizzazioni.
6. Usate **Aggiorna** per rileggere i dati.

## Cosa significano gli stati
- **Disponibile (non verificato):** la fonte è stata letta. Ciò non significa che il suo contenuto sia stato verificato.
- **Missing / Unavailable in this context:** non è stato possibile leggere la fonte per questa unità. Un avviso che non vedete non significa che tutto sia in ordine.
- **Partial view:** alcune fonti non sono state lette. Un conteggio pari a zero non significa "nessun problema".
- **Every listed source was read and returned no items:** non è arrivato alcun avviso. Questo non è un nulla osta di sicurezza.
- **Sconosciuto:** il valore manca nella fonte o non è stato possibile determinarlo. Sconosciuto non significa "No", "Pronto" o "Non applicabile".
- **Review required / Planning attention / Advisory:** la priorità della scheda. Una scheda che richiede revisione attende che una persona autorizzata la esamini.
- **Completato ≠ Verificato:** completare un record non significa che un verificatore autorizzato lo abbia confermato (13.03 §1).

## Cosa non si può fare / attenzione
- Questa pagina non prende alcuna decisione di partenza, operativa o di sicurezza. Tale decisione spetta al Comandante o al Comandante del rimorchiatore autorizzato.
- Da questa pagina non viene eseguito alcun comando; le azioni avvengono sulla pagina propria del record.
- I dati mostrati offline sono vecchi e non verificati. I dettagli di **Perché?** non si aprono offline.
- Un'ora di lettura o di salvataggio non dimostra che la fonte sia corretta.

## Se qualcosa va storto
- **Offline:** proseguite il lavoro critico e osservate lo stato di sincronizzazione. Se compare un conflitto, andate al Centro conflitti (13.03 §10).
- **Accesso negato / Nave non disponibile nel vostro ambito:** controllate il vostro ruolo e il contesto dell'unità; se necessario, contattate l'amministratore del vostro tenant.
- **Avviso inatteso:** aprite **Perché?**; se la regola o la fonte sembrano errate, segnalatelo al responsabile del controllo o a un esperto della materia.
- **Fonte non disponibile / Troppe richieste:** attendete un momento e usate **Riprova**.
- **La risposta del server non ha superato la validazione:** nulla di essa viene mostrato; segnalatelo al supporto con uno screenshot, l'ora e i passi eseguiti.

## Pagine correlate
