---
key: engine-daily-log
title: Giornale di macchina
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il giornale di macchina conserva le **registrazioni delle guardie in macchina** della nave selezionata come bozze, con la versione della fonte registrata. Risponde alla domanda: "Che cosa ha dichiarato la guardia in macchina riguardo a stato e ore dei motori, consumo di combustibile e olio lubrificante, acqua dolce, misure critiche, allarmi, difetti, bunkeraggio, sentina e controlli ambientali, e il Direttore di macchina ha registrato una revisione di questa esatta revisione?" Una voce salvata è una dichiarazione non verificata. I dati in bozza non vengono trasformati in conclusioni su emissioni, garanzia del combustibile o prontezza.

## Chi la usa
- **Macchinista di guardia (Crew / OOW / Engineer):** per tenere la registrazione della guardia in macchina in tempo reale e in ordine cronologico (13.03 §6).
- **Direttore di macchina:** per esaminare il giornale salvato, collegarlo a ordini di lavoro e difetti e registrare la revisione come atto umano (13.03 §5). Solo una nomina con l’incarico di Direttore di macchina può registrare una revisione del giornale di macchina.

## Prima di iniziare
- Nella barra di contesto in alto seleziona la **società e la nave corrette**.
- La lettura richiede `daily.engine.read`; il salvataggio richiede `daily.engine.write` e i diritti di modifica su questa nave. La registrazione di una revisione richiede `daily.engine.write` con diritti di modifica su questa nave, `daily.engine.review`, oltre a una nomina attuale come Direttore di macchina per questo esatto record.
- Il collegamento di ordini di lavoro o difetti richiede anche l’accesso in lettura a tali record.

## Passo dopo passo
1. Scegli la nave. **Bozze registrate** elenca ogni giornale per data e revisione con "Bozza · revisione necessaria".
2. Premi **Nuova bozza**, oppure apri un giornale elencato per **Modifica la bozza registrata**.
3. Compila **Contesto della guardia macchina**, **Stato e ore dei motori**, **Consumo e produzione**, **Misure, allarmi e difetti** e **Contesto dei lavori e ambientale**. Per ogni campo scegli **Noto**, **Sconosciuto** o **Mancante**.
4. Premi **Salva bozza**. Ogni salvataggio aggiunge una revisione a **Cronologia delle revisioni registrate**.
5. Su un giornale salvato usa **Ordini di lavoro e difetti**: scegli un **Tipo di record** e un **Record**, poi **Collega**. Un collegamento fissa una revisione esatta; **Rimuovi collegamento** lo rimuove.
6. Se necessario apri **Apri il record combustibile dalla fonte salvata** o **Apri il record di fermo dalla fonte salvata**. Quantità e unità non vengono copiate.
7. Il Direttore di macchina apre **Registrazione della revisione umana**, sceglie una **Nomina umana autorizzata** e una **Voce di revisione**, scrive un **Motivo della revisione** e preme **Registra voce di revisione**.

## Cosa significano gli stati
- **Unverified declaration / Noto / Sconosciuto / Mancante:** i valori dichiarati e il loro stato dei dati. Sconosciuto non significa "No", "Pronto" né "Non applicabile".
- **In attesa · revisione non registrata:** per questa revisione non esiste alcuna revisione umana.
- **Metadati umani registrati · non valutati:** per questa revisione esiste una voce di revisione del Direttore di macchina. Ciò non stabilisce una verifica né una firma del Direttore di macchina.
- **Current revision / Newer revision exists:** se un ordine di lavoro o difetto collegato è cambiato da quando è stato collegato. Un collegamento indica solo la provenienza; non verifica, non chiude e non approva quel record.
- **ricerca non disponibile — SCONOSCIUTO:** il tipo di record collegato non può essere letto con il tuo accesso.

## Cosa non si può fare / attenzione
- Il sistema non esamina né finalizza mai il giornale. La revisione è un atto umano del Direttore di macchina nominato (13.03 §5).
- Chi ha redatto una qualsiasi revisione del giornale non può esaminarlo. All’autore non viene proposta alcuna nomina e la pagina indica "You authored this log revision; an independent reviewer must record the review."; anche il server rifiuta tale revisione per la separazione dei compiti.
- Mantieni le voci in tempo reale e in ordine cronologico; non modificare il record già esaminato di un’altra persona (13.03 §6).
- Collegare qui un difetto non registra una restrizione e non lo chiude; fallo nella pagina del difetto o dell’ordine di lavoro (13.03 §5).
- I collegamenti richiedono una connessione online attiva; offline non viene collegato nulla. I conflitti offline non vengono mai uniti automaticamente.

## Se qualcosa va storto
- **"The log entry changed. Reload it before changing links.":** ricarica, poi collega di nuovo.
- **"Save outcome unknown":** usa **Riprova il comando salvato esatto**; non digitare di nuovo.
- **"Human conflict resolution required":** apri **Esamina conflitto** o il Centro conflitti e scegli esplicitamente (13.03 §10).
- **Nessuna nomina disponibile o accesso negato:** verifica il tuo ruolo e il contesto della nave con il tuo amministratore del tenant (13.03 §10).

## Pagine correlate
