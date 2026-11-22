---
key: industry-assurance
title: Assurance di settore (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Assurance di settore importa da una cartella di lavoro i risultati delle ispezioni SIRE 2.0 di una nave cisterna, sottopone le modifiche a revisione umana, collega i rilievi alle azioni di sicurezza e registra le evidenze sul campo. Risponde alla domanda: "Quali rilievi di questa ispezione sono aperti, quali hanno un’azione collegata e dove si trova l’evidenza?" Le importazioni sono bozze: ogni voce modificata viene accettata da una persona diversa. I rilievi restano aperti e non verificati; questa pagina non li chiude né li verifica mai. La catena è Rilievo → Azione → Verifica, e verifica ed efficacia sono registrazioni umane separate sull’azione in Eventi di sicurezza e quasi incidenti (13.03 §4, §8).

## Chi la usa
- **DPA / HSEQ:** collega i rilievi delle ispezioni alle azioni e segue la catena Rilievo → Azione → Verifica (13.03 §4).
- **Utente Regulatory / Assurance:** gestisce le mappature delle colonne e prepara o esamina le importazioni (13.03 §8).
- **Comandante e personale di bordo:** registrano le evidenze sul campo (foto) per un rilievo.

## Prima di iniziare
- Scegli una nave. SIRE 2.0 si applica solo alle navi cisterna. Se il tipo di nave non è dichiarato nel profilo della nave, le importazioni restano su **revisione richiesta**.
- La lettura richiede il permesso di lettura delle ispezioni. Importare, accettare un’importazione, scrivere evidenze, collegare rilievi e scrivere mappature sono permessi separati. Il collegamento di un’azione richiede anche il permesso di lettura degli eventi di sicurezza; la registrazione di evidenze richiede anche la lettura dei file.
- Lo schema delle colonne è sintetico; il formato OCIMF con licenza non viene utilizzato.
- Offline vengono messe in coda su questo dispositivo solo le acquisizioni di foto; importazioni e decisioni richiedono una connessione.

## Passo dopo passo
1. **Scegli una nave** e controlla la riga **Applicabilità**.
2. Se necessario, in **Mappature delle colonne** inserisci una **Chiave di mappatura**, scrivi una riga per colonna (intestazione=campo) e premi **Salva come nuova versione**.
3. In **Prepara importazione** scegli **Versione di mappatura**, **Riferimento ispezione**, **Data ispezione** e **Cartella di lavoro (.xlsx)**, poi premi **Invia in revisione**.
4. Un’altra persona, in **Revisione dell’importazione**, sceglie **Accetta** o **Mantieni l’attuale** per ogni voce modificata, scrive un **Motivo** di almeno 10 caratteri e preme **Accetta importazione** o **Respingi importazione**.
5. Apri un’ispezione dall’elenco **Ispezioni**. Esamina **Voci** e **Rilievi**.
6. Per collegare un rilievo a un’azione inserisci l’**ID dell’azione di sicurezza** e premi **Collega azione**.
7. Per un’evidenza sul campo inserisci **Supporto**, **ID del file pulito**, **Revisione del file** e **Checksum del file (SHA-256)** e premi **Registra evidenza sul campo**. Offline il pulsante diventa **Salva su questo dispositivo**; quando sei online invialo con **Invia ora**.

## Cosa significano gli stati
- **Tanker: applicable / Not applicable / Ship type unknown: review required:** con un tipo di nave sconosciuto il risultato resta **Revisione necessaria**; non viene mai considerato non applicabile.
- **Awaiting review / Accepted / Rejected:** lo stato dell’importazione.
- **New / Changed / Unchanged / Not in this import:** la voce confrontata con l’importazione accettata in precedenza. Una voce non presente in questa importazione è **Conservato (mai eliminato)**.
- **Negative observation / Positive observation / No observation / Unknown:** l’osservazione su una voce.
- **Open / Unverified:** qui i rilievi sono sempre mostrati come aperti e non verificati.
- **No action linked / Action linked:** se il rilievo è collegato a un’azione di sicurezza. Un’azione collegata non significa che il rilievo sia verificato né che l’azione sia efficace.
- **Captured online / Captured offline:** come è stata acquisita l’evidenza.

## Cosa non si può fare / attenzione
- La persona che ha preparato un’importazione non può esaminarla: "Hai preparato tu questa importazione; deve revisionarla un’altra persona."
- Questa pagina non chiude né verifica i rilievi. Verificato non è Efficace; l’efficacia è una revisione umana separata sull’azione.
- Le evidenze video non sono disponibili finché l’operatore non abilita un tipo di file video sottoposto a scansione.
- Una voce mancante in un’importazione successiva non viene mai eliminata; la storia viene conservata.

## Se qualcosa va storto
- **La richiesta è stata rifiutata:** risolvi i motivi elencati sotto di essa.
- **La registrazione è cambiata nel frattempo. Ricarica e ricontrolla.** Premi **Ricarica**.
- **L’esito è sconosciuto (connessione persa).** Usa **Riprova la stessa richiesta**.
- **Non sei autorizzato a leggere i dati di assurance di settore.** oppure **Non sei autorizzato a farlo.** Controlla il tuo ruolo e il contesto della nave; se necessario contatta l’amministratore del tenant (13.03 §10).
- Se un’evidenza non può essere verificata, controlla l’ID del file, la revisione e il checksum SHA-256; se necessario contatta il verificatore (13.03 §10).

## Pagine correlate
