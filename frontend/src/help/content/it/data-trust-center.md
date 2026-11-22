---
key: data-trust-center
title: Centro di affidabilità dei dati
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il centro di affidabilità dei dati mostra i **segnali di qualità dei dati** per controlli, evidenze e campi del profilo della nave selezionata. Risponde alla domanda: "Quali record di questa nave sono mancanti, non aggiornati, in conflitto, non verificati od orfani, e quali informazioni su fonte e aggiornamento lo indicano?" Un segnale di affidabilità non è una verifica del record sottostante. La pagina non contrassegna mai nulla come pronto.

## Chi la usa
- **Utente Regulatory / Assurance:** per evitare di lasciare record orfani o incompleti nella catena Requisito → Controllo → Evidenza → Verifica (13.03 §8).
- **DPA:** per seguire le lacune di evidenze e verifiche (13.03 §4).
- **Sovrintendente tecnico / Comandante:** per vedere i segnali che mantengono una valutazione di prontezza su revisione necessaria (13.03 §2, §5).

## Prima di iniziare
- Nella barra di contesto in alto controlla che sia selezionata la **società corretta** (13.03 §1).
- La pagina richiede l’accesso in lettura a controlli, evidenze, file, applicabilità e bozze normative del tenant, tutti insieme, con ambito di lettura esteso all’intero tenant.
- Se non è selezionata alcuna nave, la pagina mostra prima l’elenco "Scegli una nave".

## Passo dopo passo
1. Scegli la nave dall’elenco "Scegli una nave".
2. In **Sintesi** leggi lo stato complessivo ("Revisione necessaria" o "Nessun segnale rilevante") e i conteggi **Oggetti per segnale**.
3. Nella tabella **Controlli ed evidenze** controlla per ogni oggetto le colonne "Fonte / provenienza", "Aggiornamento", "Verifica" e "Segnali".
4. Annota il codice motivo accanto al badge di un segnale ed esamina il record nella sua pagina.
5. Vedi i segnali dei campi del profilo nella tabella **Campi del profilo nave**.
6. Usa **Precedente** / **Successivo** per altri oggetti.
7. Se compare "Questi segnali potrebbero non essere aggiornati.", premi **Ricarica**.

## Cosa significano gli stati
- **Missing / Stale / Conflicting / Unverified / Orphan:** il tipo di segnale su un oggetto. Un segnale è un indicatore di qualità; non dimostra che il record sia giusto o sbagliato.
- **Nessun segnale:** su questo oggetto non è stato trovato alcun segnale. Ciò non significa che il record sia verificato.
- **Revisione necessaria:** esiste un segnale rilevante. Finché esistono segnali rilevanti, la prontezza resta su revisione necessaria.
- **Nessun segnale rilevante:** non è una dichiarazione di prontezza.
- **Verificato da una persona:** una persona autorizzata ha verificato l’evidenza. Questo deriva dal record dell’evidenza stessa; il centro non verifica nulla.
- **Not verified / Verification expired / Verification rejected:** lo stato di verifica dell’evidenza.
- **Non applicabile ai controlli:** mostrato nelle righe dei controlli; lo stato di verifica riguarda le righe delle evidenze.
- **Observation time unknown / No freshness policy / Future-dated:** non è stato possibile stabilire l’aggiornamento oppure la data è nel futuro. Nessuno di questi significa "attuale".

## Cosa non si può fare / attenzione
- Da questa pagina i record non vengono corretti, verificati né chiusi; ciò avviene nella pagina del record da parte di una persona autorizzata.
- Se sono stati valutati solo i primi oggetti, la sintesi resta su revisione necessaria.
- Gli oggetti che non puoi leggere vengono contati ma non mostrati, e la sintesi resta quindi su Revisione necessaria. Un record che non puoi vedere non significa che non ci sia alcun problema.
- Offline i segnali di affidabilità dei dati non possono essere aggiornati.

## Se qualcosa va storto
- **Non sei autorizzato a leggere i segnali di affidabilità dei dati:** controlla il tuo ruolo e il contesto della nave; se necessario contatta il tuo amministratore del tenant (13.03 §10).
- **Il record non può essere verificato:** controlla l’evidenza e la sua provenienza; sottoponilo al verificatore autorizzato.
- **Segnale inatteso:** sottoponi il codice motivo al responsabile del controllo o a un esperto della materia.
- **I segnali di affidabilità dei dati non sono disponibili:** usa "Riprova"; se il problema persiste, segnalalo al supporto con uno screenshot, l’ora e i passaggi eseguiti.

## Pagine correlate
