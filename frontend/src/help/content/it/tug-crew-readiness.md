---
key: tug-crew-readiness
title: Competenza equipaggio rimorchiatore
roles: [Tug Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, crew.read, human.review.read, applicability.read]
related: [tug-readiness, tug-assignment, tug-equipment-readiness]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina mostra, in sola lettura, i controlli di **equipaggio e competenza di rimorchio** per la finestra temporale pianificata di un'assegnazione di rimorchiatore. Risponde alla domanda: "Per l'equipaggio di questa assegnazione, cosa dicono i controlli su tabella minima di sicurezza (MSMD), titoli e competenze, e lavoro/riposo, e da quale fonte provengono?" Il risultato è solo un parere e alimenta la verifica di prontezza; non è un'approvazione di idoneità né una decisione GO.

## Chi la usa
- **Comandante del rimorchiatore:** per valutare insieme MSMD, titoli, competenze, familiarizzazione e lavoro/riposo ai fini della prontezza dell'equipaggio (13.03 §3).
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per seguire i blocchi relativi all'equipaggio di un'assegnazione (13.03 §7).

## Prima di iniziare
- Aprite la pagina da un'assegnazione: in **Assegnazione e conflitti**, usate il collegamento **Equipaggio e competenza di rimorchio** sotto una proposta. Senza un'assegnazione selezionata la pagina mostra **Seleziona un'assegnazione dall'elenco del lavoro.**
- Oltre all'accesso in lettura ai rimorchiatori servono gli accessi in lettura a equipaggio, revisione umana e applicabilità; altrimenti la fonte protetta dell'equipaggio non può essere letta.
- L'assegnazione richiede una finestra temporale valida, attuale o futura; altrimenti il risultato richiede una revisione.

## Passo dopo passo
1. Leggete il risultato complessivo sulla riga **Stato**.
2. Sulla riga **Valutatore di idoneità dell'equipaggio**, verificate se il valutatore è collegato e qual è la sua fonte.
3. Nella tabella dei controlli, leggete **Categoria**, **Risultato** e **Codice motivo** di ogni riga.
4. Se c'è un blocco o un motivo di revisione, passate il record dell'equipaggio al suo responsabile; poi tornate a **Verifica di prontezza operativa**.

## Cosa significano gli stati
- **Ready / Conditional / Not ready / Review required:** la valutazione consultiva. Pronto non è una decisione GO.
- **Non collegato — tutti i criteri specifici del rimorchiatore restano in revisione.:** il valutatore non è collegato; i criteri specifici del rimorchiatore restano da rivedere e non vengono conteggiati come pronti.
- **Pass / Fail / Unknown:** il risultato di un singolo controllo. Una lacuna critica non viene mai mostrata in verde.
- **Sconosciuto:** la fonte manca o non è stato possibile leggerla. Un controllo sconosciuto richiede revisione; non sostituisce mai "Pronto" o "No".

## Cosa non si può fare / attenzione
- In questa pagina non si modifica alcun record dell'equipaggio e non si approva alcuna idoneità.
- Il risultato è un parere; la decisione operativa resta al Comandante del rimorchiatore e un'assegnazione da terra non prevale mai su di essa (13.03 §3, §7).
- Non modificate il record verificato di un'altra persona; le correzioni spettano al responsabile del record (13.03 §6).

## Se qualcosa va storto
- **"You do not have access to this record.":** controllate il vostro ruolo, il rimorchiatore e il contesto dell'oggetto; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).
- **"The service is unavailable.":** premete **Riprova**.
- **Prontezza inattesa:** annotate i codici motivo ed esaminateli con il responsabile del controllo o con un esperto della materia (13.03 §10).
- **Il record non può essere verificato:** controllate la fonte del titolo e delle evidenze; contattate il verificatore (13.03 §10).

## Pagine correlate
