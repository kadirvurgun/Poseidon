---
key: pre-survey-checklist
title: Checklist pre-visita
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La checklist pre-visita segue le attività di preparazione per la visita di un certificato con responsabile, scadenza, evidenze e verifica umana indipendente. Risponde alla domanda: "Quali voci di preparazione per questa visita sono aperte, quali hanno evidenze e quali sono state verificate da un'altra persona?" La prontezza è consultiva e non è mai un'approvazione della visita (13.03 §1, §8).

## Chi la usa
- **Comandante, Direttore di macchina / Sovrintendente tecnico:** avviano una checklist, rispondono alle voci e allegano evidenze.
- **Verificatore:** confronta le voci compilate da un'altra persona con le relative evidenze e le verifica o le rifiuta (13.03 §1).
- Gli utenti con solo accesso in lettura vedono "Sola lettura per il tuo ruolo."

## Prima di iniziare
- La visualizzazione richiede la lettura dei certificati. Avviare e compilare una checklist richiede la pianificazione delle visite su quella nave; verificare richiede la revisione delle visite.
- Scegliete la nave corretta con **Scegli una nave** (13.03 §1).
- La finestra di visita del certificato dovrebbe essere dichiarata nel [Centro di gestione certificati](/app/certificate-command-center).
- La pagina richiede una lettura in tempo reale. Offline vedete "Offline: è necessaria una lettura online aggiornata."

## Passo dopo passo
1. Consultate gli elenchi della nave in **Checklist**, oppure nel [Pianificatore visite](/app/survey-planner) premete **Apri checklist** o **Avvia checklist** su una riga.
2. Per un nuovo elenco usate **Scegli un certificato**, scegliete un **Modello** e premete **Crea checklist**. Il modello è un modello di partenza, non un catalogo approvato.
3. In **Dati del piano di visita** compilate **Inizio pianificato**, **Fine prevista**, **Luogo previsto (testo)** e, se disponibile, **Scalo**.
4. In **Voci** inserite per ogni voce **Ruolo responsabile**, **Scadenza**, **Risposta** e, se necessario, **Rilievo** e **Commento**. Collegate le evidenze con **Aggiungi evidenza**, poi premete **Salva**. Ogni salvataggio crea una nuova revisione.
5. Come verificatore, sulle voci compilate da un'altra persona scegliete **Verifica** o **Rifiuta** in **Decisione**, inserite il motivo e premete **Registra verifica**.
6. Tenete d'occhio **Prontezza** e **Blocchi**.

## Cosa significano gli stati
- Risposta della voce: **Aperto**, **Pronto**, **Richiede attenzione**, **Bloccato**, **Non applicabile**. Una voce la cui risposta è sconosciuta resta **Aperto**; non conta mai come "Non applicabile" né come "Pronto".
- Verifica: **In attesa**, **Verificato (umano)**, **Rifiutato**. Una voce compilata resta **In attesa** finché non viene verificata.
- Prontezza: **Bloccato**, **Incompleto**, **Preparato, in attesa di conferma umana**. Anche lo stato migliore attende una conferma umana; il sistema non dice mai "visita approvata".
- **Critico / Obbligatorio / Facoltativa:** l'importanza della voce. Le voci critiche e obbligatorie non risolte pesano sulla prontezza e compaiono in **Blocchi**.
- **Viaggio: sconosciuto (nessun modulo viaggi):** l'adeguatezza al viaggio non può ancora essere valutata e resta sconosciuta.

## Cosa non si può fare / attenzione
- Non potete verificare una voce a cui avete risposto voi: "Hai compilato questa voce; un’altra persona deve verificarla."
- Rispondere **Pronto** a una voce non la verifica; Completato ≠ Verificato. Ogni decisione di verifica è legata esattamente alla risposta e alle evidenze che vedete.
- La prontezza non è un'approvazione della visita, una prenotazione né la validità del certificato. Il coordinamento con classe, bandiera e autorità è esterno e qui non viene simulato.
- Le voci del modello non sono un catalogo approvato; una persona autorizzata valuta se sono adatte alla nave.

## Se qualcosa va storto
- **Questa checklist è cambiata da quando l’hai aperta. Ricarica prima di modificare.** Premete **Ricarica la versione corrente**.
- **L’esito non è ancora noto. Riprovare invia la stessa richiesta.** Usate **Riprova la stessa richiesta**.
- **Voci non registrate:** correggete le voci elencate e salvate di nuovo.
- **Non hai accesso a questa vista.** Controllate il vostro ruolo, la nave e l'ambito dell'oggetto; se necessario contattate il Tenant Admin (13.03 §10).
- Se una voce non può essere verificata, controllatene evidenze e fonte e rimandatela al verificatore (13.03 §10).

## Pagine correlate
