---
key: notification-escalation
title: Notifiche
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina delle notifiche elenca le **eccezioni delle fonti configurate nell’applicazione** (manutenzione in scadenza, revisione della manutenzione necessaria, fonte della manutenzione cambiata o non disponibile) per la nave o il rimorchiatore selezionato. Risponde alla domanda: "Quali notifiche di manutenzione hanno raggiunto il mio ruolo, quanto sono gravi, quali sono state inoltrate al livello superiore perché nessuno ne ha preso visione in tempo, e su quale record di origine si basano?" La presa visione non verifica le evidenze e non approva le operazioni.

## Chi la usa
- **Direttore di macchina / Sovrintendente tecnico:** per seguire le scadenze di manutenzione e le richieste di revisione e passare al piano di origine (13.03 §5).
- **Comandante del rimorchiatore / Comandante / Equipaggio:** per vedere le notifiche indirizzate ai loro ruoli e prenderne visione (13.03 §6).

## Prima di iniziare
- Ti serve l’accesso in lettura a notifiche, manutenzione e attrezzature. La presa visione e il salvataggio delle preferenze richiedono permessi separati; senza di essi i relativi pulsanti non vengono mostrati.
- Scegli una nave o un rimorchiatore autorizzato da **Nave della notifica**.
- Offline non è possibile inviare alcun comando di presa visione o di preferenza.

## Passo dopo passo
1. Scegli una nave da **Nave della notifica**.
2. Leggi la riga delle regole: "Configurato · regole della fonte non verificate · consegna solo nell’applicazione".
3. Per ogni notifica leggi il titolo, il badge di gravità (**Informazione**, **Avviso**, **Critico**) e il badge di stato; controlla **Fonte / revisione**, **Data di scadenza** e **Data di creazione**. **Regola di instradamento** mostra il codice della regola.
4. Usa **Apri piano di origine** per passare al relativo piano di manutenzione.
5. Per registrare che hai visto una notifica premi **Registra presa visione** (solo nello stato **Aperta** o **Inoltrato al livello superiore**).
6. Se hai accesso alle preferenze, modifica **Silenzia notifiche consultive non obbligatorie** e **Ore di silenzio** (inizio, fine, fuso orario IANA) e premi **Salva preferenza consultiva**.
7. Usa **Notifiche precedenti** / **Altre notifiche** per le altre pagine e **Ricarica posta** per leggere di nuovo.

## Cosa significano gli stati
- **Aperta:** la notifica attende una presa visione.
- **Presa visione registrata:** una persona ha registrato di averla vista. Ciò non significa che la manutenzione sia stata eseguita né che le evidenze siano state verificate.
- **Inoltrato al livello superiore:** la notifica non ha ricevuto presa visione in tempo ed è stata indirizzata al ruolo di escalation della sua regola.
- **Source changed / Source unavailable:** il record di base è cambiato o non può essere confermato. La base registrata viene conservata; ciò non annulla né riattiva la notifica.
- **Instradamento obbligatorio · non silenziabile:** le preferenze non possono silenziare questa notifica.
- **Unknown (Due date):** la fonte non ha una data di scadenza. Ciò non significa che non ci sia nulla in scadenza.

## Cosa non si può fare / attenzione
- La presa visione non verifica le evidenze e non approva le operazioni.
- Gli instradamenti obbligatori e critici non possono essere silenziati dalle preferenze consultive. Le ore di silenzio ritardano solo gli avvisi facoltativi.
- Una pagina vuota non significa che sia tutto a posto. Se le regole o le fonti non sono disponibili, la pagina lo indica separatamente.
- Non esiste un nuovo tentativo automatico; un comando incerto viene conservato in questa scheda e le modifiche di contesto restano bloccate.

## Se qualcosa va storto
- **Messaggio "Outcome unknown":** usa **Riprova il comando esatto della notifica** per inviare di nuovo il comando conservato.
- **Messaggio "Notification version changed":** usa **Ricarica posta** per caricare l’elenco attuale.
- **Messaggio "Notification sources unavailable or access denied.":** controlla il tuo ruolo e il contesto della nave; se necessario contatta il tuo amministratore del tenant (13.03 §10).
- **Messaggio "The saved quiet hours could not be applied":** vengono mostrati tutti gli avvisi; controlla le impostazioni delle ore di silenzio.

## Pagine correlate
