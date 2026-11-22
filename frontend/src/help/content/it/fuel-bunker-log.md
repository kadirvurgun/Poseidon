---
key: fuel-bunker-log
title: Combustibile, bunker e ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina combustibile, bunker e ROB conserva le **dichiarazioni di combustibile e bunker** della nave selezionata come bozze non verificate, con la loro storia della fonte. Risponde alla domanda: "Quale combustibile è stato ricevuto o consumato, quale ROB abbiamo dichiarato prima e dopo, quali riferimenti BDN, fornitore, campione e serbatoio vi appartengono, e la catena del ROB dichiarato prosegue da un record al successivo?" La pagina non corregge le quantità, non calcola le emissioni e non stabilisce conformità né verifica.

## Chi la usa
- **Direttore di macchina / ufficiali di macchina:** per registrare dichiarazioni di bunkeraggio, consumo e ROB con i relativi riferimenti di evidenza (13.03 §5).
- **Comandante:** per seguire le dichiarazioni di combustibile accanto ai riepiloghi di mezzogiorno rivisti dal Comandante (13.03 §2).

## Prima di iniziare
- Nella barra di contesto in alto seleziona la **società e la nave corrette**. Se non è selezionata alcuna nave, la pagina mostra "Seleziona una nave".
- La lettura richiede `operations.fuel.read`. Creare e salvare richiede `operations.fuel.write` e i diritti di modifica su questa nave.
- Gli orari devono includere data, secondi e Z o uno scostamento UTC esplicito; un’ora locale senza fuso orario non è accettata.

## Passo dopo passo
1. Scegli la nave. **Record del combustibile e del ROB** elenca i record restituiti. Usa **Filtra la pagina restituita** per restringere solo questa pagina.
2. Leggi **Verifica di continuità ROB** sotto l’elenco. Mostra, per ogni tipo di combustibile dichiarato, il bilancio (prima + ricevuto − consumato − dopo), **Continuità con il record precedente** e **Segnalazioni di revisione**.
3. Leggi **Riepiloghi combustibile e ROB dei Noon report rivisti dal comandante** come contesto. Sono mostrati come testo dichiarato e non vengono copiati nel registro.
4. Premi **Nuovo record di bozza**. Compila **Contesto del record e della consegna**, **Dichiarazioni delle quantità e del ROB** e **Proprietà del combustibile e record del campione**. Per ogni campo mantieni un valore dichiarato, **Mancante** o **Sconosciuto**.
5. Premi **Salva bozza non verificata**. Ogni salvataggio aggiunge una revisione a **Cronologia delle revisioni registrate**.
6. Per partire da un rapporto di mezzogiorno o da un giornale di macchina salvato, usa **Apri il record combustibile dalla fonte salvata** su quella pagina. La **Dichiarazione giornaliera collegata** resta qui in sola lettura.

## Cosa significano gli stati
- **Draft declaration / Unverified draft record:** una persona ha dichiarato i valori; nulla è verificato.
- **Revisione necessaria:** lo stato della revisione di conformità di ogni record; la conformità non è stata valutata.
- **In attesa; verifica non accertata:** lo stato della revisione di verifica; non esiste alcuna verifica.
- **Nessuna discrepanza rilevata:** i numeri dichiarati tornano. Non è una verifica.
- **Sconosciuto:** la continuità non può essere giudicata, ad esempio quando manca un valore o la storia più vecchia è al di fuori del periodo valutato. Sconosciuto non significa "Nessuna discrepanza" né "Pronto".
- **Mancante / Sconosciuto:** distinti dallo zero.

## Cosa non si può fare / attenzione
- Revisione umana, approvazione operativa o legale, finalizzazione e acquisizione offline non sono disponibili in questa pagina.
- Il sistema segnala le differenze per una revisione umana; non corregge mai una quantità e non decide la conformità del combustibile.
- Vengono valutati solo i 100 record più recenti. Senza un riferimento di serbatoio, una discrepanza su una nave con più serbatoi è una questione di dati di serbatoio mancanti, non un errore nei dati.
- Le quantità del rapporto di mezzogiorno o del giornale di macchina con un’unità non risolta non vengono mai convertite in valori ricevuti, consumati o di ROB.

## Se qualcosa va storto
- **"The saved source changed.":** i tuoi input vengono conservati; ricarica o scarta esplicitamente prima di salvare di nuovo.
- **"Outcome unknown.":** usa **Riprova il comando operativo esatto**; non digitare di nuovo.
- **"Check the declared values, explicit time offsets and current source permission.":** correggi il campo segnalato oppure lascialo su Mancante o Sconosciuto.
- **Fonte operativa o accesso non disponibile:** usa **Riprova la lettura della fonte**; se l’accesso resta negato, verifica il tuo ruolo e il contesto della nave con il tuo amministratore del tenant (13.03 §10).

## Pagine correlate
