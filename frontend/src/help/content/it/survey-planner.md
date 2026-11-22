---
key: survey-planner
title: Pianificatore visite
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Il pianificatore visite mostra le finestre di visita dichiarate su un calendario di flotta insieme all'idoneità rispetto a viaggio e porto. Risponde alla domanda: "Nell'intervallo che ho scelto, quali certificati su quali navi hanno una finestra di visita che si apre o è già passata, ed esiste una checklist di preparazione?" È soltanto un supporto alla pianificazione: non è un calendario dell'autorità, una prenotazione né un risultato di visita (13.03 §8).

## Chi la usa
- **Comandante, Direttore di macchina / Sovrintendente tecnico:** vedono le visite in arrivo e avviano in tempo la checklist di preparazione (13.03 §5).
- **Utente Regulatory / Assurance:** tiene sotto controllo le finestre dichiarate e le dichiarazioni mancanti in tutta la flotta (13.03 §8).

## Prima di iniziare
- La visualizzazione richiede la lettura dei certificati. Vengono elencate solo le navi a cui potete accedere.
- Le finestre derivano dalle dichiarazioni nel record del certificato. Se non è dichiarata alcuna finestra, la riga compare in **Finestra non dichiarata**; inserite le date dal documento di origine nel [Centro di gestione certificati](/app/certificate-command-center).
- La pagina richiede una lettura in tempo reale. Offline vedete "Offline: è necessaria una lettura online aggiornata."

## Passo dopo passo
1. In **Intervallo del calendario** scegliete **Dal** e **Al** e premete **Mostra**.
2. Leggete nella riga di riepilogo i conteggi aperti, in arrivo, scaduti e sconosciuti.
3. Su ogni riga esaminate **Certificato**, **Emittente**, **Bandiera dichiarata**, **Base normativa**, **Scadenza dichiarata**, **Finestra di visita** e **Idoneità viaggio/porto**.
4. Premete **Apri certificato** per vedere il certificato.
5. Usate **Apri checklist** se esiste un elenco di preparazione, oppure **Avvia checklist** se non esiste, per andare alla [Checklist pre-visita](/app/pre-survey-checklist).

## Cosa significano gli stati
- **In arrivo / Finestra aperta:** la posizione rispetto alla finestra dichiarata.
- **Finestra superata, soddisfacimento non registrato:** la finestra è terminata e nessun record mostra che la visita sia stata eseguita. Non prova nemmeno che la visita sia stata saltata; controllate i record.
- **Finestra sconosciuta:** la data manca o non è leggibile. Una finestra sconosciuta non significa mai "nessun problema"; richiede una revisione.
- Idoneità viaggio/porto: **Condizionato**, **Non idoneo**, **Sconosciuto**. La pagina non fornisce mai un'approvazione di "idoneità".
- Prontezza della checklist: **Bloccato**, **Incompleto**, **Preparato, in attesa di conferma umana**.

## Cosa non si può fare / attenzione
- Il pianificatore non prenota nulla e non presenta nulla a un'autorità. Il coordinamento con classe, bandiera e autorità è esterno e qui non viene simulato.
- Ciò che mostra il calendario non è un risultato di visita né la validità di un certificato. Le decisioni su viaggio e visite spettano a persone autorizzate.
- La pagina è in sola lettura; le modifiche si fanno nelle pagine dei certificati e delle checklist.

## Se qualcosa va storto
- **È mostrata solo una parte della flotta; restringi l’intervallo.** Scegliete un intervallo di calendario più breve.
- **Nessun certificato con finestre di visita in questo intervallo.** Ampliate l'intervallo o controllate le dichiarazioni dei certificati.
- **Impossibile caricare. Riprova.** Premete **Riprova**. Se il problema persiste, segnalatelo al supporto con la schermata, l'ora e i passaggi eseguiti (13.03 §10).
- **Non hai accesso a questa vista.** Controllate il vostro ruolo e l'ambito delle navi; se necessario contattate il Tenant Admin (13.03 §10).

## Pagine correlate
