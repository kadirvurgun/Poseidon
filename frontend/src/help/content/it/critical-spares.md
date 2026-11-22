---
key: critical-spares
title: Ricambi critici e richieste
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina dei ricambi critici e delle richieste mostra, per la nave selezionata, la **scorta dichiarata, il minimo e il massimo, gli avvisi di scorta bassa e le proposte di richiesta tecnica**. Risponde alla domanda: "Quale ricambio critico ha raggiunto il minimo, quanti ordini di lavoro aperti lo citano, ed è stata proposta e decisa una richiesta tecnica?" I conteggi sono dichiarazioni umane e non sono verificati rispetto a un magazzino.

## Chi la usa
- **Direttore di macchina / Sovrintendente tecnico:** per seguire le soglie minime dei ricambi critici e avviare una richiesta tecnica (13.03 §5). Chi propone una richiesta e chi la approva sono persone diverse.

## Prima di iniziare
- La visualizzazione richiede `spare.read`. Definire i ricambi e registrare i conteggi richiede `spare.write`, proporre una richiesta `requisition.propose` e deciderla `requisition.approve`.
- Proposte e decisioni richiedono un utente umano che abbia effettuato l’accesso.
- Scegli la nave corretta dall’elenco **Nave**.

## Passo dopo passo
1. Scegli la nave. Si apre la vista **Ricambi critici**. Leggi in alto i conteggi **Scorta bassa** e **Revisione necessaria**.
2. In ogni riga della tabella leggi **Criticità del ricambio**, **Criticità dell’apparecchiatura**, **Stato della scorta**, **Previsto**, **Min / max** e **Ordini di lavoro aperti**.
3. Premi **Nuovo ricambio critico** per un nuovo ricambio o **Modifica** per uno esistente. Inserisci **Codice articolo**, **Nome**, **Unità**, **Scorta minima** e **Scorta massima**. Lascia vuote le soglie se non sono state decise.
4. Per inserire un conteggio spunta **Registra un nuovo conteggio di giacenza**, compila **Quantità contata** e premi **Salva**.
5. In una riga **Scorta bassa** senza richiesta aperta premi **Proponi richiesta**. Il server calcola la quantità proposta (massimo meno scorta prevista). Se non è impostato un massimo, non viene proposta alcuna quantità e chi approva deve inserire **Quantità approvata**.
6. Nella vista **Richieste tecniche**, una persona autorizzata che non ha fatto la proposta controlla **Quantità approvata** e preme **Approva per l'esportazione ERP**, oppure sceglie un **Motivo del rifiuto** e preme **Rifiuta**.
7. Apri **Riferimento di esportazione ERP** su una richiesta approvata per vedere il riferimento.

## Cosa significano gli stati
- **Scorta bassa:** la scorta prevista (ultimo conteggio dichiarato meno i ricambi registrati negli ordini di lavoro inviati dopo quel conteggio) è pari o inferiore al minimo. La proposta di richiesta è offerta su questa riga.
- **Sopra il minimo (dichiarato):** la scorta prevista è sopra il minimo; il conteggio non è verificato.
- **Minimo non configurato:** non è stata inserita alcuna soglia. Una soglia vuota è segnalata come revisione necessaria, mai come in buono stato.
- **Stock unknown / Consumption unresolved:** non è stato possibile calcolare la scorta. Sconosciuto non significa "scorta sufficiente".
- **Proposed / Approved for ERP export / Rejected:** lo stato della richiesta tecnica.
- **Critical (declared) / Non-critical (declared) / Sconosciuto:** la criticità del ricambio è una dichiarazione.

## Cosa non si può fare / attenzione
- Non puoi decidere una richiesta che hai proposto tu. La pagina indica "L'hai proposta tu; deve decidere un'altra persona."
- Proposta e approvazione sono atti umani separati. L’approvazione crea solo un riferimento di esportazione ERP; qui non si gestiscono ordini di acquisto, fornitori o prezzi.
- La pagina non ha un campo separato per la soglia di riordino; la scorta bassa è valutata rispetto alla scorta minima.
- Offline i dati non possono essere aggiornati e non è possibile eseguire alcuna azione.

## Se qualcosa va storto
- **Il record è cambiato:** usa **Riprova** per aggiornare la pagina e riprova.
- **Outcome unknown:** **Riprova lo stesso comando** invia lo stesso comando.
- **La richiesta è stata rifiutata da una regola:** controlla lo stato della scorta e le informazioni sulla richiesta aperta.
- **Non hai il permesso:** controlla il tuo ruolo e il contesto della nave; se necessario contatta il tuo amministratore del tenant (13.03 §10).

## Pagine correlate
