---
key: defects
title: Difetti e salvaguardie
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina dei difetti e delle misure temporanee contiene i **difetti registrati, le restrizioni e le misure temporanee dichiarate** della nave selezionata. Risponde alla domanda: "Quanto è critico questo difetto, quale salvaguardia o restrizione operativa è registrata, l’azione correttiva è stata verificata e che cosa impedisce ancora la chiusura?" Ogni passaggio è una registrazione umana; la pagina non concede alcuna autorità di sicurezza o operativa.

## Chi la usa
- **Direttore di macchina / Sovrintendente tecnico:** per registrare la salvaguardia o la restrizione e l’impatto sulla prontezza di un difetto critico, e per seguire l’azione correttiva (13.03 §5).
- **Comandante:** per vedere l’impatto di un difetto in termini di restrizioni operative (13.03 §2).
- **Macchinista / equipaggio:** per dichiarare un difetto riscontrato (13.03 §6).

## Prima di iniziare
- La lettura richiede `defect.read` e `equipment.read`. Dichiarazioni e registrazioni del ciclo di vita richiedono `defect.write`.
- La verifica richiede `defect.verify`, l’efficacia `defect.effectiveness` e la chiusura `defect.close`, ciascuna con un utente umano che abbia effettuato l’accesso.
- Scegli la nave corretta da **Seleziona una nave o un rimorchiatore**.

## Passo dopo passo
1. Scegli la nave. Se necessario, filtra **Dichiarazioni registrate** con **Filtra le osservazioni di scadenza restituite** e apri una dichiarazione.
2. Per un nuovo difetto premi **Nuova dichiarazione di difetto**, compila i campi e premi **Salva dichiarazione non valutata**. Le bozze incomplete possono essere salvate.
3. Su una dichiarazione salvata premi **Avvia ciclo di vita** nella sezione **Ciclo di vita del difetto**.
4. Usa **Registra valutazione** per scegliere la criticità e scrivere la dichiarazione (almeno 10 caratteri).
5. Usa **Registra salvaguardia o restrizione** e scegli **Salvaguardia** o **Restrizione operativa** come **Tipo di misura**. Per una misura temporanea inserisci la scadenza (UTC).
6. Usa **Registra azione correttiva** per descrivere il lavoro svolto.
7. Una persona autorizzata che non ha registrato alcuna azione correttiva registra **Registra verifica** e poi **Registra efficacia**.
8. Quando non restano **Ostacoli alla chiusura**, una persona autorizzata che a sua volta non ha registrato alcuna azione correttiva registra **Chiudi e rimetti in servizio**. Le registrazioni compaiono in **Registrazioni del ciclo di vita**.

## Cosa significano gli stati
- **Detect / Assess / Safeguard / Action / Verify / Effectiveness:** la fase del ciclo di vita.
- **Chiuso (decisione umana registrata):** la chiusura è una decisione umana; non è un risultato verde "riparato".
- **Si applica la regola critica:** un difetto critico non può essere chiuso senza una salvaguardia.
- **Active / Expired — blocking / Lifted / Ended at closure:** lo stato di una salvaguardia o restrizione. Una misura temporanea scaduta deve essere rinnovata o revocata.
- **Critical defect without an active safeguard / Criticality unknown and no safeguard:** avvisi. Questi difetti compaiono anche in **Avvisi di condizione** nel [centro di comando PMS](/app/pms-command-center).
- **Sconosciuto:** la criticità non è stata valutata. Sconosciuto non significa "Bassa" né "Non critico".

## Cosa non si può fare / attenzione
- Se hai registrato l’azione correttiva, non puoi registrare verifica, efficacia o rimessa in servizio; richiedono una propria autorità e una persona diversa.
- Verificato, Efficace e Chiuso sono registrazioni umane separate. Una fase o misura dichiarata non autorizza il servizio, non verifica l’efficacia e non consente la chiusura.
- Se la dichiarazione cambia dopo la verifica, la chiusura è bloccata e il record torna in revisione.
- La criticità dell’asset non viene copiata come valutazione di questo difetto.
- Offline le fonti attuali dei difetti non sono disponibili; non esiste archiviazione offline.

## Se qualcosa va storto
- **Qualcun altro ha modificato questo record:** usa **Ricarica** per leggere il record attuale e registra di nuovo.
- **L’esito del salvataggio è sconosciuto:** **Riprova lo stesso comando** invia lo stesso comando.
- **Non hai il permesso del server per questa azione o questo record:** controlla il tuo ruolo e il contesto della nave; se necessario contatta il tuo amministratore del tenant (13.03 §10).
- **Ostacoli alla chiusura:** completa la registrazione mancante nell’elenco. Se un record non può essere verificato, controlla l’evidenza con il verificatore (13.03 §10).

## Pagine correlate
