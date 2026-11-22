---
key: deck-daily-log
title: Giornale di coperta
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il giornale di coperta conserva le **registrazioni delle guardie di coperta** della nave selezionata come bozze, con la versione della fonte registrata. Risponde alla domanda: "Che cosa ha dichiarato la guardia per questa data e questo periodo riguardo a posizione, rotta, velocità, meteo, visibilità, navigazione, attività portuali e di carico, zavorra, lavori di coperta, osservazioni di sicurezza e passaggio di consegne, ed è stata registrata una revisione per questa esatta revisione?" Una voce salvata è una dichiarazione non verificata, non un giornale approvato.

## Chi la usa
- **Ufficiale di guardia (Crew / OOW):** per tenere la registrazione della guardia in tempo reale e in ordine cronologico (13.03 §6).
- **Comandante o Primo ufficiale:** per esaminare il giornale salvato e registrare tale revisione come atto umano (13.03 §2). Solo una nomina con l’incarico di Comandante o Primo ufficiale può registrare una revisione del giornale di coperta.

## Prima di iniziare
- Nella barra di contesto in alto seleziona la **società e la nave corrette**. Se non è selezionata alcuna nave, la pagina mostra "Seleziona una nave".
- La lettura richiede `daily.deck.read`. Creare e salvare richiede `daily.deck.write` e i diritti di modifica su questa nave; altrimenti vedi "Accesso in sola lettura. Modifica e salvataggio non sono autorizzati."
- La registrazione di una revisione richiede `daily.deck.write` con diritti di modifica su questa nave, `daily.deck.review`, oltre a una nomina attuale come Comandante o Primo ufficiale per questo esatto record.

## Passo dopo passo
1. Scegli la nave. **Bozze registrate** elenca ogni giornale per data e revisione con "Bozza · revisione necessaria".
2. Premi **Nuova bozza** per una **Nuova bozza giornaliera**, oppure apri un giornale elencato per **Modifica la bozza registrata**.
3. Compila prima **Contesto di guardia** (**Data del giornale**, **Guardia / periodo**, **Riferimento dell’ufficiale di guardia**). L’elenco degli ufficiali propone solo le persone disponibili per la data del giornale dichiarata.
4. Compila **Navigazione e meteo**, **Attività portuali e di carico** e **Lavori di coperta e passaggio di consegne**. Per ogni campo scegli **Noto**, **Sconosciuto** o **Mancante**.
5. Premi **Salva bozza** durante la guardia o subito dopo. Ogni salvataggio aggiunge una revisione a **Cronologia delle revisioni registrate**; le revisioni precedenti restano invariate.
6. Il Comandante o il Primo ufficiale apre **Registrazione della revisione umana**, sceglie una **Nomina umana autorizzata** e una **Voce di revisione**, scrive un **Motivo della revisione** e preme **Registra voce di revisione**.

## Cosa significano gli stati
- **Draft · declarations unverified / Unverified declaration:** dichiarato da una persona; non verificato.
- **Noto / Sconosciuto / Mancante:** lo stato dei dati del campo. Sconosciuto non significa "No", "Pronto" né "Non applicabile".
- **In attesa · revisione non registrata:** per questa revisione non esiste alcuna revisione umana.
- **Metadati umani registrati · non valutati:** per questa revisione esiste una voce di revisione. Ciò non stabilisce una verifica, un invio né un’approvazione di sicurezza del Comandante.
- **Record reviewed declaration (unassessed) / Return for correction:** i due tipi di voce di revisione che un revisore può registrare.
- **Historical revision:** la revisione riguarda una revisione precedente, non la bozza attuale.

## Cosa non si può fare / attenzione
- Il sistema non esamina né finalizza mai il giornale. La revisione è un atto umano del Comandante o del Primo ufficiale nominato (13.03 §2).
- Chi ha redatto una qualsiasi revisione del giornale non può esaminarlo. All’autore non viene proposta alcuna nomina e la pagina indica "You authored this log revision; an independent reviewer must record the review."; anche il server rifiuta tale revisione per la separazione dei compiti.
- Mantieni le voci in tempo reale e in ordine cronologico; non riscrivere una vecchia guardia perché sembri nuova (13.03 §6).
- Non modificare il record già esaminato di un’altra persona; un salvataggio successivo è una nuova revisione e richiede una nuova revisione umana (13.03 §6).
- L’acquisizione offline riguarda solo i campi classificati esplicitamente. Un conflitto offline non viene mai unito automaticamente; non dare per scontato che prevalga il tuo record (13.03 §6).

## Se qualcosa va storto
- **"The record changed. Your inputs are retained.":** scarta esplicitamente e ricarica prima di un nuovo tentativo.
- **"Save outcome unknown":** usa **Riprova il comando salvato esatto**; non digitare di nuovo i campi.
- **Riferimento dell’ufficiale non disponibile:** lascia il campo su Mancante o Sconosciuto; un nome digitato non può sostituire la ricerca.
- **"Human conflict resolution required":** apri **Esamina conflitto** o il Centro conflitti e scegli esplicitamente (13.03 §10).
- **Nessuna nomina disponibile:** verifica il tuo ruolo e il contesto della nave con il tuo amministratore del tenant (13.03 §10).

## Pagine correlate
