---
key: departure-signoff
title: Prontezza alla partenza e firma del Comandante
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: [1, 2, 3, 4, 5, 6, 7, 9]
reviewed: false
---
## A cosa serve questa pagina
Questa pagina mostra una **valutazione di prontezza a titolo consultivo** per la nave selezionata e il periodo di partenza previsto, e registra la decisione del Comandante nominato. Risponde alla domanda: "Che cosa dicono, per questo periodo, le fonti relative a equipaggio, aspetti tecnici, requisiti statutari, emergenza ed eventi di sicurezza, e perché?" La decisione di autorizzare, sospendere o fermare la partenza spetta al **Comandante** (13.03 §2). Il sistema non autorizza mai una partenza; una firma registrata non è un nulla osta alla partenza.

## Chi la usa
- **Comandante:** per esaminare la valutazione e l’elenco "Perché", quindi registrare Autorizza, Sospendi o Nega con una motivazione (13.03 §2).
- **Operazioni a terra / Sovrintendente tecnico:** per seguire in sola lettura la valutazione e le decisioni registrate del Comandante. Il personale a terra non può registrare una decisione al posto del Comandante.

## Prima di iniziare
- Nella barra di contesto in alto controlla che sia selezionata la **società corretta**. La pagina elenca solo le navi nel tuo ambito (13.03 §1).
- Per vedere la pagina ti serve l’accesso in lettura alla prontezza alla partenza.
- Il solo permesso di decisione non basta per registrare una decisione: ti serve anche una **nomina attuale come Comandante** per questa nave. Senza di essa la pagina indica "You hold no current Master appointment for this ship. Decisions are read-only."

## Passo dopo passo
1. Scegli la nave dall’elenco "Scegli una nave".
2. In **Periodo di partenza previsto** compila "Data di partenza" e "Fine del viaggio" e premi **Valuta la prontezza**.
3. Nella riga **Valutazione** leggi il badge di stato, la modalità "Attuale" o "Previsione", "Valutato il" e "Valido fino al".
4. Nella tabella delle categorie controlla i risultati di Equipaggio, Tecnico, Statutario, Emergenza ed Eventi di sicurezza.
5. Leggi l’elenco **Perché**. Ogni riga mostra un’osservazione non superata, il suo codice motivo e la sua fonte. Esamina una riga inattesa nella pagina della fonte.
6. Se la valutazione non è attuale e Pronto, leggi **Perché Autorizza non è disponibile**. Indica quando il periodo è una previsione, elenca le categorie che non sono ancora superate con i loro risultati e spiega quale record chiuderebbe le lacune di fonte di Emergenza (nessuna fonte di esercitazioni 61.02 ancora collegata) e Statutario (nessun insieme di certificati richiesti definito). Spiega solo le categorie del server; non modifica il controllo.
7. In **Decisioni del Comandante** vedi le decisioni registrate in precedenza e la loro validità.
8. Se sei il Comandante, in **Registra la decisione del Comandante** scegli Autorizza, Sospendi o Nega, seleziona la tua nomina in "Nomina", scrivi un "Motivo" (10–2000 caratteri) e premi **Registra**.
9. Usa **Aggiorna** per leggere di nuovo la valutazione.

## Cosa significano gli stati
- **Pronto:** nessuna fonte registrata ha un’osservazione non superata. È solo un’informazione; la decisione di partenza resta del Comandante.
- **Condizionato:** almeno una fonte segnala una condizione. Per questa valutazione Autorizza non viene offerto.
- **Non pronto:** almeno una fonte restituisce "Non superato". Il superamento delle altre categorie non lo compensa.
- **Revisione necessaria:** almeno un input è "Sconosciuto". Una persona autorizzata deve esaminarlo.
- **Superato / Condition / Non superato / Sconosciuto:** il risultato della categoria nella tabella delle categorie. **Sconosciuto** significa che la fonte non ha potuto essere letta o non ha dati; non significa "Superato" né "Pronto". In questa versione non è collegata alcuna fonte di preparazione alle emergenze per la categoria Emergenza, quindi quella riga mostra Sconosciuto. Statutario può restare Sconosciuto anche quando ogni certificato registrato è valido, perché nessuna fonte definisce l’insieme dei certificati richiesti per la nave. Statutario include anche i segnali di affidabilità dei dati (DATA_TRUST_*); senza accesso in lettura all’affidabilità dei dati Statutario mostra Sconosciuto.
- **Previsione:** il periodo non include la data odierna. È solo una previsione di pianificazione e non può essere autorizzata.
- **Attuale / Stale — inputs changed:** la validità di una decisione registrata. Quando gli input cambiano, la decisione precedente diventa non aggiornata e non viene eliminata.

## Cosa non si può fare / attenzione
- Il sistema non autorizza mai una partenza e non mostra alcuno stato come nulla osta alla partenza. Una decisione registrata è la registrazione umana del Comandante nominato.
- Autorizza può essere registrato solo per una valutazione READY attuale; ogni categoria deve essere superata. Poiché in questa versione la categoria Emergenza resta Sconosciuto, la valutazione non può raggiungere Pronto e Autorizza non viene offerto; **Perché Autorizza non è disponibile** ne mostra il motivo. Anche con Pronto la decisione di autorizzazione è del Comandante; il sistema non approva nulla. "Sospendi e Nega possono sempre essere registrati con una motivazione."
- Per ogni decisione è obbligatoria una motivazione. Le decisioni registrate non vengono modificate; una nuova decisione è un nuovo record.
- Non puoi registrare una decisione mentre la valutazione non è aggiornata o sei offline; aggiorna prima.

## Se qualcosa va storto
- **Sei offline:** la valutazione potrebbe non essere aggiornata e la firma è disabilitata. Aggiorna quando torni online (13.03 §10).
- **Non hai accesso alla prontezza alla partenza per questa nave:** controlla il tuo ruolo e il contesto della nave; se necessario contatta il tuo amministratore del tenant.
- **Prontezza inattesa:** apri l’elenco "Perché"; se una fonte sembra errata, sottoponila al responsabile del controllo o a un esperto della materia.
- **L’esito è incerto:** "Riprova" invia lo stesso comando. Non scrivere una nuova decisione.
- **La cronologia delle decisioni è cambiata / La valutazione è cambiata:** usa "Ricarica", esamina la valutazione attuale, poi decidi.
- **Il comando non è stato accettato:** segnalalo al supporto con uno screenshot, l’ora e i passaggi eseguiti.

## Pagine correlate
