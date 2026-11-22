---
key: tug-equipment-readiness
title: Prontezza dell'attrezzatura di rimorchio
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina mostra le **dichiarazioni delle attrezzature di rimorchio** del rimorchiatore selezionato (verricello di rimorchio, gancio di rimorchio, cavo di rimorchio, sgancio di emergenza, antincendio FiFi) e le relative **verifiche del direttore di macchina**. Risponde alla domanda: "Quali sono il CLS, le date di prova, la condizione e la criticità di ciascun elemento, chi lo ha verificato e cosa dice il controllo di prontezza?" La valutazione è solo un parere e alimenta la verifica di prontezza; non è una decisione GO.

## Chi la usa
- **Comandante del rimorchiatore:** per controllare le evidenze su verricello, cavo di rimorchio, gancio di rimorchio / stopper della catena, sgancio di emergenza, FiFi e difetti critici (13.03 §3).
- **Chief Engineer:** per verificare o non verificare le dichiarazioni e registrare l'effetto di un difetto critico sulla prontezza (13.03 §5).

## Prima di iniziare
- Aprite il rimorchiatore da **Operazioni rimorchiatore**; questa schermata non richiede alcuna assegnazione.
- L'aggiunta o la modifica di una dichiarazione richiedono l'accesso in scrittura ai rimorchiatori e alle attrezzature.
- La registrazione di una verifica richiede una designazione di **Chief Engineer** registrata dal server per quel rimorchiatore.

## Passo dopo passo
1. Leggete il badge complessivo sulla riga **Valutazione**.
2. Su ogni scheda di attrezzatura, controllate il badge di risultato, il codice motivo, **CLS (t)**, **Prossima prova prevista**, **Condizione** e **Critico**.
3. Per una nuova dichiarazione, premete **Aggiungi attrezzatura**; compilate **Tipo di attrezzatura**, **Etichetta**, **CLS (t)**, **Ultima prova**, **Prossima prova prevista**, **Condizione**, **Critico**, **ID asset PMS**, **ID certificato** e **Note**, poi premete **Salva dichiarazione**.
4. Per modificare una dichiarazione esistente, usate **Modifica** sulla sua scheda.
5. Come direttore di macchina, scegliete la vostra **Designazione**, scrivete un motivo e usate **Verificato** o **Non verificato** per **Registra verifica**. Per "Non verificato" il motivo è obbligatorio.
6. Consultate lo storico delle verifiche con revisione e ora sotto **Verifiche del direttore di macchina**, e la tabella **Verifiche delle fonti** in fondo.

## Cosa significano gli stati
- **Ready / Conditional / Not ready / Review required:** la valutazione consultiva. Pronto non è una decisione GO.
- **Pass / Fail / Unknown:** il risultato del controllo per un elemento.
- **Serviceable / Degraded / Failed / Unknown:** la condizione dichiarata. Se non viene scelta alcuna condizione resta "Sconosciuto"; non viene conteggiata come in servizio.
- **Non valutato:** la criticità non è stata valutata. Ciò non significa "No".
- **Verificato / Non verificato:** la registrazione umana del direttore di macchina.
- **sostituito da una revisione successiva:** la verifica apparteneva a una revisione precedente della dichiarazione; la revisione attuale richiede una nuova verifica.

## Cosa non si può fare / attenzione
- Il salvataggio registra una dichiarazione non verificata. Ogni modifica richiede una nuova verifica del direttore di macchina.
- Completare un record e verificarlo non sono la stessa azione; la verifica attende la persona autorizzata (13.03 §1).
- Il tipo di attrezzatura non può essere cambiato dopo il salvataggio; aggiungete una nuova dichiarazione per un altro tipo.
- La pagina non dichiara mai sicura l'attrezzatura di rimorchio; la decisione resta al Comandante del rimorchiatore (13.03 §3).
- I comandi sono disattivati offline.

## Se qualcosa va storto
- **Messaggio "The record changed":** usate **Ricarica** per esaminare la dichiarazione attuale.
- **Messaggio "The outcome is uncertain":** **Riprova** invia lo stesso comando.
- **Il record non può essere verificato:** controllate le evidenze e la fonte PMS/certificato; contattate il verificatore (13.03 §10).
- **Nessuna autorizzazione:** controllate il vostro ruolo, il rimorchiatore e il contesto di designazione; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).

## Pagine correlate
