---
key: tug-command
title: Comando rimorchiatori
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## A cosa serve questa pagina
Indicata nel menu come **Comando rimorchiatori**, questa pagina mostra il **contesto delle fonti** del rimorchiatore selezionato. Risponde alla domanda: "Quali dichiarazioni di ruolo, tipo, tiro a punto fisso (BP) e attrezzature sono registrate per questo rimorchiatore, da dove provengono e quanto sono attuali?" La pagina mostra solo le dichiarazioni e le loro fonti; non fornisce alcuna prontezza operativa, idoneità, permesso o approvazione di sicurezza. Il badge in alto lo indica: **Dichiarazioni delle fonti · prontezza operativa sconosciuta**.

## Chi la usa
- **Comandante del rimorchiatore:** per confermare il contesto di ruolo, BP e attrezzature del proprio rimorchiatore prima dell'inizio del lavoro (13.03 §3).
- **Coordinatore delle operazioni dei rimorchiatori / Operazioni a terra:** per assicurarsi di trovarsi nel giusto contesto di rimorchiatore prima di passare al tabellone missioni e all'acquisizione missione (13.03 §7).
- **Equipaggio:** per vedere la propria vista di ruolo e le schermate della modalità nave a cui ha accesso (13.03 §6).

## Prima di iniziare
- Servono la modalità rimorchiatore e l'accesso in lettura ai rimorchiatori. Senza di essi la pagina mostra **L’accesso non è disponibile.**
- Se non è selezionato alcun contesto, la pagina elenca i rimorchiatori autorizzati sotto **Seleziona rimorchiatore**; apritene uno.
- L'elenco delle attrezzature richiede anche l'accesso in lettura alle attrezzature; senza di esso la fonte delle attrezzature appare come "unavailable or not granted".

## Passo dopo passo
1. Aprite il rimorchiatore dall'elenco **Seleziona rimorchiatore**. La riga **Contesto del rimorchiatore** sotto il titolo mostra codice, nome e modalità del rimorchiatore.
2. In **Ruolo (assegnazione server)** e **Vista menu modalità rimorchiatore**, leggete il ruolo e la vista di menu che il server vi ha assegnato.
3. In **Visibilità delle modalità**, verificate se le schermate della modalità nave sono aperte. La sezione **Schermate modalità nave** rimanda solo alle schermate che vi sono concesse.
4. Controllate da **Tipo di rimorchiatore**, **Fonte / revisione del tipo** e **Osservazione / politica della fonte del tipo** da quale fonte e revisione proviene il tipo.
5. Leggete **BP del profilo (non verificata)**, **Fonte / versione / provenienza del BP** e **Attualità / osservazione del BP**. Si tratta di un valore dichiarato, non misurato.
6. In **Contesto attrezzature**, vedete le dichiarazioni delle attrezzature, i loro livelli, le revisioni e la provenienza.
7. Usate le schede della pagina per passare a **Tabellone missioni** o (con accesso in scrittura) **Acquisizione missione**, oppure (con accesso in scrittura) premete **Nuova richiesta**.

## Cosa significano gli stati
- **Sconosciuto — nessuna assegnazione di ruolo:** il server non ha alcuna assegnazione di ruolo per voi su questo rimorchiatore. Questo non significa "nessun ruolo, nessun problema"; serve un'assegnazione da parte di un amministratore.
- **Role labels unavailable / Menu lens unavailable:** la fonte non può essere letta ora. Un'informazione mancante non viene considerata vuota.
- **Nessuna vista configurata per la modalità rimorchiatore:** per voi non è definita alcuna vista di menu in questa modalità.
- **Nascoste in modalità rimorchiatore. È richiesto un ruolo multimodale.:** le schermate della modalità nave richiedono un ruolo multimodale autorizzato esplicitamente.
- **BP del profilo (non verificata):** il tiro a punto fisso dichiarato. Non è il risultato di una prova verificata.
- **Nessuna dichiarazione di attrezzature restituita. L'idoneità operativa verificata rimane sconosciuta.:** un elenco vuoto non significa mai che le attrezzature siano idonee.
- **Unknown / Unavailable:** il valore di origine manca o non è stato possibile leggerlo. Sconosciuto non sostituisce mai "Pronto" o "No".

## Cosa non si può fare / attenzione
- In questa pagina non si modifica alcun campo e non si produce alcun risultato di prontezza. La decisione operativa e l'autorità di sicurezza restano al Comandante del rimorchiatore a bordo (13.03 §3).
- Una proposta di assegnazione da terra non prevale mai sulla decisione di sicurezza del Comandante del rimorchiatore a bordo (13.03 §7).
- Anche con un permesso multimodale dichiarato, ogni schermata della modalità nave richiede comunque la propria autorizzazione del server.
- Offline, le fonti attuali del profilo non vengono mostrate; riconnettetevi per ricontrollare l'accesso e le versioni delle fonti.

## Se qualcosa va storto
- **L'accesso non è disponibile:** controllate il vostro ruolo, il rimorchiatore e il contesto dell'oggetto; se necessario, contattate l'amministratore del vostro tenant (13.03 §10).
- **Avviso "Current identity and vessel access must be checked":** premete **Ricontrolla accesso**.
- **Dati non disponibili:** controllate la connessione e premete **Riprova**.
- **Valori di BP o di attrezzature inattesi:** annotate i campi di fonte e versione ed esaminateli con il responsabile della fonte; se un record non può essere verificato, controllatene le evidenze e la provenienza (13.03 §10).

## Pagine correlate
