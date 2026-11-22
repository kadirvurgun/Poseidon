---
key: certificate-command-center
title: Centro certificati
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il Centro certificati elenca le **dichiarazioni** di certificato della nave selezionata con i loro stati di calendario dichiarati. Risponde alla domanda: "Quali certificati di questa nave sono vicini alla scadenza o a una finestra di visita, e quali record attendono ancora una revisione umana?" Le date mostrate sono dichiarazioni. La pagina non decide mai che un certificato sia valido o verificato (13.03 §1, §8).

## Chi la usa
- **Comandante, Direttore di macchina / Sovrintendente tecnico:** seguono il calendario dei certificati della nave, inseriscono nuove bozze di certificato e avviano la preparazione dei rinnovi.
- **Utente Regulatory / Assurance:** controlla che le dichiarazioni si basino su una fonte ufficiale e con versione e registra l’attestazione di un Auditor assegnato (13.03 §8).
- Gli utenti con solo accesso in lettura vedono l’elenco e non possono modificarlo.

## Prima di iniziare
- La visualizzazione richiede la lettura dei certificati. L’inserimento di bozze richiede la scrittura dei certificati e l’ambito di modifica su quella nave; l’estrazione di suggerimenti dai documenti richiede la lettura dei file e l’estrazione; la registrazione di un’attestazione umana richiede la scrittura della directory, il permesso di registrazione della verifica e una nomina attuale come **Auditor** per questa nave e questo certificato. Le dichiarazioni protette dell’equipaggio richiedono inoltre la lettura dell’equipaggio, la lettura dei certificati dell’equipaggio, la lettura dell’identità dell’equipaggio e la lettura dei file.
- Scegli prima la nave corretta: premila nell’elenco **Seleziona una nave** (13.03 §1).
- La pagina richiede una lettura online attiva. Offline vedi "Current certificate sources unavailable offline or hidden." e non viene dedotta alcuna autorizzazione offline.

## Passo dopo passo
1. Apri la nave dall’elenco **Seleziona una nave**.
2. Usa **Ambito dei certificati** per scegliere **Certificati della nave** oppure, se disponi del permesso, **Dichiarazioni protette dell’equipaggio**. Restringi l’elenco con **Filtro del calendario dichiarato**.
3. Apri un record premendo la sua riga. Le righe riportano l’etichetta **Dichiarazione di certificato non verificata**.
4. Per un nuovo record premi **Nuova bozza di certificato**. Compila **Nome del documento**, **Base normativa dichiarata**, **Bandiera dichiarata**, la finestra di visita e **Disponibilità dichiarata** dal documento di origine, poi premi **Salva bozza non verificata**.
5. Per ottenere suggerimenti dal documento collegato premi **Estrai dal documento collegato** in **Esamina i fatti del documento**. Confronta ogni suggerimento con la fonte, spunta **Ho verificato questo fatto sulla fonte** e **Ho verificato che questo documento appartenga a questa nave e a questo certificato**, poi premi **Conferma i fatti selezionati e salva la bozza**.
6. Come Auditor assegnato, in **Record di attestazione umana** compila **Seleziona l’assegnazione effettiva** e **Seleziona l’affermazione registrata** e inserisci il motivo, poi premi **Registra attestazione non valutata**.
7. Per avviare un rinnovo usa **Prepara rinnovo** sotto **Lavori relativi al certificato** nel record. Questo ti porta al flusso [Rinnovo del certificato](/app/renewal-workflow).

## Cosa significano gli stati
- **Declared expiry <90 days / <60 days / <30 days / Declared expiry passed:** un’osservazione di calendario basata sulla data di scadenza dichiarata. Non è una decisione sulla validità.
- **Declared survey window open / Beyond due window:** posizione rispetto alla finestra di visita dichiarata.
- **Revisione del calendario necessaria:** una data manca, è ambigua o è in conflitto. Il record non è pronto finché una persona non lo controlla rispetto al documento di origine.
- **Sconosciuto:** il valore non è stato dichiarato. Un valore sconosciuto non significa mai "non mancante" o "conforme"; richiede una revisione.
- **Low confidence · review / Ambiguous · review / Revisione necessaria:** un suggerimento dal documento non ancora confermato da una persona. Un punteggio di affidabilità non è una verifica.
- "Solo questa pagina. Le osservazioni di calendario sono separate dalla qualifica; non sono totali di prontezza a livello di flotta."

## Cosa non si può fare / attenzione
- Salvare una bozza non verifica il certificato. La scrittura del record, la conferma dei suggerimenti dal documento e l’attestazione dell’Auditor sono passaggi separati; Completato ≠ Verificato.
- L’estrazione automatica si limita a suggerire. I valori mancanti restano sconosciuti e solo i fatti che selezioni e correggi aggiornano la bozza.
- Un ruolo di amministratore da solo non consente di registrare un’attestazione umana; sono necessari una nomina attuale e il file di origine.
- Un certificato salvato non viene mai modificato silenziosamente. Ogni salvataggio crea una nuova revisione e le revisioni precedenti restano in **Cronologia delle revisioni registrate** (13.03 §1).
- La pagina non concede alcuna approvazione di classe, bandiera o autorità e non decide l’idoneità al viaggio. Tale decisione spetta alle persone autorizzate.

## Se qualcosa va storto
- **Fonte del certificato o accesso non disponibile. Non si deduce un inventario valido, vuoto o verificato.** Premi **Riprova la lettura della fonte**. Se il problema persiste, controlla il tuo ruolo, la nave e l’ambito dell’oggetto; se necessario contatta il Tenant Admin (13.03 §10).
- **Outcome unknown.** Conserva il comando originale e invia di nuovo la stessa richiesta con **Riprova il comando certificato esatto**.
- **Current source changed.** I tuoi campi locali vengono conservati; usa **Torna ai record**, rileggi il record attuale e ripeti la modifica.
- **Automatic reading is not available for this document:** inserisci i valori manualmente dal documento di origine.
- Se un record non può essere verificato, controlla la sua evidenza e la provenienza della fonte e sottoponilo al verificatore (13.03 §10).

## Pagine correlate
