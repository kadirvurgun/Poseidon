---
key: document-compare
title: Confronto revisioni documento
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina mostra **due revisioni di un documento controllato affiancate, in sola lettura**, per la nave selezionata. Risponde alla domanda: "Che cosa differisce tra queste due revisioni a livello di file e di metadati, e qual è la storia di pubblicazione, verifica e custodia di ciascuna?" La pagina mostra le differenze; non approva, non pubblica e non giudica le modifiche. Non mette in vigore una revisione e non le fa sostituire una precedente. Gli originali delle evidenze non vengono mai modificati silenziosamente; quando una nuova revisione sostituisce una precedente, lo stato di pubblicazione della revisione precedente diventa Superseded e l’evento compare nella sua storia di custodia (13.03 §1).

## Chi la usa
- **Comandante / Direttore di macchina / Equipaggio:** per confrontare la revisione in vigore con quella precedente e registrare una presa visione di lettura (13.03 §2, §5, §6).
- **DPA / Utente Regulatory e assurance:** per esaminare la storia delle revisioni e i record di verifica (13.03 §4, §8).

## Prima di iniziare
- Se non è selezionata alcuna nave, la pagina indica **Scegli una nave nel tuo ambito.** ed elenca le navi. Seleziona la nave corretta.
- Per vedere la pagina ti serve l’accesso in lettura ai file. I contenuti riservati e i documenti riservati ISPS richiedono inoltre un proprio permesso.
- Il caricamento dei contenuti e la registrazione di una presa visione funzionano solo online.

## Passo dopo passo
1. Apri un documento dall’elenco **Documenti controllati**. Le etichette di classificazione e **Riservato ISPS** sono mostrate sulla riga.
2. In **Scegli due revisioni** seleziona le revisioni **Sinistra (base)** e **Destra (esaminata)**. La pagina propone inizialmente la revisione più recente a destra e quella precedente a sinistra.
3. Premi **Confronta**. Usa **Scambia lati** per invertirle.
4. Leggi la dichiarazione sui byte e la tabella **Confronto dei metadati**: **Tipo di file**, **Dimensione (byte)**, **SHA-256 del contenuto**, **Acquisizione**, **Pubblicazione**, **Verifica** e i campi temporali.
5. Esamina lo **Storico di approvazione e custodia** su ciascun lato.
6. Per vedere il contenuto premi **Carica contenuto (accesso registrato)**. Il contenuto è mostrato con una filigrana di attribuzione.
7. Dopo **Load content**, se hai letto la revisione in vigore, spunta **Ho letto questa revisione e ho potuto accedere al suo contenuto.** e premi **Registra presa visione**.

## Cosa significano gli stati
- **Unpublished / Effective / Superseded:** lo stato di pubblicazione della revisione. Una revisione non in vigore mostra **Non in vigore operativo**.
- **Unverified / Verified valid / Verified invalid / Verification inconclusive:** il record di verifica della revisione. Non verificato non significa "valido".
- **Uguale / Changed / Not comparable:** il risultato di una riga dei metadati. Non confrontabile non significa "Uguale".
- **Byte-identical files:** solo byte identici; non è una revisione del contenuto.
- **Files differ at byte level:** quale regola è cambiata, e con quale rilevanza, richiede l’esame di una persona.
- **Contenuto riservato per il tuo ruolo; solo metadati.** il contenuto non viene aperto per questo ruolo.

## Cosa non si può fare / attenzione
- Da questa pagina non puoi pubblicare, approvare, mettere in vigore o sostituire un documento.
- La pagina non interpreta automaticamente le differenze di contenuto; una persona autorizzata ne decide la rilevanza.
- Una presa visione viene registrata solo per la revisione in vigore, una volta per persona per revisione. Non è un’approvazione né una verifica.
- Una copia stampata non è controllata. Il contenuto non viene memorizzato nella cache né conservato offline.
- Offline il confronto non è aggiornato e non è possibile caricare nuovi contenuti.

## Se qualcosa va storto
- **Questo documento non è disponibile per te:** controlla il contesto della nave e il permesso; se necessario contatta il tuo amministratore del tenant (13.03 §10).
- **Non hai il permesso per questa azione:** controlla il tuo ruolo e il contesto della nave (13.03 §10).
- **Il record è cambiato nel frattempo:** usa **Ricarica** per aprire la versione più recente.
- **Una revisione non può essere verificata:** controlla l’evidenza e la storia della provenienza; parla con un verificatore autorizzato (13.03 §10).
- **La richiesta non è riuscita:** invia al supporto la schermata, l’ora e i passaggi eseguiti (13.03 §10).

## Pagine correlate
