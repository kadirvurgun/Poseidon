---
key: noon-report
title: Rapporto di mezzogiorno
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: [1]
reviewed: false
---
## A cosa serve questa pagina
La pagina del rapporto di mezzogiorno conserva le **dichiarazioni giornaliere di mezzogiorno** della nave selezionata come bozze, con la versione della fonte registrata. Risponde alla domanda: "Che cosa abbiamo dichiarato per ora del rapporto, viaggio, posizione, distanza, meteo, carico, ore delle macchine, combustibile e ROB, e il Comandante ha registrato una revisione di questa esatta revisione?" Una bozza salvata è una dichiarazione non verificata. Salvarla non stabilisce un’approvazione del Comandante o del Direttore di macchina, e la pagina non ne trae alcuna conclusione su emissioni, garanzia del combustibile o prontezza.

## Chi la usa
- **Ufficiale che prepara il rapporto (Crew / OOW):** per compilare e salvare la bozza di mezzogiorno per la nave e la data corrette (13.03 §6).
- **Comandante:** per esaminare il rapporto salvato e registrare tale revisione come atto umano (13.03 §2). Solo una nomina con l’incarico di Comandante può registrare una revisione del rapporto di mezzogiorno.

## Prima di iniziare
- Nella barra di contesto in alto seleziona la **società e la nave corrette**. Se non è selezionata alcuna nave, la pagina mostra "Seleziona una nave".
- La lettura richiede `daily.noon.read`. Creare e salvare bozze richiede `daily.noon.write` e i diritti di modifica su questa nave; senza di essi vedi "Accesso in sola lettura. Modifica e salvataggio non sono autorizzati."
- La registrazione di una revisione richiede `daily.noon.write` con diritti di modifica su questa nave, `daily.noon.review`, oltre a una nomina attuale come Comandante per questo esatto record.

## Passo dopo passo
1. Scegli la nave. L’elenco **Bozze registrate** mostra ogni rapporto per data e revisione con il badge "Bozza · revisione necessaria". Usa **Bozze precedenti** / **Altre bozze** per sfogliare le pagine.
2. Premi **Nuova bozza** per aprire **Nuova bozza giornaliera**, oppure apri un rapporto elencato per **Modifica la bozza registrata**.
3. Compila i gruppi (**Rapporto e viaggio**, **Posizione e attività**, **Meteo e carico**, **Macchinari, combustibile e ROB**). Per ogni campo scegli uno **Stato del dato**: **Noto**, **Sconosciuto** o **Mancante**. Le bozze incomplete possono essere salvate.
4. Premi **Salva bozza**. La pagina conferma "Draft saved. Human review and operational approval remain separate." Ogni salvataggio aggiunge una revisione in **Cronologia delle revisioni registrate**.
5. Su un rapporto salvato il Comandante apre **Registrazione della revisione umana**, sceglie una **Nomina umana autorizzata**, una **Voce di revisione** ("Registra dichiarazione esaminata (non valutata)" o "Restituisci per correzione") e scrive un **Motivo della revisione**, poi preme **Registra voce di revisione**.
6. Se occorre registrare combustibile o un evento portuale, usa **Usa la fonte operativa salvata** e **Apri il record combustibile dalla fonte salvata** o **Apri l’evento portuale dalla fonte salvata**. Quantità, unità e approvazioni non vengono copiate.

## Cosa significano gli stati
- **Draft · declarations unverified / Unverified declaration:** i valori sono dichiarati da una persona; nessuno li ha verificati.
- **Noto / Sconosciuto / Mancante:** lo stato dei dati di un campo. Sconosciuto non significa "No", "Pronto" né "Non applicabile", e Mancante non è zero.
- **In attesa · revisione non registrata:** per questa revisione non è stata registrata alcuna revisione umana.
- **Human metadata recorded · unassessed / Recorded · unassessed:** per la revisione mostrata esiste una voce di revisione del Comandante. La registrazione della revisione non stabilisce una verifica, un invio né un’approvazione di sicurezza del Comandante.
- **Historical revision:** la voce di revisione appartiene a una revisione precedente e non esamina la bozza salvata attuale.

## Cosa non si può fare / attenzione
- Il sistema non esamina né finalizza mai il rapporto. Registrare una revisione è un atto umano del Comandante nominato (13.03 §2).
- Chi ha redatto una qualsiasi revisione del giornale non può esaminarlo. All’autore non viene proposta alcuna nomina e la pagina indica "You authored this log revision; an independent reviewer must record the review."; anche il server rifiuta tale revisione per la separazione dei compiti. Se il Comandante ha scritto il rapporto, la revisione deve comunque essere registrata da un revisore indipendente con autorità di revisione.
- Un nuovo salvataggio crea una nuova revisione; la revisione umana precedente resta sulla propria revisione. Esamina di nuovo dopo le modifiche.
- Un riferimento digitato come testo libero non può sostituire una ricerca autorizzata; lascia il campo su Mancante o Sconosciuto.
- Offline solo i campi classificati esplicitamente possono andare nella coda cifrata. I campi di revisione, approvazione e verifica sono esclusi.

## Se qualcosa va storto
- **"The record changed. Your inputs are retained.":** qualcuno ha salvato una versione più recente. Scarta esplicitamente e ricarica prima di un nuovo tentativo.
- **"Save outcome unknown":** i campi sono bloccati; usa **Riprova il comando salvato esatto**, non digitare di nuovo.
- **"No current source-authorized human appointment is available for this record.":** verifica il tuo ruolo e il contesto della nave con il tuo amministratore del tenant (13.03 §10).
- **"Human conflict resolution required":** un’acquisizione offline è in conflitto con il server. Apri **Esamina conflitto** o il Centro conflitti e scegli esplicitamente; nulla viene unito automaticamente (13.03 §10).
- **Fonti giornaliere non disponibili o accesso negato:** usa **Riprova la lettura delle fonti**; se il problema persiste, segnala al supporto l’ora e i passaggi eseguiti.

## Pagine correlate
