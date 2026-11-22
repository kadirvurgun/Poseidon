---
key: role-uat
title: Registri UAT per ruolo
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Registri UAT per ruolo registra come **evidenze umane** le sessioni di test di accettazione utente (UAT) con i ruoli di nave e rimorchiatore, i relativi esiti degli scenari e i rilievi, e li confronta con la soglia concordata. Risponde alla domanda: "Le evidenze UAT attestate per ogni ruolo e scenario raggiungono la soglia concordata dalle persone?" Vale l'avviso in alto: "Registra solo evidenze UAT umane. Il sistema non accetta mai l’UAT e non decide mai un GO del pilota; i risultati dei test automatici sono allegati, non un PASS."

## Chi la usa
- **Facilitatore UAT:** apre una sessione, registra gli esiti degli scenari osservati per il partecipante e i rilievi, e invia la sessione per attestazione. I ruoli dei partecipanti per la nave includono Comandante, DPA, Direttore di macchina ed Equipaggio (13.03 §2, §4, §5, §6).
- **Attestatore:** una persona diversa dal facilitatore attesta la sessione.
- **Responsabili UAT / Product Owner:** registrano la soglia concordata e usano il risultato nella pagina **Revisione go/no-go del pilota**.

## Prima di iniziare
- La visualizzazione richiede la lettura a livello di tenant e la lettura UAT. Registrare sessioni ed esiti, registrare la soglia e attestare richiedono ciascuno un proprio permesso.
- I responsabili UAT devono aver concordato la soglia in anticipo; il sistema non ne imposta mai una.
- Scegliete uno pseudonimo per il partecipante. Non usate mai un nome, un indirizzo e-mail o un numero di equipaggio.
- La registrazione richiede una connessione attiva; offline è disattivata.

## Passo dopo passo
1. Scegliete **Nave** o **Rimorchiatore** con i pulsanti **Modalità operativa**.
2. In **Evidenze UAT rispetto alla soglia concordata** esaminate lo stato, i conteggi di **Bloccanti aperti**, **Rilievi maggiori aperti**, **Sessioni attestate** e **In attesa di attestazione** e la tabella degli scenari.
3. Se non esiste una soglia, compilate **Tasso minimo di superamento per scenario**, **Numero massimo di rilievi maggiori aperti**, **Conformità al design**, **Riferimento dell’accordo** e **Motivazione** in **Registra la soglia concordata** e premete **Registra**.
4. In **Sessioni UAT** usate il modulo **Nuova sessione UAT** per inserire **Ruolo**, **Ambiente**, **Commit di build** e **Pseudonimo del partecipante**, poi premete **Registra**.
5. Aprite la sessione. Con **Registra esito** scegliete lo **Scenario** e l'**Esito**; se pertinente aggiungete **Assistenza fornita** e un **Riferimento evidenza automatica**. Per un problema usate **Registra rilievo** con **Gravità** e **Sintesi del rilievo**.
6. Dopo aver registrato gli scenari premete **Invia la sessione per attestazione** (solo il facilitatore). Gli scenari senza risultato contano come **Non eseguito**. Una persona diversa dal facilitatore scrive una **Dichiarazione di attestazione** in **Attesta la sessione** e preme **Registra**.
7. In **Rilievi aperti** chiudete un rilievo con una **Risoluzione** e una **Nota di risoluzione**, poi premete **Risolvi**.

## Cosa significano gli stati
- **Soglia non impostata:** "Nessuna soglia concordata da persone è registrata. L’UAT non può essere superata finché non viene registrata."
- **Criteri non soddisfatti / Evidenze incomplete:** le evidenze attestate non raggiungono la soglia, oppure non tutti i ruoli e scenari sono coperti.
- **Criteri soddisfatti; decisione umana in attesa:** lo stato migliore possibile. Non è un'accettazione UAT né un GO del pilota; la decisione è umana.
- **Non eseguito:** non esiste alcun risultato per lo scenario. Uno scenario non eseguito non conta mai come superato.
- **Aperto / Inviato / Attestata:** lo stato della sessione. Solo le sessioni attestate contano per la soglia.
- **Superato / Fallito / Bloccato / Non eseguito:** l'esito dello scenario osservato per il partecipante.
- **Bloccante / Maggiore / Minore:** la gravità del rilievo. Un bloccante aperto fa fallire il gate.
- **Dati fittizi:** un record di esempio sintetico; non è un'evidenza UAT reale.

## Cosa non si può fare / attenzione
- Non potete attestare una sessione che avete facilitato: "L’attestazione deve essere registrata da una persona diversa dal facilitatore."
- Non potete risolvere un rilievo che avete segnalato voi. Un bloccante si chiude solo come **Risolto (corretto e ritestato)**; **Rischio accettato** non viene offerto.
- Un risultato automatico (Playwright) è solo un'evidenza allegata; l'esito è il risultato osservato per il partecipante.
- Il sistema non accetta mai l'UAT, non imposta mai una soglia e non inventa mai attestazioni o firme di approvazione.

## Se qualcosa va storto
- **Non hai accesso a questi record.** Controllate il vostro ruolo e l'ambito del tenant e contattate il Tenant Admin (13.03 §10).
- **Il record non è stato trovato nel tuo tenant.** Controllate il collegamento alla sessione e la modalità selezionata.
- **Il record è cambiato o è già stato usato. Ricarica.** Aprite lo stato attuale con **Ricarica**.
- **L’esito è incerto. Riprova invia lo stesso comando.** Usate **Riprova** per reinviare lo stesso comando in modo sicuro.
- **Rifiutato dalle regole.** Per esempio lo scenario non appartiene al ruolo della sessione, oppure un bloccante non può essere chiuso come rischio accettato.
- **Non consentito.** Per esempio avete facilitato questa sessione e non potete attestarla, oppure avete segnalato questo rilievo e non potete risolverlo.
- **Sei offline. La registrazione è disattivata fino al ritorno della connessione.** Quando la connessione ritorna premete **Aggiorna**.

## Pagine correlate
