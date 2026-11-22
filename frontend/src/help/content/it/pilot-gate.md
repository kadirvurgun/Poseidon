---
key: pilot-gate
title: Revisione go/no-go del pilota
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina di revisione go/no-go del pilota mostra in un unico punto lo stato dei gate richiesti per un pilota, il numero di P0 aperti e la prontezza del supporto, e registra la decisione go/no-go del Product Owner designato. Risponde alla domanda: "Ogni gate del pilota è stato soddisfatto con evidenze, ed è stata registrata una decisione umana?" Vale l'avviso in alto: "Solo revisione consultiva. Gate sconosciuti o rinviati e un P0 sconosciuto o aperto raccomandano NO-GO. Solo il Product Owner nominato registra una decisione."

## Chi la usa
- **Product Owner:** legge il risultato della revisione e registra la decisione go/no-go con una motivazione. Per decidere serve una nomina vigente di Product Owner.
- **Supporto / Tenant Admin:** contrassegna i punti di prontezza del supporto e della reperibilità con un riferimento di evidenza.
- 13.03 non definisce un ruolo separato per questa pagina; si applicano i principi di amministrazione del tenant (privilegio minimo, pubblicazione controllata) (13.03 §9).

## Prima di iniziare
- La visualizzazione richiede la lettura a livello di tenant e la lettura dei gate del pilota. Contrassegnare un punto di supporto richiede il permesso di registrazione del supporto; decidere richiede il permesso di decisione più una nomina vigente di Product Owner predisposta sul server. Il solo permesso non basta.
- Solo un account umano può contrassegnare il supporto o decidere; la nomina deve corrispondere al vostro ruolo attivo ed essere nel suo periodo di validità.
- Gli input dei gate provengono da altre pagine; per esempio i gate UAT nave e UAT rimorchiatore sono calcolati dalle esecuzioni attestate nella pagina **Registri UAT per ruolo**.
- La registrazione richiede una connessione attiva; offline è disattivata.

## Passo dopo passo
1. In **Revisione del gate pilota** leggete la raccomandazione (**Raccomandazione consultiva**), il numero di **P0 aperti** e la tabella dei gate: **Gate**, **Stato**, **Fonte**, **Motivi**.
2. In **Prontezza di supporto e reperibilità** scegliete uno **Stato** per ogni punto, inserite un **Riferimento dell’evidenza** per **Pronto**, scrivete una **Nota** e premete **Contrassegna**.
3. Se siete il Product Owner, scegliete la decisione in **Registra la decisione go/no-go** (**NO-GO**, **GO condizionato**, **GO**), scegliete la vostra nomina di Product Owner in **Nomina** e scrivete la **Motivazione**. Per **GO condizionato** compilate **Condizioni (una per riga)**.
4. Premete **Registra**. La decisione compare in **Decisioni del Product Owner** con il suo numero di sequenza.
5. Premete **Aggiorna** per ricaricare i dati.

## Cosa significano gli stati
- **Soglia di evidenza raggiunta (PASS):** la fonte del gate raggiunge la soglia di evidenza. Non è un'approvazione; decide una persona.
- **Non superato (FAIL)**, **Sconosciuto (UNKNOWN)**, **Rinviato (DEFERRED):** il gate non è soddisfatto. Un gate sconosciuto o rinviato non conta mai come superato e raccomanda NO-GO.
- **NO-GO raccomandato:** almeno un gate non è PASS, oppure i P0 aperti sono sconosciuti o superiori a zero.
- **Idoneo al GO; decisione umana in attesa:** ogni gate è PASS e i P0 aperti sono zero. Il sistema non decide mai il GO; solo il Product Owner lo registra.
- **Non contrassegnato / Pronto / Non pronto:** l'ultimo stato contrassegnato di un punto di supporto.
- **Attuale / Obsoleta: revisione cambiata:** una decisione è legata alla revisione del momento in cui è stata presa. Se la revisione è cambiata in seguito, la decisione appare obsoleta e va riconsiderata.

## Cosa non si può fare / attenzione
- "GO è possibile solo quando tutti i gate sono PASS e i P0 aperti sono zero." Altrimenti l'opzione GO è disattivata.
- Il GO condizionato richiede lo stesso stato del GO (ogni gate PASS, P0 aperti noti e pari a zero) e almeno una condizione. Non può essere registrato finché un gate è non superato, sconosciuto o rinviato, o finché un P0 è aperto o sconosciuto. Il NO-GO è sempre registrabile, senza condizioni.
- Il sistema, l'IA o l'automazione non prendono mai la decisione sul pilota e non inventano mai gate o firme di approvazione. I gate e le decisioni del pilota sono record umani.
- Non potete modificare una decisione registrata; una nuova decisione viene aggiunta con un nuovo numero di sequenza.

## Se qualcosa va storto
- **Non hai una nomina vigente di Product Owner. Le decisioni sono in sola lettura.** Le nomine sono predisposte sul server; contattate il Tenant Admin (13.03 §10).
- **Non hai accesso a questi record.** Controllate il vostro ruolo e l'ambito del tenant.
- **Il record è cambiato o è già stato usato. Ricarica.** La revisione è cambiata mentre stavate decidendo; aprite la revisione attuale con **Ricarica**. Questo messaggio compare anche quando gli stati dei gate sono cambiati e la decisione non è più consentita.
- **L’esito è incerto. Riprova invia lo stesso comando.** Usate **Riprova** per reinviare lo stesso comando in modo sicuro.
- **Rifiutato dalle regole.** Un gate è rinviato.
- **Non consentito.** La vostra nomina non è vigente oppure il vostro account non è un account umano.
- **Il servizio non è disponibile. Riprova.** Se il problema persiste, segnalate al supporto il correlation ID, l'ora e i passaggi eseguiti (13.03 §10).

## Pagine correlate
