---
key: port-activity-sof
title: Attività portuale e SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Attività portuale e SOF conserva gli **eventi portuali** della nave selezionata come record operativo con riferimenti alle evidenze, e permette a una persona designata di finalizzare internamente lo **Statement of Facts (SOF)**. Risponde alla domanda: "Quali eventi portuali abbiamo dichiarato, in quale ordine e intervallo UTC, con quali riferimenti a carico, pescaggio, pilota, rimorchiatore, ritardi e proteste, e una persona designata ha finalizzato questa revisione del SOF?" La pagina non produce firme, laytime o demurrage, e gli eventi registrati non sono istruzioni né finestre pianificate approvate.

## Chi la usa
- **Comandante:** per conservare l'arrivo e l'attività portuale come record operativo con evidenze e per finalizzare il SOF (13.03 §2).
- **Primo Ufficiale / Direttore di macchina:** per registrare gli eventi portuali e, con una designazione valida, finalizzare il SOF.

## Prima di iniziare
- Nella barra di contesto in alto, selezionate la **società e la nave corrette**. Se non è selezionata alcuna nave, la pagina mostra "Seleziona una nave".
- La lettura richiede `operations.port.read`; il salvataggio delle bozze richiede `operations.port.write` e i diritti di modifica su questa nave.
- La finalizzazione richiede `operations.port.write`, i diritti di modifica su questa nave e `operations.port.finalize`, più una designazione di finalizzazione valida (Comandante, Primo Ufficiale o Direttore di macchina) per questa nave.

## Passo dopo passo
1. Scegliete la nave. **Cronologia degli eventi portuali registrati** elenca gli eventi restituiti nell'ordine UTC del server. **Filtra la pagina restituita** restringe solo questa pagina.
2. Leggete **Cronologia dello Statement of Facts** sotto l'elenco: ogni evento con **Intervallo registrato**, **Motivo del ritardo** e **Osservazioni**.
3. Premete **Nuovo record di bozza**. Compilate **Contesto dello scalo portuale**, **Attività e tempistiche registrate**, **Dichiarazioni sul carico e sui pescaggi** e **Assistenza, eccezioni ed evidenze**. Gli orari degli eventi richiedono Z o un offset UTC esplicito.
4. Premete **Salva bozza non verificata**. Ogni salvataggio aggiunge una revisione a **Cronologia delle revisioni registrate**.
5. Su un record salvato, aprite **Finalizzazione dello Statement of Facts**. Se è mostrato **Dati obbligatori mancanti**, completate prima quei campi.
6. Spuntate ogni riferimento non risolto elencato per prenderne atto, scegliete la vostra **Designazione** e premete **Finalizza SOF**. Il sistema registra chi ha finalizzato, quando, la funzione e il motivo.

## Cosa significano gli stati
- **BOZZA:** il SOF non è finalizzato.
- **FINALE (interno):** una persona designata ha finalizzato questa revisione. È interno: non è una firma né un accordo esterno.
- **Firma non registrata:** sempre mostrato; in questa pagina non esiste alcuna firma.
- **Modificato dopo FINALE: servono nuova revisione e finalizzazione.:** il record è stato salvato di nuovo dopo la finalizzazione ed è tornato in BOZZA.
- **UNKNOWN (riferimenti non risolti):** per questi riferimenti obbligatori non esiste una ricerca di appartenenza. Prenderne atto non li rende noti.
- **Revisione richiesta / Record di bozza non verificato:** gli eventi portuali sono dichiarazioni; nulla è verificato.
- **Bozza (non finalizzata) / Finale (interno; non è una firma):** lo stato del SOF che il server ha registrato per questa revisione, mostrato nel riepilogo del record. Finale deriva solo da una finalizzazione umana registrata.

## Cosa non si può fare / attenzione
- Il sistema non finalizza mai il SOF. La finalizzazione è un atto umano di un Comandante, Primo Ufficiale o Direttore di macchina designato (13.03 §2).
- Finalizza SOF resta disattivato finché mancano dati obbligatori, non esiste una designazione o non si è preso atto dei riferimenti non risolti.
- La finalizzazione non è una firma, non è un accordo esterno e non è laytime né demurrage.
- Il piè di pagina della pagina indica: "La finalizzazione del SOF è un atto umano interno registrato nel pannello di finalizzazione da un Comandante, Primo Ufficiale o Direttore di Macchina designato. Firme, approvazione operativa/legale e acquisizione offline rimangono non disponibili."
- Il salvataggio delle bozze e la finalizzazione richiedono una sessione online attiva; questa pagina non ha acquisizione offline.

## Se qualcosa va storto
- **"Nessuna designazione di finalizzazione valida per questa nave.":** verificate il vostro ruolo e il contesto della nave con l'amministratore del tenant (13.03 §10).
- **"Esito sconosciuto: riprovare invia lo stesso comando una sola volta.":** usate **Riprova**; non avviate una nuova finalizzazione.
- **"Comando non accettato":** leggete il codice mostrato, ricaricate il record con **Leggi il record salvato attuale** e riprovate.
- **"The saved source changed.":** ricaricate o scartate esplicitamente prima di salvare di nuovo.

## Pagine correlate
