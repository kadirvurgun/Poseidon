---
key: pilot-program
title: Programma pilota: settimane e rapporto di valore
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina del programma pilota riunisce in un unico punto le settimane pilota di una nave (e facoltativamente di un rimorchiatore), i punti di controllo di ogni settimana, i problemi del pilota, il feedback giornaliero e il rapporto di valore. Risponde alla domanda: "I punti di ogni settimana pilota sono stati soddisfatti da record reali, e come sono cambiati i valori misurati rispetto alla baseline?" Vale l'avviso in alto: "Il software non può completare un pilota. Un punto di controllo è soddisfatto solo da un collegamento che crei a un record reale verificato dal server; una baseline mancante resta Sconosciuta; solo il Product Owner nominato chiude una settimana o registra la decisione del programma."

## Chi la usa
- **Product Owner:** chiude le settimane e registra la decisione del programma (Continua, Correggi, Interrompi, Espandi). Serve una nomina vigente di Product Owner.
- **Team del pilota:** collega record ai punti di controllo, segnala problemi, registra feedback e misurazioni.
- **Responsabile del triage:** esegue il triage e risolve i problemi.
- 13.03 non definisce un ruolo separato per questa pagina; si applicano i principi di amministrazione del tenant (13.03 §9).

## Prima di iniziare
- La visualizzazione richiede la lettura a livello di tenant e la lettura del programma pilota. Creare programmi, registrare, eseguire il triage e decidere richiedono ciascuno un proprio permesso; chiusure di settimana e decisioni richiedono anche una nomina di Product Owner.
- I record che collegate (per esempio un certificato, un record di equipaggio o un ordine di lavoro) devono già esistere nelle rispettive pagine.
- Nel feedback non inserite mai nomi, indirizzi e-mail, numeri di telefono o di documenti; usate solo uno pseudonimo e il ruolo.
- La registrazione richiede una connessione attiva.

## Passo dopo passo
1. Aprite un programma da **Programmi pilota**. Per un nuovo programma compilate **Nome del programma**, **Nave**, **Rimorchiatore (facoltativo)**, **Data di inizio della settimana 1** e **Fuso orario del calendario (IANA)** in **Crea programma pilota** e premete **Registra**.
2. In ogni sezione **Settimana** esaminate le colonne **Punto di controllo**, **Criterio (fonte)**, **Ambito**, **Stato** e **Record collegati**.
3. Per soddisfare un punto, inserite **Punto di controllo**, **Tipo di record** e **Identificativo del record** in **Collega un record** e premete **Collega un record**. Rimuovete un collegamento errato con **Ritira collegamento** dopo aver scritto un **Motivo del ritiro**.
4. In **Problemi del pilota** usate **Segnala problema** per inserire l'ambito, la **Categoria**, la gravità e la **Sintesi del problema**. Il responsabile del triage usa **Triage** con una **Nota di triage**, poi risolve il problema.
5. In **Feedback giornaliero** usate **Registra feedback** per inserire una **Sintesi** e una **Valutazione**.
6. In **Rapporto di valore** usate **Registra misurazione** per inserire **Metrica**, **Fase** (Baseline o Periodo pilota), **Valore**, **Dimensione del campione**, **Periodo dal**, **Periodo al**, **Riferimento dell’evidenza** e **Metodo e campione**.
7. Se siete il Product Owner, registrate esito e motivazione in **Chiudi settimana**. Alla fine del programma compilate la decisione, **Nomina** e **Motivazione** in **Registra la decisione del programma** e premete **Registra**.

## Cosa significano gli stati
- **Collegato e verificato:** al punto è collegato un record reale verificato dal server. I punti **Collegamento non più valido** e **Mancante** non sono soddisfatti.
- **Completion required:** il record collegato deve trovarsi nel suo stato completato. "La traccia di audit prova che una persona ha scritto il record; non prova che il record o il flusso sia completato."
- **Pronta per chiusura umana / Non pronto:** una raccomandazione per la settimana; solo il Product Owner la chiude come **Accettata** o **Non accettata**.
- **Triage nello stesso giorno / Triage in ritardo / Triage previsto oggi / Scaduto:** la tempistica del triage dei problemi P0 rispetto al giorno di calendario del programma. Per i problemi con **Nessun obiettivo in giornata nella fonte** il sistema non inventa alcuna scadenza.
- **Misurato / Sconosciuto:** quando manca un valore di baseline o del pilota, il rapporto resta **Sconosciuto**; non viene mai contato come zero né come miglioramento. I valori **Non confrontabile** non vengono confrontati.
- **Telemetria (dichiarata, non verificata):** un valore di telemetria dichiarato.
- **Espansione possibile; decisione umana in attesa / Correzione consigliata:** una raccomandazione del programma, non una decisione.

## Cosa non si può fare / attenzione
- "Accettata è possibile solo se ogni punto è verificato e nessun triage P0 è aperto, in attesa o in ritardo."
- "Espandere è possibile solo se ogni settimana è accettata e valori, UAT, difetti e P0 sono risolti."
- Se siete autori delle evidenze su cui si basa una settimana o una decisione (collegamenti, feedback, misurazioni), non potete chiudere quella settimana né registrare quella decisione.
- Non è possibile registrare chiusure di settimana o decisioni per un programma fittizio di fixture.
- Il sistema non completa mai un pilota, non chiude mai una settimana e non inventa mai decisioni o firme di approvazione.

## Se qualcosa va storto
- **Non hai una nomina vigente di Product Owner; chiusure e decisioni sono in sola lettura.** Contattate il Tenant Admin per la nomina (13.03 §10).
- **La raccolta dei feedback non è disponibile finché un operatore non configura la politica di conservazione.** Chiedete all'amministratore di sistema di configurare la politica di conservazione.
- **Il record è cambiato o è già stato usato. Ricarica.** Aprite lo stato attuale con **Ricarica**.
- **L’esito è incerto. Riprova invia lo stesso comando.** Usate **Riprova** per reinviare lo stesso comando in modo sicuro.
- **Rifiutato dalle regole.** Per esempio il record collegato potrebbe non esistere, essere fuori ambito o non essere completato.
- **Non consentito.** Siete autori di collegamenti, feedback o misurazioni su cui si basa questa chiusura di settimana o decisione; deve registrarla un'altra persona.
- Una settimana non ancora iniziata non può essere chiusa (viene mostrato "Il record è cambiato o è già stato usato. Ricarica.").
- **Non hai accesso a questi record.** Controllate il vostro ruolo e l'ambito del tenant (13.03 §10).

## Pagine correlate
