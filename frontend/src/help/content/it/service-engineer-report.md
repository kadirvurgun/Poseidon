---
key: service-engineer-report
title: Rapporti di servizio e del tecnico
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina registra ed elenca i **rapporti delle visite di servizio e dei tecnici** sulla nave selezionata. Risponde alla domanda: "Chi ha eseguito quale lavoro su quale apparecchiatura e per quale ordine di lavoro, quali ricambi e misure sono stati registrati, e il rapporto è verificato?" Un rapporto è collegato a un ID apparecchiatura, a un ordine di lavoro e a difetti, quindi è legato alla cronologia dell'ordine di lavoro e dell'apparecchiatura (13.03 §5). Un rapporto verificato è un record umano; non chiude un difetto né un ordine di lavoro.

## Chi la usa
- **Direttore di macchina / Sovrintendente tecnico:** per collegare i rapporti alla cronologia di apparecchiature e ordini di lavoro, seguire ciò che manca e verificare i rapporti di cui non sono autori (13.03 §5).
- **Tecnico:** per inserire un rapporto come bozza dopo un intervento e inviarlo per verifica (13.03 §6).

## Prima di iniziare
- Se non è selezionata alcuna nave, la pagina indica **Scegli una nave.** ed elenca le navi del vostro ambito. Selezionate la nave corretta.
- Per vedere i rapporti serve l'accesso in lettura ai rapporti di servizio e alle apparecchiature. Il pulsante **Nuovo rapporto** viene mostrato solo con accesso in scrittura e mentre siete online.
- Tenete pronti gli ID di apparecchiatura, ordine di lavoro, difetto e file di evidenza. I campi ordine di lavoro, difetto ed evidenza vengono mostrati solo se potete leggere quei record.

## Passo dopo passo
1. Aprite un rapporto dall'elenco **Rapporti**, oppure avviate una nuova bozza con **Nuovo rapporto**.
2. Compilate **N. rapporto**, **Fornitore del servizio**, **Tecnico**, **Data del servizio**, **Motivo e ambito** e **Lavori eseguiti**.
3. Se necessario, usate **Aggiungi ricambio** e **Aggiungi misura** per inserire i ricambi usati e le misure. Se non conoscete il risultato di una misura, lasciatelo come **Sconosciuto**.
4. In **Collegamenti**, inserite l'**ID apparecchiatura**, l'**ID ordine di lavoro** e gli ID dei difetti e dei file di evidenza collegati (uno per riga).
5. Salvate con **Salva bozza**. Se il rapporto mostra un elenco **Mancante prima dell’invio**, completate prima quelle voci.
6. Inviate il rapporto con **Invia per verifica**.
7. Su un rapporto inviato di cui non siete autori, scrivete un **Motivo** di almeno 10 caratteri e usate **Registra verifica** o **Restituisci all’autore**.
8. Per modificare un rapporto verificato, scrivete un motivo e usate **Modifica (nuova revisione)**. Ogni revisione resta in **Cronologia immutabile**.

## Cosa significano gli stati
- **Bozza / Inviato / Restituito:** il ciclo di vita del rapporto. Inviare non è verificare.
- **Verificato (decisione umana registrata) / Verificato da un non autore:** una persona autorizzata che non è autrice del rapporto e non lo ha inviato ha registrato una verifica. Completato e Verificato non sono la stessa cosa.
- **In attesa di verifica:** il rapporto è stato inviato e non è ancora verificato.
- **È stata verificata una revisione precedente, non questa:** dopo una modifica la nuova revisione richiede di nuovo una verifica.
- **revisione attuale / modificato dal collegamento / non disponibile:** lo stato di un'apparecchiatura, di un ordine di lavoro o di un difetto collegato rispetto alla revisione che era stata collegata.
- **Trasferimento accettato, non prova verificata:** un file allegato è soltanto un trasferimento accettato.
- Un risultato di misura **Sconosciuto** non significa "Entro i limiti".

## Cosa non si può fare / attenzione
- Non potete verificare un rapporto di cui siete autori o che avete inviato; il server lo rifiuta.
- Un rapporto verificato non chiude un difetto né un ordine di lavoro, e non dimostra che l'apparecchiatura sia sicura.
- Gli originali delle evidenze non vengono modificati; una modifica apre una nuova revisione e la vecchia revisione resta nella cronologia (13.03 §1).
- Offline non viene registrato nulla. Quando la connessione ritorna, **Riprova lo stesso comando** invia di nuovo lo stesso comando.

## Se qualcosa va storto
- **Mancano dati o record obbligatori:** completate l'elenco delle voci mancanti e salvate di nuovo.
- **Qualcun altro ha modificato questo record:** usate **Ricarica** per aprire la versione più recente, poi salvate di nuovo.
- **Non hai l’autorizzazione del server per questa azione o questo record:** controllate il vostro ruolo e il contesto della nave; se necessario, contattate l'amministratore del tenant (13.03 §10).
- **Un rapporto non può essere verificato:** controllate i record collegati e i file di evidenza; parlatene con un verificatore autorizzato (13.03 §10).
- **Impossibile caricare il record:** inviate al supporto la schermata, l'ora e i passaggi eseguiti (13.03 §10).

## Pagine correlate
