---
key: voice-event-draft
title: Bozza di evento vocale
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Bozza di evento vocale trasforma una breve nota parlata o scritta in **suggerimenti di bozza** per una segnalazione di evento di sicurezza. Risponde alla domanda: "Il tipo di evento, il luogo, l'ora, cosa è successo e l'azione immediata sono stati ricavati correttamente da ciò che ho detto o scritto?" I suggerimenti provengono da regole locali fisse, ciascuna ricondotta alle vostre parole con un grado di confidenza. Una persona modifica e conferma la bozza; nulla viene inviato, chiuso o segnalato automaticamente (13.03 §1, §6).

## Chi la usa
- **Equipaggio / OOW / macchinista e Comandante:** prendono una nota rapida sul campo e avviano una segnalazione di evento di sicurezza (13.03 §2, §6).
- **DPA / HSEQ:** osservano **Usabilità misurata (questa nave)**. Questi valori sono un'osservazione operativa, non un'affermazione di accuratezza.

## Prima di iniziare
- Scegliete una nave nel vostro ambito. La creazione di una bozza richiede il permesso di scrittura delle bozze vocali e l'ambito di modifica su quella nave.
- Il passaggio di una bozza confermata a una bozza di evento di sicurezza richiede anche il permesso di segnalazione degli eventi di sicurezza.
- Privacy: l'audio non viene conservato. Il parlato usa solo un motore locale su questa installazione; se non ne è installato alcuno, digitate o incollate la nota. Evitate nomi e dati sanitari personali.
- Offline non viene inviato nulla.

## Passo dopo passo
1. Premete **Nuova nota vocale o scritta**.
2. Se è configurato un motore locale premete **Registra con motore locale**, poi **Ferma e trascrivi**. Altrimenti digitate una breve nota in **Testo della nota**: cosa, dove, quando, cosa avete fatto.
3. Controllate **Data della nota**. Serve solo a risolvere parole come "oggi" o "ieri".
4. Premete **Crea suggerimenti di bozza**.
5. Controllate ogni campo: **Tipo di evento**, **Quando è accaduto (ora locale)**, **Posizione a bordo**, **Dettaglio del luogo**, **Cosa è successo**, **Azione immediata**. Sotto ogni campo vedete la confidenza del suggerimento, la regola e la parte corrispondente della vostra nota. Leggete anche **Note delle regole**.
6. Correggete i suggerimenti errati e premete **Salva modifiche**. Quando **Ancora richiesto prima della conferma:** è vuoto premete **Conferma bozza**. La conferma registra che avete controllato ogni campo; non invia né chiude un evento.
7. Dopo la conferma, se è stata creata una bozza di evento di sicurezza, seguite **Apri la bozza dell'evento di sicurezza** fino alla pagina Eventi di sicurezza e near miss e completatela e inviatela lì. Se non ne è ancora stata creata alcuna e possedete il permesso, usate **Crea ora la bozza dell'evento di sicurezza**.
8. Se la nota non è un evento o è errata, scegliete un **Motivo**, aggiungete se necessario **Nota (obbligatoria per Altro)** e premete **Rifiuta bozza**.

## Cosa significano gli stati
- **Bozza (non confermata):** nessuna persona ha ancora confermato i suggerimenti.
- **Confermata da una persona:** i campi sono stati controllati; questa non è una segnalazione di evento di sicurezza e non è stato inviato nulla.
- **Rifiutata:** il motivo del rifiuto viene registrato per la valutazione.
- **Suggested, unchanged / Changed by you / Entered by you / Empty:** l'origine di un campo. Suggested, unchanged è un suggerimento che una persona deve ancora controllare.
- **Typed or pasted / Local speech engine:** come è stata inserita la nota. La modifica del testo proveniente dal motore locale lo trasforma in una nota scritta.
- Le note delle regole come **Ora senza giorno; nessuna data ipotizzata** indicano informazioni mancanti; il sistema non indovina il valore mancante.
- Una nota come **2 missing**: quanti campi obbligatori della bozza sono ancora vuoti nell'elenco.

## Cosa non si può fare / attenzione
- I suggerimenti sono solo una bozza; una confidenza non è una garanzia di correttezza. Controllate voi stessi ogni campo prima di confermare.
- Questa pagina non invia, non chiude e non segnala un evento di sicurezza. La segnalazione si invia nella pagina Eventi di sicurezza e near miss.
- Se vengono menzionate evidenze, allegatele voi; la pagina non allega alcuna evidenza.
- **Conferma bozza** non è disponibile finché ci sono modifiche non salvate; salvatele prima.
- Senza il permesso di segnalazione degli eventi di sicurezza non viene creata alcuna bozza di evento di sicurezza da una bozza confermata.

## Se qualcosa va storto
- **Speech capability unavailable: no local engine is configured.** oppure **Transcription was not completed.** Digitate o incollate la nota.
- **Accesso al microfono non concesso.** Controllate il permesso del browser o digitate la nota.
- **The record changed meanwhile.** Premete **Ricarica** e ripetete la modifica.
- **You are offline. Nothing was sent.** Quando siete online usate **Riprova lo stesso comando**.
- **Non hai l’autorizzazione per questa azione.** Controllate il vostro ruolo e il contesto della nave; se necessario, contattate l'amministratore del tenant (13.03 §10).

## Pagine correlate
