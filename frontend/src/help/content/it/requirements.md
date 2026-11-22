---
key: requirements
title: Bozze dei requisiti
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Bozze dei requisiti conserva i **requisiti** derivati da una revisione di fonte come bozze con versione e lega ogni requisito a una revisione esatta della fonte. Risponde alla domanda: "Su quale revisione di quale fonte si basa questo requisito, e qual è il suo ciclo di vita registrato?" Un requisito è il primo anello della catena Requisito → Controllo → Evidenza → Verifica. Nessun record di questa catena dovrebbe restare scollegato (orfano) (13.03 §8).

## Chi la usa
- **Utente Regulatory / Assurance:** lega il requisito alla sua fonte, valuta l'applicabilità con gli input di contesto e mantiene completa la catena (13.03 §8).
- **DPA / HSEQ:** legge la base di un requisito durante il lavoro su rilievi e audit (13.03 §4).

## Prima di iniziare
- La visualizzazione richiede la lettura a livello di tenant e la lettura delle bozze normative; la modifica richiede anche la scrittura delle bozze normative.
- Le sottoviste richiedono permessi propri, sempre con ambito di lettura a livello di tenant. **Ciclo di vita e applicabilità** richiede la lettura dell'applicabilità; modificare le regole richiede la scrittura delle regole, richiedere una revisione richiede la revisione delle regole e valutare richiede la valutazione dell'applicabilità; qualifica e attestazione dei fatti richiedono la qualifica normativa o l'attestazione dei fatti più una designazione registrata sul server. **Controlli ed evidenze** richiede la lettura di controlli, evidenze, file e applicabilità; collegare richiede la scrittura dei controlli o delle evidenze con diritti di modifica sulla nave, e l'approvazione richiede l'approvazione dei controlli.
- Registrate prima la fonte nella pagina **Bozze delle fonti**. Un requisito può essere legato solo a una revisione di fonte esistente.
- Il salvataggio richiede una connessione attiva al server; offline, le modifiche restano conservate nella scheda.

## Passo dopo passo
1. Consultate le bozze dei requisiti nell'elenco; cercate con **Search drafts** e **Cerca**.
2. Per un nuovo requisito premete **Nuova bozza**.
3. In **Draft fields** scegliete uno **Stato** per ogni campo (**Mancante**, **Sconosciuto**, **Known (declared)**) e inserite i valori noti: **Strumento normativo**, **Dichiarazione della versione / modifica**, **Data di entrata in vigore dichiarata**, **Dichiarazione / provenienza**, **Fonte osservata il (con offset UTC)**.
4. In **Immutable source citation** digitate in **Find a source** e premete **Search sources**. Scegliete la fonte in **Source draft**, poi la revisione esatta in **Exact revision**. Controllate il contenuto della revisione scelta con **Selected immutable source content**.
5. Premete **Salva bozza**. Quando il record è aperto, compaiono le sezioni **Ciclo di vita registrato** e **Cronologia immutabile**.
6. Usate **Ciclo di vita e applicabilità** per l'applicabilità e **Controlli ed evidenze** per i collegamenti a controlli ed evidenze. Il **Rapporto delle dichiarazioni critiche orfane** all'interno di **Controlli ed evidenze** elenca le dichiarazioni critiche senza collegamenti a requisiti; collegate quei record a un requisito.

## Cosa significano gli stati
- **Ciclo di vita registrato:** lo stato che il server ricava dalle regole collegate: **Bozza**, **Adottato**, **Efficace in futuro**, **In vigore**, **Sostituito**. Se non può essere letto, viene mostrato **Non disponibile**; il sistema non presume né bozza né in vigore.
- "Le regole collegate registrano stati del ciclo di vita diversi. Il valore mostrato è una precedenza software; è richiesta la revisione di un esperto." In quel caso il valore mostrato non è una decisione; serve la revisione di un esperto.
- **Sconosciuto:** l'informazione non è nota. Quando dati critici sono sconosciuti il risultato di applicabilità resta **Revisione richiesta** (REVIEW_REQUIRED); non viene mai contato come "non applicabile" o "no" (13.03 §8).
- **Revisione di applicabilità richiesta / Collegamento al requisito mancante / Verifica non eseguita:** voci aperte nella catena controllo-evidenza.
- **Completamento sconosciuto · Non verificato · Efficacia sconosciuta:** completamento, verifica ed efficacia sono stati distinti. Un record completato non è verificato, e un record verificato non è efficace.

## Cosa non si può fare / attenzione
- Non potete legare un requisito alla fonte "più recente"; si sceglie sempre una **Exact revision**. Quando la fonte riceve una nuova revisione, il vecchio collegamento resta conservato nella cronologia.
- Un'anteprima di applicabilità non stabilisce conformità, prontezza, permessi né approvazione legale: "PREVIEW / DRAFT. Predictions do not establish compliance, readiness, permits or legal approval."
- Una modifica del ciclo di vita passa per **Richiedi revisione umana**; decidono l'ammissione della fonte e un revisore indipendente qualificato. Il sistema non la chiude mai da solo.
- Non date per scontato che le fonti di regole di classe, bandiera, società e locali si sostituiscano a vicenda (13.03 §8).

## Se qualcosa va storto
- **Source selection unavailable.** Premete **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Aprite la revisione corrente con **Ricarica la bozza salvata**.
- **Check field states, bounds, dates and source revision.** Se avete scelto una fonte, scegliete anche la sua revisione esatta.
- **History unavailable. Editor retained.** Premete **Retry history**.
- Se il ciclo di vita è inatteso, esaminate le regole collegate e le revisioni delle fonti; se necessario contattate il responsabile del controllo o l'esperto della materia (SME) (13.03 §10).
- Senza permesso, controllate il vostro ruolo e l'ambito del tenant e rivolgetevi al Tenant Admin (13.03 §10).

## Pagine correlate
