---
key: sources
title: Bozze delle fonti
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Bozze delle fonti conserva le fonti normative e regolamentari su cui si basa la vostra organizzazione (convenzione, bandiera, classe, regola aziendale o locale) come **record di bozza con versione**. Risponde alla domanda: "Su quale revisione di quale fonte ufficiale si basa questo requisito?" Vale l'avviso in alto: "Tenant-local DRAFT · Declared and unverified. Dates and hashes confer no legal authority. Source references are stored as text." (13.03 §8).

## Chi la usa
- **Utente Regulatory / Assurance:** registra la fonte ufficiale con versione e ne indica la data di entrata in vigore e il ciclo di vita (13.03 §8).
- **DPA / HSEQ:** la consulta per vedere a quale revisione della fonte si riferisce un audit o un rilievo (13.03 §4).

## Prima di iniziare
- La visualizzazione richiede la lettura a livello di tenant e la lettura delle bozze normative. Creare e salvare bozze richiede anche la scrittura delle bozze normative; senza di essa il pulsante **Nuova bozza** non viene mostrato.
- Le sottoviste richiedono permessi propri, sempre con ambito di lettura a livello di tenant. **Ciclo di vita e applicabilità** richiede la lettura dell'applicabilità; modificare le regole richiede la scrittura delle regole, richiedere una revisione richiede la revisione delle regole e valutare richiede la valutazione dell'applicabilità; qualifica e attestazione dei fatti richiedono la qualifica normativa o l'attestazione dei fatti più una designazione registrata sul server. **Controlli ed evidenze** richiede la lettura di controlli, evidenze, file e applicabilità; collegare richiede la scrittura dei controlli o delle evidenze con diritti di modifica sulla nave, e l'approvazione richiede l'approvazione dei controlli.
- Tenete a portata di mano il testo ufficiale della fonte e i dettagli di versione/modifica. POSEIDON non scarica né verifica il testo della fonte; siete voi a dichiararlo.
- Il salvataggio richiede una connessione attiva al server. Offline, le vostre modifiche restano nella scheda ma non vengono salvate.

## Passo dopo passo
1. Consultate le bozze delle fonti registrate nell'elenco. Per cercare, digitate in **Search drafts** e premete **Cerca**. Spostatevi tra le pagine con **Precedente** e **Successivo**.
2. Per una nuova fonte premete **Nuova bozza**.
3. In **Draft fields** scegliete prima uno **Stato** per ogni campo: **Mancante**, **Sconosciuto** o **Known (declared)**. Potete inserire un valore solo dopo aver scelto **Known (declared)**. I campi sono **Titolo**, **Autorità dichiarata**, **Riferimento della fonte (solo testo)**, **Dichiarazione della versione / modifica**, **Data di entrata in vigore dichiarata**, **Dichiarazione / provenienza** e **Fonte osservata il (con offset UTC)**.
4. Premete **Salva bozza**. In caso di successo vedete "Draft saved. Unverified."; aprite il record con **Open saved draft and history**.
5. Aprite una bozza esistente dall'elenco, modificatela e salvate di nuovo. Ogni salvataggio crea una nuova **Revisione**; le revisioni precedenti restano in **Cronologia immutabile** con il loro contenuto e l'**Hash di integrità (non costituisce autorità)**.
6. Per passare alle regole di applicabilità o alla catena controllo-evidenza usate **Ciclo di vita e applicabilità** o **Controlli ed evidenze** sopra l'elenco. Premete **Back to register** per tornare.

## Cosa significano gli stati
- **BOZZA:** lo stato memorizzato mostrato nell'elenco e nell'intestazione del record. Ogni record di fonte in questa pagina è una bozza; non è un'adozione ufficiale né una decisione di entrata in vigore.
- **Mancante:** per il campo non è ancora stata inserita alcuna informazione. I passaggi successivi (per esempio una valutazione di applicabilità) sono bloccati finché mancano dati obbligatori.
- **Sconosciuto:** l'informazione è stata cercata ma non è nota. Il sistema non legge mai Sconosciuto come "no" o "non applicabile"; quando dati critici sono sconosciuti il risultato resta in revisione (REVIEW_REQUIRED).
- **Known (declared):** avete dichiarato voi il valore. Una dichiarazione non è un'informazione verificata.
- **Hash di integrità (non costituisce autorità):** mostra che il contenuto di una revisione non è cambiato; non è una prova di validità legale.

## Cosa non si può fare / attenzione
- Non potete modificare una revisione salvata. Quando la fonte cambia, salvate una nuova revisione; i requisiti restano legati alla revisione precedente e la cronologia viene conservata (13.03 §1).
- Non date per scontato che le fonti di regole di classe, bandiera, società e locali si sostituiscano a vicenda; registrate ciascuna come fonte a sé (13.03 §8).
- Questa pagina non ammette ufficialmente una fonte e non fornisce a nessuno un risultato di conformità, permesso o prontezza. L'ammissione della fonte e la revisione indipendente sono di competenza umana.
- Le date di entrata in vigore e gli hash non conferiscono alcuna autorità legale.

## Se qualcosa va storto
- **Permission denied.** Controllate il vostro ruolo e l'ambito del tenant; se necessario rivolgetevi al Tenant Admin (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Qualcuno ha modificato il record prima di voi. Aprite la revisione corrente con **Ricarica la bozza salvata** e ripetete la modifica.
- **Check field states, bounds, dates and source revision.** Il valore di un campo o il formato di una data non è valido.
- **Service unavailable. Your draft is retained.** Quando la connessione ritorna, reinviate lo stesso comando con **Retry frozen command**.
- **Sign in to continue; retain your draft.** La sessione è terminata; usate il collegamento **Accedi**.
- Se un errore dell'applicazione persiste, segnalate al supporto il correlation ID, la schermata, l'ora e i passaggi eseguiti (13.03 §10).

## Pagine correlate
