---
key: audit
title: Cronologia delle azioni
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: [1, 2, 3]
reviewed: false
---
## A cosa serve questa pagina
La pagina Cronologia delle azioni mostra una registrazione **di sola lettura** e attribuita delle modifiche ai record aziendali nel tuo ambito autorizzato. Risponde alla domanda: "Quando è cambiato questo record, con quale azione e sotto quale attore e contesto di ruolo?" Gli eventi di sicurezza sono limitati separatamente e non compaiono qui (13.03 §4, §9).

## Chi la usa
- **DPA / HSEQ:** traccia i record di rilievi, azioni e verifiche (13.03 §4).
- **Utente Regulatory / Assurance:** controlla la cronologia dei record prima di un audit o di una revisione (13.03 §8).
- **Tenant Admin:** verifica le modifiche di appartenenza e di permessi e gli orari di inizio e fine delle deleghe (13.03 §9).

## Prima di iniziare
- La visualizzazione richiede la lettura dell’audit. Senza di essa vedi "È necessaria l’autorizzazione di audit."
- Sono elencati solo i record nell’ambito di tenant e nave per cui sei autorizzato.
- Se cerchi un record specifico, tieni pronto il suo **ID nave** o **ID oggetto**. I link alla cronologia su altre pagine aprono questa pagina già filtrata.

## Passo dopo passo
1. Compila i filtri: **ID nave**, **ID oggetto**, **Tipo di oggetto** e **Azione** (**Tutti**, **Creato**, **Aggiornato**, **Eliminato**).
2. Premi **Filtra**. Usa **Cancella filtri** per reimpostare.
3. Esamina le colonne **Ora**, **Oggetto** e **Azione** in **Record autorizzati**. Spostati tra le pagine con **Prima pagina** e **Successivo**.
4. Premi il link dell’oggetto su una riga per aprire **Dettagli record**. Leggi **Riferimento attore**, **Riferimenti dei ruoli assegnati**, **Capacità attendibili**, **Motivo**, **Metadati**, **Versione risorsa** e **Correlazione**.
5. Se esistono prove di revisione immutabile, premi **Apri la revisione immutabile esatta** per vedere il record com’era. Premi **Chiudi** per chiudere i dettagli.

## Cosa significano gli stati
- **Creato / Aggiornato / Eliminato:** il tipo di azione sul record.
- **Registrato:** i metadati sono stati acquisiti completamente.
- **Historical metadata incomplete / Historical identifier unavailable / Historical context unavailable:** i record più vecchi non hanno conservato queste informazioni. Le informazioni mancanti non vengono mai completate per supposizione.
- **Valori non divulgati:** il valore del campo è nascosto per motivi di privacy; viene mostrato solo il fatto che è cambiato.
- **Le prove della revisione immutabile non sono disponibili per questo record.** Per questo tipo di record non esiste un’istantanea della revisione.

## Cosa non si può fare / attenzione
- La pagina è di sola lettura. Una voce della cronologia delle azioni non può essere modificata né eliminata.
- La presenza di un record nella cronologia non significa che il lavoro sia stato verificato o efficace; Verificato ≠ Efficace (13.03 §4).
- Questa pagina registra le modifiche aziendali. Da sola non costituisce un audit completo delle azioni degli utenti né una prova di conformità alla privacy; gli eventi di sicurezza sono conservati separatamente.
- L’elenco mostra solo l’ambito per cui sei autorizzato. I record al di fuori del tuo ambito non sono elencati; non vedere un record non significa che non sia mai stato modificato.

## Se qualcosa va storto
- **È necessaria l’autorizzazione di audit.** Controlla il tuo ruolo e il tuo ambito; se necessario contatta il Tenant Admin (13.03 §10).
- **Accedi per continuare.** Accedi di nuovo.
- **La cronologia non è disponibile. Verifica i filtri e riprova.** Controlla che gli identificativi siano completi e corretti, poi premi **Riprova**.
- **Nessun record autorizzato corrisponde a questi filtri.** Amplia i filtri.
- Se un errore dell’applicazione persiste, segnalalo al supporto con l’ID di correlazione, la schermata, l’ora e i passaggi eseguiti (13.03 §10).

## Pagine correlate
