---
key: renewal-workflow
title: Rinnovo del certificato
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Rinnovo del certificato registra passo dopo passo la preparazione del rinnovo di un certificato e il flusso interno: preparazione, domanda, attesa dell'autorità, sostituto rilasciato, verifica e completamento. Risponde alla domanda: "A che punto è il rinnovo di questo certificato, e chi esegue il prossimo passaggio?" È soltanto un record di flusso interno; non stabilisce la validità legale né l'approvazione dell'autorità (13.03 §1, §8).

## Chi la usa
- **Comandante, Direttore di macchina / Sovrintendente tecnico:** preparano la bozza di rinnovo e registrano i requisiti e la domanda.
- **Responsabile del rinnovo assegnato:** fa avanzare il flusso. È un ruolo ricoperto da membri con il permesso di responsabile del rinnovo; senza di esso il flusso non può essere aperto né convalidato.
- **Verificatore e chi completa:** confrontano il sostituto rilasciato con la sua fonte e lo verificano e, come persona diversa, completano il rinnovo (13.03 §1, §8).

## Prima di iniziare
- La visualizzazione richiede la lettura dei certificati. Preparare una bozza richiede la scrittura dei certificati e la scrittura dei rinnovi su quella nave; verificare richiede la verifica dei rinnovi; completare richiede il completamento dei rinnovi.
- Scegliete prima la nave corretta in **Seleziona una nave** (13.03 §1).
- Il certificato da rinnovare deve avere una revisione salvata corrente. Un certificato sostituito non può essere rinnovato.
- I passaggi del flusso richiedono una connessione attiva. Offline vedete "You are offline. Workflow steps are unavailable until the connection returns."

## Passo dopo passo
1. Aprite un **Record di rinnovo** da **Bozze di rinnovo registrate**. Per un nuovo rinnovo usate **Scegli il certificato da rinnovare** per andare al [Centro di gestione certificati](/app/certificate-command-center) e premete **Prepara rinnovo** sul record.
2. Compilate il **Modulo di rinnovo del certificato**: **Data di scadenza del rinnovo dichiarata**, la finestra di visita, **Luogo previsto**, **Ruolo responsabile del rinnovo assegnato** e **Dichiarazione di visita obbligatoria**, **Dichiarazione dei documenti obbligatori**, **Dichiarazione delle azioni obbligatorie**. Lasciate un valore che non conoscete come **Sconosciuto**.
3. Salvate la bozza con **Salva bozza non verificata**.
4. In **Flusso di rinnovo**, scegliete da **Passaggi disponibili** il passaggio aperto al vostro ruolo, per esempio **Conferma requisiti completati**, **Registra domanda** o **Attendi la decisione dell’autorità**. Compilate i campi obbligatori e premete **Registra passaggio**.
5. Quando l'autorità rilascia il nuovo certificato, usate **Registra sostituto rilasciato** e selezionatelo. Il suo documento viene usato come evidenza.
6. Il verificatore usa **Verifica sostituto** e imposta **L’emittente corrisponde**, **Il numero del certificato corrisponde**, **L’oggetto corrisponde**, **Le date corrispondono** e **L’ambito corrisponde** confrontando con la fonte.
7. Una persona diversa completa il rinnovo con **Completa e sostituisci**. Se l'autorità ha rifiutato, usate **Registra rifiuto dell’autorità**; se la preparazione viene abbandonata, usate **Annulla preparazione**.

## Cosa significano gli stati
- **Bozza / In preparazione:** il rinnovo è in preparazione; il modulo può essere modificato.
- **Pronto per la domanda / Domanda registrata / In attesa dell’autorità:** la fase della domanda. La decisione dell'autorità avviene fuori dal sistema.
- **Rilasciato, non verificato:** il sostituto è registrato ma non è ancora stato confrontato con la sua fonte da una persona.
- **Verificato dal revisore:** il verificatore ha registrato il confronto; il rinnovo non è ancora completato.
- **Completato:** il flusso interno è chiuso e il sostituto sostituisce l'originale. Non è una decisione sulla validità legale del certificato.
- **Annullato / Rifiutato:** il flusso si è fermato.
- Un requisito può essere **Completa / Non richiesto / Sconosciuto**. **Sconosciuto** non significa "Non richiesto"; il server non fa avanzare il flusso con un requisito sconosciuto.

## Cosa non si può fare / attenzione
- Chi ha registrato il sostituto rilasciato, o chi ha inserito quel certificato sostitutivo, non può verificarlo. Chi completa deve essere diverso dal verificatore, da chi ha registrato il sostituto rilasciato e dall'autore del certificato sostitutivo.
- Verifica e completamento sono esclusivamente decisioni umane; Completato ≠ Verificato.
- Le dichiarazioni di pianificazione non stabiliscono visite completate, domande approvate, idoneità al viaggio né verifica.
- I collegamenti a un sostituto in bozza non sostituiscono l'originale. Il vecchio certificato si chiude solo tramite il passaggio di completamento con un collegamento di sostituzione tracciabile (13.03 §1).
- Ogni passaggio viene registrato come revisione immutabile; la cronologia resta in **Cronologia delle revisioni registrate**.
- Senza un responsabile assegnato esplicitamente il sistema non ripiega mai su un amministratore.

## Se qualcosa va storto
- **Nessun passaggio disponibile per il tuo ruolo in questo stato.** Il prossimo passaggio spetta a un altro ruolo; controllate il vostro permesso e la vostra designazione e, se necessario, contattate il Tenant Admin (13.03 §10).
- **The renewal changed after it was loaded.** oppure **Questa non è la revisione corrente.** Premete **Ricarica** ed esaminate lo stato attuale.
- **L’esito è sconosciuto. Riprovare invia di nuovo la stessa richiesta.** Usate **Riprova la stessa richiesta**.
- **Rifiutato per separazione dei compiti: questo passaggio deve essere eseguito da un'altra persona. Lei ha registrato, redatto o verificato un passaggio precedente di questo rinnovo, oppure non è ancora registrata alcuna verifica da parte di un'altra persona.** Il record non è cambiato; ricaricare non aiuta. Lasciate il passaggio a un'altra persona autorizzata.
- **Il passaggio non è stato registrato. Controlla i dati e riprova.** Controllate i campi obbligatori e le selezioni di corrispondenza.
- Se il sostituto non può essere verificato, controllate il documento e la provenienza dalla fonte e rimandatelo al verificatore (13.03 §10).

## Pagine correlate
