---
key: safety-events
title: Eventi di sicurezza e near miss
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Eventi di sicurezza e near miss vi permette di segnalare rapidamente un evento di sicurezza su una nave e di seguire i passaggi umani successivi: classificazione (triage), indagine, revisione indipendente, azioni correttive e preventive, verifica, efficacia e chiusura. Risponde alla domanda: "Cosa è successo, chi se ne occupa, e l'azione ha davvero funzionato?" Qui nulla decide automaticamente gravità, causa radice o chiusura; ciascuna è un record umano separato. La pagina segue la catena Rilievo → Azione → Verifica, e **chiuso non è la stessa cosa di efficace** (13.03 §4).

## Chi la usa
- **Equipaggio / OOW / Macchinista e Comandante:** segnalano near miss, incidenti, infortuni, affaticamento, eventi ambientali o informatici (13.03 §2, §6).
- **DPA / HSEQ:** come ponte per la sicurezza, tengono sotto controllo gli eventi critici e le azioni scadute e gestiscono l'indagine e l'efficacia delle azioni (13.03 §4).
- Triage, indagine, revisione, lettura dell'identità, scrittura delle azioni, verifica e revisione dell'efficacia sono permessi separati; ogni utente vede solo le parti per cui ha i permessi.

## Prima di iniziare
- Scegliete la nave corretta (**Scegli una nave**). Senza accesso vedete "Non hai accesso agli eventi di sicurezza di questa nave."
- Segnalare richiede il permesso di segnalazione; leggere l'elenco richiede il permesso di lettura.
- Una segnalazione **Riservato** e **Invia segnalazione** richiedono una connessione. Offline potete solo salvare una bozza nominativa, e solo su un dispositivo preparato in anticipo.
- L'ora si inserisce in UTC.

## Passo dopo passo
1. Consultate gli eventi della nave nell'elenco **Eventi**. Usate **Solo le mie segnalazioni** per vedere solo le vostre.
2. Per un nuovo record premete **Segnala un evento**. Scegliete **Nominativo** o **Riservato** come **Modalità di segnalazione**. Per una segnalazione riservata che richiede un seguito, scegliete **Fornitemi un codice di follow-up privato**.
3. Compilate **Tipo di evento**, **Quando è successo?**, **Operazione**, **Posizione a bordo**, **Sintesi**, **Cosa è successo**, **È stata intrapresa un’azione immediata?** e facoltativamente **La tua stima della gravità**. Se l'ora non è nota scegliete **Sconosciuto** e compilate **Perché l’ora è sconosciuta?**.
4. Potete usare **Salva bozza** finché è incompleta. Quando **Ancora necessario prima dell’invio** è vuoto premete **Invia segnalazione**. Una bozza non inviata può essere ritirata con **Ritira bozza** e un motivo.
5. Se è stato emesso un codice di follow-up, conservate subito in privato il codice indicato in **Il tuo codice di follow-up riservato** e premete **L’ho conservato**. Viene mostrato una sola volta. In seguito, inseritelo come **Codice di follow-up** in **Follow-up riservato** e premete **Apri** per leggere le risposte del team.
6. Chi esegue il triage registra la gravità e **Indagine richiesta** con un motivo in **Triage** e preme **Registra classificazione**, oppure la rimanda con **Restituisci al segnalante**.
7. Un investigatore preme **Apri indagine**, compila **Fattori contributivi**, **Cause radice**, **Barriere** e **Incertezze**, poi preme **Salva indagine** e **Invia in revisione**. In **Revisione indipendente** un'altra persona sceglie **Approva**, **Restituisci** o **Rifiuta**.
8. In **Azioni correttive e preventive** usate **Aggiungi azione** con un responsabile, una scadenza e il rilievo a cui si riferisce. Il responsabile registra **Segnala completamento**, un'altra persona registra **Verifica** e una terza registra **Valuta efficacia**.
9. Quando **Ostacoli alla chiusura** è vuoto, un revisore (con permesso di revisione) che non è né il segnalante né un autore dell'indagine preme **Chiudi l’evento**. In **Messaggi** potete inviare messaggi con visibilità **Solo team** o **Condiviso con il segnalante**.

## Cosa significano gli stati
- **Bozza / Inviato / Restituito / Classificato / In indagine / Monitoraggio azioni / Chiuso / Ritirato:** il ciclo di vita dell'evento.
- **Gravità — Non classificato / Non valutato:** non esiste ancora una classificazione umana. La **Stima del segnalante** è solo un suggerimento. **Severity policy: not configured – human classification only** indica che il sistema non ricava alcuna gravità.
- **La necessità di indagine è UNKNOWN:** non è stata presa alcuna decisione; non significa "no" e blocca la chiusura.
- **Azione — Aperta / Completata / Verificata:** Completato non significa Verificato, e Verificato non significa Efficace.
- **Efficacia — In attesa / Efficace / Non efficace / Non conclusivo / Nessuna azione / Non visibile:** l'efficacia è una revisione umana per ogni azione ed è separata dalla chiusura. **Nessuna azione** non significa efficace.
- Riepilogo **Eventi di sicurezza aperti**: **Eventi di gravità alta aperti**, **Revisione richiesta** o **Nessun evento di gravità alta aperto**. È solo un avviso; non fornisce alcuna decisione di prontezza.
- **Riservato (protetto):** l'identità del segnalante è in un archivio protetto e non viene mostrata ai lettori.
- **Acquisito offline / In attesa di sincronizzazione / Salvata su questo dispositivo. Non ancora inviata.** In coda non significa inviato.

## Cosa non si può fare / attenzione
- Un segnalante non può eseguire il triage della propria segnalazione. Un autore dell'indagine, chi l'ha inviata o il segnalante non possono effettuare la revisione indipendente.
- Solo il responsabile dell'azione segnala il completamento. Né il responsabile né chi ha completato possono verificare; nessuno tra responsabile, chi ha completato e verificatore può valutare l'efficacia. Sono record separati di persone diverse.
- Le cause radice sono dichiarazioni umane; il sistema non ne propone mai una.
- Una segnalazione inviata non può mai essere ritirata né cancellata dal suo autore.
- In una segnalazione riservata l'identità viene aperta solo in base alla necessità di sapere, da chi ha il permesso di lettura dell'identità e sceglie una **Finalità**; ogni lettura viene registrata con la sua finalità. La segnalazione anonima richiede il canale pubblico di raccolta, che non è configurato.
- Una revisione Non conclusivo può essere ripetuta; Efficace e Non efficace sono definitive; un'azione che non ha funzionato richiede una nuova azione di follow-up.

## Se qualcosa va storto
- **Il server ha rifiutato il comando:** completate gli elementi mancanti o bloccanti elencati sotto il messaggio.
- **Il record è stato modificato nel frattempo. Ricarica e verifica prima di riprovare.** Premete **Ricarica**.
- **L’esito è sconosciuto (connessione persa).** Usate **Riprova lo stesso comando**; non verrà applicato due volte.
- **Questa azione non ti è consentita.** Controllate il vostro ruolo, la nave e il contesto dell'oggetto; se necessario contattate l'amministratore del tenant (13.03 §10).
- **The device key is unavailable** oppure **Online re-authorisation is needed:** i dati acquisiti sono conservati; autenticatevi di nuovo quando siete online. In caso di errore dell'applicazione, contattate il supporto con il correlation ID, la schermata, l'ora e i passaggi eseguiti (13.03 §10).

## Pagine correlate
