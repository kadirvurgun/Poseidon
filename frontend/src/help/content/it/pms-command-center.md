---
key: pms-command-center
title: Centro di comando PMS
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il centro di comando PMS raccoglie in una sola schermata le **eccezioni** di manutenzione della nave selezionata. Risponde alla domanda: "Questa nave ha lavori critici scaduti, ordini di lavoro aperti, avvisi sui difetti o carenze di ricambi critici, e perché?" La pagina è una vista consultiva delle eccezioni; non è una decisione di idoneità, prontezza o sicurezza. Le voci in buono stato non vengono elencate, solo contate.

## Chi la usa
- **Direttore di macchina / Sovrintendente tecnico:** per seguire lavori scaduti, ordini di lavoro aperti, avvisi sui difetti e carenze di ricambi critici (13.03 §5).
- **Comandante:** per vedere l'impatto in termini di restrizioni operative dei record PMS e dei difetti (13.03 §2).

## Prima di iniziare
- Per vedere la pagina serve il permesso di lettura delle apparecchiature (`equipment.read`).
- Ogni sezione dipende dal permesso di lettura del proprio modulo. Per esempio, senza permesso di lettura dei difetti la sezione **Avvisi di condizione** resta "Accesso richiesto: non mostrato".
- Scegliete prima una nave. Senza nave la pagina mostra solo l'elenco **Scegli una nave.**.

## Passo dopo passo
1. Scegliete la nave dall'elenco. Se nessuna nave è nel vostro ambito, la pagina indica "Nessuna nave nel tuo ambito."
2. Leggete in alto il badge **Riepilogo eccezioni** e l'ora di **Ultimo aggiornamento**.
3. Esaminate le sezioni: **Lavori critici scaduti**, **Lavori in scadenza, scaduti e da rivedere**, **Ordini di lavoro aperti**, **Avvisi di condizione** e **Carenze di ricambi critici**.
4. Aprite **Perché** su una voce per vedere i codici di motivo registrati e, se presente, l'ora **Osservato**.
5. Usate **Apri record di origine** per andare alla fonte. Una voce di difetto apre [Difetti e misure temporanee](/app/defects), una voce di ordine di lavoro apre [Centro ordini di lavoro](/app/work-order-center).
6. Premete **Aggiorna** per leggere di nuovo i dati.

## Cosa significano gli stati
- **Bloccato / Revisione richiesta / Attenzione:** lo stato del riepilogo eccezioni. I dettagli sono nelle voci delle sezioni.
- **Nessuna eccezione aperta (consultivo):** le fonti lette non hanno restituito eccezioni aperte. Questo non è un risultato di prontezza o idoneità.
- **Critico / Attenzione / Revisione richiesta:** la gravità di una voce.
- **Fonte non disponibile: sconosciuto, non zero:** non è stato possibile leggere la fonte della sezione. Sconosciuto non significa "zero", "nessun problema" né "Pronto".
- **Accesso richiesto: non mostrato:** non avete il permesso per questa sezione; non vengono mostrati né il contenuto né il conteggio.
- **Nessuna eccezione da questa fonte:** la sezione è stata letta e non ha restituito eccezioni. Il numero di voci in buono stato è mostrato a parte.
- **Dati non aggiornati / Attualità sconosciuta:** la lettura alla base della voce non è attuale, oppure la sua attualità è sconosciuta.

## Cosa non si può fare / attenzione
- La pagina non modifica alcun record; si limita a leggere. Ordini di lavoro, difetti e ricambi si gestiscono nelle rispettive pagine.
- La pagina non dichiara mai una nave "pronta" o "sicura". La decisione di partenza spetta al Comandante (13.03 §2).
- Un difetto conta come chiuso solo tramite un record di chiusura umano, e solo per la revisione della dichiarazione che è stata chiusa. Se la dichiarazione cambia in seguito, la voce torna in revisione.
- I dati mostrati offline possono essere vecchi, e **Aggiorna** non è disponibile.

## Se qualcosa va storto
- **Impossibile caricare il record:** controllate la connessione e usate **Ricarica**.
- **Non hai l’autorizzazione del server per questa azione o questo record:** controllate il vostro ruolo e il contesto della nave; se necessario, contattate l'amministratore del tenant (13.03 §10).
- **Eccezione inattesa:** aprite **Perché** ed esaminate il record di origine con il responsabile o con un esperto della materia (13.03 §10).
- **Errore dell'applicazione:** segnalate al supporto il Correlation ID, la schermata, l'ora e i passaggi eseguiti (13.03 §10).

## Pagine correlate
