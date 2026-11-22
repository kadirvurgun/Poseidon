---
key: companies
title: Società
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## A cosa serve questa pagina
La pagina Società elenca i **record delle società** della tua area di lavoro che sei autorizzato a vedere. Risponde alla domanda: "Quali società sono definite in questa area di lavoro e qual è il loro codice e nome?" I record di navi e rimorchiatori appartengono a una società, quindi la società corretta è il primo passo nella scelta del tuo contesto.

## Chi la usa
- **Tenant Admin:** per creare e modificare i record delle società secondo il principio del privilegio minimo (13.03 §9).
- **Operazioni a terra:** per trovare il contesto societario corretto.

## Prima di iniziare
- Per vedere questa pagina ti serve il permesso di lettura delle società. Senza di esso viene mostrato il messaggio "Non hai accesso a questa area di lavoro."
- La creazione di un record richiede il permesso di creazione. La possibilità di modificare un record dipende dal permesso che il server concede per quel record.

## Passo dopo passo
1. Nell’elenco vedi il codice e il nome di ogni società. Usa **Precedente** / **Successivo** per le altre pagine.
2. Per cercare, digita in **Cerca per codice o nome** e premi **Cerca**.
3. Seleziona il nome di una società per aprire la sezione **Dettagli**.
4. Per una nuova società premi **Nuovo record**, inserisci codice e nome e premi **Salva**. Tutti i campi sono obbligatori.
5. Per modificare un record esistente usa **Modifica** nei dettagli.
6. Apri **Mostra la scheda dell’entità giuridica** per vedere le informazioni sull’entità giuridica; questa sezione è di sola lettura.

## Cosa significano gli stati
- **No records found:** per la tua ricerca o il tuo ambito di permessi non è stato restituito alcun record.
- **Legal entity: Draft – incomplete, not active / Active / Inactive:** lo stato del record dell’entità giuridica. Bozza significa che prima dell’attivazione mancano ancora dei valori.
- **Sconosciuto:** il valore non è presente nel record. Sconosciuto non significa "No" né "valido".
- **Saved:** il record è stato scritto sul server. Questo record non crea alcuna approvazione operativa.

## Cosa non si può fare / attenzione
- Creare o modificare il record di una società non è un’approvazione operativa, di conformità o di sicurezza.
- I valori dell’entità giuridica non vengono modificati da questa pagina; cambiano solo tramite comandi HR sottoposti ad audit. Gli identificativi di registro sono mostrati mascherati.
- Lo stesso codice non può essere usato da due società.
- Se lasci la pagina con una bozza non salvata, ti viene chiesto se scartarla o continuare a modificarla.

## Se qualcosa va storto
- **This record changed in another operation:** la tua bozza viene conservata. Scarta la bozza per caricare il record attuale, poi ripeti la modifica.
- **This code is already in use:** inserisci un codice diverso.
- **The outcome could not be confirmed:** i campi restano bloccati finché l’esito non è noto; riprova in sicurezza lo stesso comando.
- **Your permission may have changed / No access:** controlla il tuo ruolo e il contesto dell’area di lavoro; se necessario contatta il tuo amministratore del tenant (13.03 §10).
- **The service is unavailable:** usa "Riprova"; se il problema continua, segnalalo con il riferimento di supporto.

## Pagine correlate
