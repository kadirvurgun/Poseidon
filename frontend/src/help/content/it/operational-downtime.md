---
key: operational-downtime
title: Fermo operativo
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
Questa pagina registra le **interruzioni operative dichiarate** di una nave (fermo) insieme alla loro provenienza dalle fonti. Risponde alla domanda: "Quale interruzione è stata dichiarata su questa nave, quando, per quale causa e collegata a quale difetto o ordine di lavoro, e a che punto sono la sua revisione e la sua chiusura?" La pagina non decide il diritto al loss of hire, l'idoneità al servizio né alcun nulla osta di sicurezza.

## Chi la usa
- **Direttore di macchina / Sovrintendente tecnico:** per registrare un fermo causato da un guasto tecnico o da manutenzione e collegarlo alla cronologia del difetto o dell'ordine di lavoro (13.03 §5).
- **Comandante:** per seguire l'impatto operativo del fermo e rivederlo quando designato (13.03 §2).
- **Revisore designato (Comandante, Primo Ufficiale, Direttore di macchina, Fleet Manager, DPA):** per verificare, restituire per correzione o chiudere un record di fermo.

## Prima di iniziare
- Nella barra di contesto in alto, controllate che sia selezionata la **società corretta**; la pagina elenca solo le navi del vostro ambito.
- La lettura richiede l'accesso in lettura ai fermi; aprire una bozza richiede l'accesso in scrittura. Le azioni di revisione richiedono anche l'accesso di revisione e una designazione di servizio valida per questa nave.
- Per aprire un fermo collegato, potete usare **Apri la bozza di fermo collegata** nella pagina di un difetto o di un ordine di lavoro.

## Passo dopo passo
1. Scegliete la nave dall'elenco "Seleziona una nave". Si apre l'elenco **Interruzioni operative registrate**.
2. Usate **Cerca cause in questa pagina** per restringere i record di questa pagina.
3. Per un nuovo record, premete **Nuova bozza di fermo**.
4. Per ogni campo scegliete prima lo stato del valore: **Mancante**, **Sconosciuto** o **Valore dichiarato**. Inserite un valore solo mentre è selezionato "Valore dichiarato".
5. In "Inizio dell’interruzione" e "Fine dell’interruzione", digitate un timestamp completo con i secondi e un offset UTC esplicito.
6. Premete **Salva bozza non valutata**. Una bozza incompleta può essere salvata.
7. In **Revisione e chiusura del fermo**, se necessario collegate le evidenze del claim con **Seleziona evidenza**, poi premete **Invia in revisione**.
8. Se siete un revisore designato, scegliete la vostra "Designazione" e usate **Verifica**, **Restituisci per correzione** o **Chiudi**.
9. Consultate le revisioni precedenti in **Cronologia immutabile delle bozze**.

## Cosa significano gli stati
- **Bozza / Revisione richiesta:** il contenuto registrato non è valutato. I motivi sono in "Perché questa bozza resta non valutata".
- **Non inviato / Aperto / In revisione:** lo stato del ciclo di vita del record.
- **Verificato (umano):** una persona designata diversa da chi ha registrato lo ha verificato. Completato non significa Verificato (13.03 §1).
- **Chiuso:** il fermo è stato chiuso. Una chiusura verificata non è un nulla osta di sicurezza.
- **Sconosciuto:** il valore non è stato dichiarato o non è stato possibile determinarlo. Sconosciuto non significa "No" né "Non applicabile". Per questo motivo loss of hire e chiusura restano "Sconosciuto".
- **Candidato Loss of Hire:** soltanto un candidato; non indica alcun diritto né importo.
- **Intervallo registrato:** un'osservazione tecnica non valutata; non sostituisce i minuti dichiarati.

## Cosa non si può fare / attenzione
- Chi ha registrato il fermo non può verificarlo né chiuderlo; deve farlo un'altra persona designata.
- Invio, verifica e chiusura non sono possibili finché mancano dati obbligatori.
- Collegare evidenze del claim non è una verifica.
- Nessuna descrizione, orario o decisione di chiusura viene copiata dalla fonte di appartenenza (difetto, ordine di lavoro); viene conservato solo il collegamento alla revisione esatta.
- Offline non è possibile leggere le fonti attuali e non c'è archiviazione offline; i vostri inserimenti restano solo nella pagina aperta.

## Se qualcosa va storto
- **L'esito del salvataggio è sconosciuto:** il vostro comando originale è protetto. Con lo stesso account, usate "Riprova il comando fermo esatto".
- **La bozza salvata è cambiata:** qualcun altro ha modificato il record. I vostri inserimenti sono conservati; usate "Leggi la bozza corrente" per ricaricare, oppure scartate.
- **I record di fermo o l'accesso attuale non sono disponibili:** controllate il vostro ruolo e il contesto della nave; se necessario, contattate l'amministratore del tenant (13.03 §10).
- **La fonte è cambiata:** usate la revisione attuale della fonte di appartenenza per una nuova bozza.
- **Comando non accettato:** segnalatelo al supporto con uno screenshot, l'ora e i passaggi eseguiti.

## Pagine correlate
