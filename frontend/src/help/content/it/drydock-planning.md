---
key: drydock-planning
title: Pianificazione del bacino di carenaggio
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pianificazione del bacino di carenaggio contiene i **pacchetti di bacino** della nave selezionata (progetto, cantiere, date pianificate e voci dell’ambito). Risponde alla domanda: "A quale ordine di lavoro, difetto, certificato o checklist di visita è collegata ciascuna voce dell’ambito del bacino, e questi collegamenti sono attuali?" L’ambito è collegato a questi record (13.03 §5). **Completezza dei collegamenti** indica solo la completezza dei collegamenti; non è il risultato di una visita di classe o di bandiera né una dichiarazione di navigabilità.

## Chi la usa
- **Sovrintendente tecnico:** per preparare un pacchetto di bacino, collegare l’ambito ai record, pianificarlo e completarlo (13.03 §5).
- **Direttore di macchina:** per seguire come l’ambito si collega ai record di difetti e ordini di lavoro e, con il permesso, verificare un pacchetto completato da un’altra persona (13.03 §5).

## Prima di iniziare
- Seleziona la nave corretta. Se non è selezionata alcuna nave, la pagina indica **Seleziona una nave nell'area di lavoro per continuare.**
- Per vedere i pacchetti ti serve l’accesso in lettura al bacino. Creare, modificare e cambiare stato richiede l’accesso in scrittura; verificare richiede l’accesso di verifica.
- Tieni pronti gli identificativi e le revisioni dei record di ordini di lavoro, difetti, certificati e checklist di visita che collegherai.

## Passo dopo passo
1. Apri un pacchetto dall’elenco, oppure crea una bozza vuota con **Crea pacchetto di bacino**.
2. Compila **Progetto di bacino**, **Cantiere navale**, **Inizio pianificato** e **Fine prevista**.
3. Aggiungi voci con **Aggiungi voce dell'ambito**; inserisci il tipo di **Voce dell'ambito**, **Titolo**, **Pacchetto di lavoro**, **Requisito di classe**, **Requisito statutario** e **Priorità**.
4. Su ogni voce usa **Aggiungi collegamento** e inserisci **Tipo di record**, **Identificativo del record** e **Revisione fissata**.
5. Per il completamento aggiungi un riferimento di evidenza in **Prova di completamento** con **Aggiungi**. Poi premi **Salva**.
6. Controlla l’elenco **Blocchi alla pianificazione** o **Blocchi al completamento**. In **Cambia stato** scegli un’**Azione** (**Pianifica**, **Torna a bozza**, **Avvia bacino**, **Completa bacino**, **Annulla pacchetto**) e confermala. L’annullamento richiede una nota. Sono offerte solo le azioni valide per lo stato attuale; un pacchetto può essere annullato solo mentre è in Bozza o Pianificato.
7. Su un pacchetto completato che non hai completato tu, scrivi un **Motivo** di almeno 10 caratteri e usa **Verifica record** o **Rifiuta verifica**.
8. Segui le date pianificate ed effettive e ogni revisione nella **Cronologia**.

## Cosa significano gli stati
- **Draft / Planned / In corso / Completed / Verified / Cancelled:** il ciclo di vita del pacchetto. Completato e Verificato non sono la stessa cosa.
- **Links current / Attention / Review required / Incomplete:** la completezza dei collegamenti. Anche con i collegamenti attuali, non si tratta di un risultato di visita.
- **Linked / Unlinked / Overdue / Review required:** lo stato di collegamento di una voce dell’ambito (Review required quando un qualsiasi collegamento è Stale o Unavailable).
- **Current / Stale / Unavailable:** un record collegato confrontato con la sua revisione fissata. Esamina un collegamento non aggiornato e usa **Fissa di nuovo su** per fissarlo alla nuova revisione.
- **Sconosciuto / non impostato:** non è stata inserita alcuna priorità. Sconosciuto non significa "Normale" né "Bassa".

## Cosa non si può fare / attenzione
- La pagina non decide che un bacino sia sicuro né che la nave sia in condizioni di navigabilità.
- Non puoi verificare un pacchetto che hai completato tu. Rifiutare la verifica riporta il pacchetto a **In corso**.
- Costi, valuta, fornitore e budget sono solo riferimenti esterni; qui nulla viene sommato, approvato o ordinato.
- Un pacchetto completato, verificato o annullato non può essere modificato.
- Offline i comandi non vengono inviati.

## Se qualcosa va storto
- **Il server ha elencato voci mancanti o bloccanti:** risolvi l’elenco dei blocchi e riprova.
- **Il record è cambiato o il comando è in conflitto:** usa **Ricarica la versione più recente** ed esamina.
- **L’esito è sconosciuto:** **Riprova** invia di nuovo il comando identico.
- **Non hai accesso a questo record:** controlla il tuo ruolo e il contesto della nave; se necessario contatta il tuo amministratore del tenant (13.03 §10).
- **Non puoi leggere questi tipi di record collegati:** verifica il permesso con il responsabile del record o con il tuo amministratore del tenant.

## Pagine correlate
