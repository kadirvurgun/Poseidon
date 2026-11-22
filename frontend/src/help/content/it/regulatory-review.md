---
key: regulatory-review
title: Revisione normativa SME
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La revisione normativa SME registra campagne in cui un esperto umano esamina le voci di fonte e di applicabilità di una nave o di un rimorchiatore. Risponde alla domanda: "Quali voci di fonte e di requisito per questa nave sono state confermate da un revisore autorizzato, e quali sono ancora aperte e tracciate?" È soltanto un record di revisione umana: il sistema non approva alcun requisito, non attiva alcuna regola o permesso e non dichiara alcuna approvazione di classe o di tipo (13.03 §8).

## Chi la usa
- **Utente Regulatory / Assurance:** apre campagne di revisione, registra gli esiti sulle voci e chiude la campagna (13.03 §8).
- **Revisore designato (SME):** conferma o traccia le voci nell'ambito della propria autorità della fonte.
- **DPA / HSEQ:** tiene sotto controllo le voci aperte e non tracciate (13.03 §4).

## Prima di iniziare
- La visualizzazione richiede la lettura della revisione normativa. Aprire e chiudere campagne richiede la gestione; registrare esiti richiede il permesso di esito e una nomina di revisore valida per questo ambito.
- Scegliete **Nave** o **Rimorchiatore** nelle schede **Ambito di revisione**, poi aprite la nave corretta in **Scegli una nave** (13.03 §1).
- Le fonti dovrebbero essere registrate come ufficiali e con versione nella pagina [Bozze delle fonti](/app/sources) (13.03 §8).
- Offline, i dati possono non essere aggiornati e i comandi sono disattivati.

## Passo dopo passo
1. Scegliete ambito e nave. Se non esiste una campagna, premete **Apri campagna di revisione**.
2. In **Matrice di responsabilità delle fonti** esaminate per ogni autorità della fonte la categoria, i conteggi di voci aperte e confermate e i ruoli responsabili.
3. Selezionate una voce in **Elementi da rivedere**.
4. Nel modulo **Registra esito della revisione** compilate **Nomina di revisore**, **Esito**, **Autorità della fonte** e **Ruolo responsabile**. Se la voce non ha una fonte registrata, inserite **Riferimento della fonte autorevole** e **Versione / edizione della fonte**. Per una voce che resta aperta, inserite un **ID della segnalazione**.
5. Scrivete un motivo di 10–2000 caratteri in **Nota del revisore** e premete **Registra**.
6. Quando tutte le voci sono definite, scegliete un **Esito della chiusura** in **Chiudi campagna**, scrivete il **Motivo della chiusura** e premete **Chiudi campagna**.

## Cosa significano gli stati
- Campagna: **Aperta**, **In revisione**, **Chiusa con elementi aperti**, **Chiusa**.
- Esito: **Confermato**, **Non applicabile (confermato)**, **Revisione richiesta**, **Ambiguo**. Quando dati critici sono sconosciuti la voce resta **Revisione richiesta**; una voce sconosciuta non viene mai contata come "Non applicabile".
- **Candidato (non verificato):** la fonte non è ancora collegata a una fonte ufficiale verificata.
- **Obsoleto — riferimento cambiato** e **Profilo della nave modificato:** la fonte o il profilo della nave sono cambiati dopo l'esito; la voce deve essere rivista.
- **Elementi aperti non tracciati:** voci aperte senza una segnalazione tracciata. Finché il numero è superiore a zero la campagna non può essere chiusa.
- Le categorie **Legge**, **Regola di classe**, **SMS aziendale** e **Linea guida — non è legge** non si sostituiscono mai l'una con l'altra (13.03 §8).

## Cosa non si può fare / attenzione
- Chi ha aperto una campagna non può registrare esiti al suo interno; senza nomina di revisore gli esiti sono in sola lettura.
- Un revisore la cui qualifica non è verificata può tracciare voci aperte ma non può confermare. Una voce obsoleta non può essere confermata.
- Per confermare serve una fonte registrata oppure un riferimento a una fonte autorevole con la sua edizione. Non lasciate record orfani o senza fonte nella catena Requisito → Controllo → Evidenza → Verifica (13.03 §8).
- **Chiusa** richiede che ogni voce sia confermata; altrimenti chiudete con elementi aperti, ciascuno tracciato.
- Non dichiarate nella nota alcuna approvazione di classe o di tipo. Nell'ambito rimorchiatori, nessun limite di rimorchio privo di una fonte controllata è un valore predefinito legale.
- Gli esiti vengono registrati solo da utenti umani; gli account di IA e di automazione non possono registrarli.

## Se qualcosa va storto
- **Il record è cambiato. Ricarica e verifica prima di continuare.** Premete **Ricarica**.
- **L’esito è incerto. Riprova invia lo stesso comando.** Usate **Riprova**.
- **Le regole di revisione hanno rifiutato questo comando.** Controllate se la voce è obsoleta, il riferimento della fonte e la segnalazione tracciata.
- **Non hai accesso alla revisione regolamentare di questa nave.** oppure **Non sei autorizzato a questa azione.** Controllate il vostro ruolo e l'ambito; se necessario contattate il Tenant Admin (13.03 §10).
- Se l'applicabilità appare inattesa, controllate la fonte e gli input di contesto e parlatene con il responsabile del controllo o con lo SME (13.03 §10).

## Pagine correlate
