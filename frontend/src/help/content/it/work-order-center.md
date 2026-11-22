---
key: work-order-center
title: Ordini di lavoro
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## A cosa serve questa pagina
Il centro ordini di lavoro contiene le **bozze degli ordini di lavoro e i record di esecuzione dichiarati** della nave selezionata. Risponde alla domanda: "Quali ordini di lavoro esistono su questa nave, e quali sono stati rilasciati, eseguiti, con evidenze inviate e verificati da una persona?" Ogni revisione salvata viene conservata in una cronologia immutabile. La verifica e la chiusura non sono un rientro in servizio, un permesso né un'approvazione del rischio.

## Chi la usa
- **Direttore di macchina / Sovrintendente tecnico:** per aprire, rilasciare, verificare e chiudere ordini di lavoro pianificati e non pianificati (13.03 §5).
- **Macchinista / equipaggio:** per avviare il lavoro assegnato e registrare e inviare letture, ricambi, evidenze fotografiche e documentali (13.03 §6).

## Prima di iniziare
- La lettura richiede `workorder.read`, `equipment.read` e `maintenance.read`. Le bozze richiedono `workorder.write`, il rilascio `workorder.release`, l'esecuzione `workorder.execute`, la verifica `workorder.verify` e la chiusura `workorder.close`.
- Rilascio, verifica e chiusura richiedono un utente umano autenticato.
- Scegliete la nave corretta da **Seleziona una nave o un rimorchiatore**.

## Passo dopo passo
1. Scegliete la nave. In **Ordini di lavoro registrati**, filtrate questa pagina con **Cerca nei numeri degli ordini di lavoro restituiti** e aprite un record.
2. Per un nuovo lavoro, premete **Nuova bozza di ordine di lavoro**, compilate i campi e premete **Salva bozza non valutata**. Le bozze incomplete possono essere salvate.
3. In **Esecuzione ed evidenze**, se avete il permesso di rilascio, scegliete **Dichiara evidenze richieste** sotto **Politica delle evidenze richieste**, spuntate i tipi di evidenza e premete **Rilascia ordine di lavoro**. Se rilasciate senza dichiarare, la verifica resta bloccata finché chi rilascia non usa **Modifica evidenze richieste** → **Salva evidenze richieste**.
4. L'esecutore preme **Avvia lavoro**, compila **Letture**, **Ricambi consumati**, **Evidenze fotografiche e documentali**, **Lavoro svolto** e **Rilievi**, preme **Salva evidenze** e poi **Invia evidenze**.
5. Un verificatore autorizzato che non ha eseguito il lavoro esamina le evidenze. Preme **Registra verifica**, oppure sceglie un **Motivo della restituzione** e preme **Restituisci all'esecutore**.
6. Per un ordine di lavoro verificato, una persona autorizzata preme **Registra chiusura**.
7. Consultate le revisioni precedenti in **Cronologia immutabile degli ordini di lavoro**. Se serve un record di fermo, usate **Apri la bozza di fermo collegata**.

## Cosa significano gli stati
- **Not released for execution. / Released / In progress:** il lavoro non è ancora rilasciato, è rilasciato oppure è in esecuzione.
- **Evidenze inviate:** l'esecutore ha dichiarato il lavoro e le evidenze. Non significa verificato.
- **Verificato (registrazione umana):** registrato da un verificatore autorizzato che non ha eseguito il lavoro. **Chiuso (registrazione umana)** è un passo separato.
- **Required evidence missing / Present, pending human verification / Verified by a human:** lo stato del controllo sulle evidenze richieste.
- **Revisione richiesta: politica non configurata:** non è stata dichiarata alcuna evidenza richiesta. Non configurato non significa mai che non sia richiesto nulla; la verifica resta bloccata.
- **Restituito dal verificatore:** il lavoro è tornato all'esecutore con il motivo indicato.

## Cosa non si può fare / attenzione
- Se avete eseguito il lavoro, non potete verificarlo né chiuderlo. La pagina indica "Hai eseguito tu questo lavoro, quindi non puoi verificarlo né chiuderlo."
- Evidenze inviate, Verificato e Chiuso sono registrazioni umane separate. La pagina non dichiara mai un'attrezzatura pronta per il servizio.
- Il riepilogo **Fonte dell'ordine di lavoro salvato** descrive i campi della bozza salvata e afferma: "Rilascio, registrazione delle evidenze, verifica indipendente e chiusura sono registrati nel pannello di esecuzione. Le evidenze presentate sono accettate solo tramite verifica umana indipendente, mai dalla persona che ha eseguito il lavoro; non esiste una fase separata di accettazione delle evidenze e la verifica non è una decisione di rientro in servizio." Queste azioni si svolgono in **Esecuzione ed evidenze**.
- Le letture sono conservate come dichiarate; non viene applicata alcuna conversione di unità né soglia.
- La coda offline delle evidenze funziona solo quando esiste una politica offline approvata e mentre il lavoro è in corso. I conflitti non vengono mai uniti automaticamente; usate **Apri il centro conflitti** e decide una persona.

## Se qualcosa va storto
- **Il record è cambiato o questa azione non è consentita nel suo stato attuale:** usate **Ricarica** per leggere il record attuale e riprovate.
- **Esito sconosciuto:** **Riprova lo stesso comando** invia lo stesso comando; non crea un nuovo record.
- **Manca il permesso o l'autorità umana:** controllate il vostro ruolo e il contesto della nave; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).
- **Il controllo sulle evidenze richieste ha rifiutato questa azione:** completate i tipi di evidenza mancanti. Se un record non può essere verificato, controllate le evidenze e la loro provenienza con il verificatore (13.03 §10).

## Pagine correlate
