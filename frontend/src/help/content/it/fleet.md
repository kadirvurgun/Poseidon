---
key: fleet
title: Flotta
roles: [Master, Tug Master, Chief Engineer, Shore Operations, Tenant Admin]
permissions: [directory.read, directory.write]
related: [today, companies, certificate-command-center, data-trust-center, departure-signoff]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## A cosa serve questa pagina
"Flotta" è l’**elenco di navi e rimorchiatori nel tuo ambito**. Risponde alla domanda: "A quali unità posso accedere, e dove si trovano il record, il profilo normativo, il passaporto di conformità e gli allegati di un’unità?" La pagina mostra il record dell’unità e le informazioni registrate; la prontezza operativa è valutata separatamente.

## Chi la usa
- **Comandante / Comandante del rimorchiatore / Direttore di macchina:** per selezionare la propria unità e passare alle sue aree di lavoro (attrezzature e manutenzione, ruolo equipaggio) (13.03 §2, §3, §5).
- **Operazioni a terra:** per trovare le unità della flotta ed esaminarne i record.
- **Tenant Admin:** per creare e modificare i record delle unità (13.03 §9).

## Prima di iniziare
- Nella barra di contesto in alto controlla che sia selezionata la **società corretta** (13.03 §1). L’elenco mostra solo le unità nel tuo ambito.
- Per vedere l’elenco ti serve l’accesso in lettura. "Nuovo record" e "Modifica" compaiono solo per le unità a cui hai accesso in scrittura.

## Passo dopo passo
1. Nelle schede di riepilogo in alto vedi il numero di unità elencate, navi, rimorchiatori e record modificabili in questa pagina. **I conteggi riguardano solo questa pagina**.
2. Digita in **Cerca per codice o nome**, se necessario scegli "Nave" o "Rimorchiatore" in **Tipo di nave** e premi **Cerca**.
3. Premi il nome di un’unità nella tabella. **Dettagli** mostra codice, nome e tipo; l’unità selezionata compare come "Nave corrente".
4. Apri il profilo dell’unità con **Profilo normativo**, oppure i suoi dati di avvio con **Configurazione iniziale della nave pilota**.
5. Usa **Passaporto digitale di conformità** per esaminare requisiti registrati, fonti, la revisione dell’applicabilità e gli ostacoli irrisolti. Usa **Torna alla nave** per tornare indietro.
6. Passa alle aree di lavoro dell’unità con **Importazione controllata**, **Attrezzature e manutenzione** o **Ruolo equipaggio**.
7. Nella sezione inferiore passa tra **Vessel attachments** e **Bozze dei documenti**.
8. Se hai accesso, aggiungi un’unità con **Nuovo record** o modificala con **Modifica** e salva. Usa **Cambia nave** per un’altra unità.

## Cosa significano gli stati
- **Read-only / Editable records:** il tuo accesso attuale in questa pagina. "Modifica" non è mostrato su un record di sola lettura.
- **Solo questa pagina. La prontezza operativa è valutata separatamente.:** il grafico di composizione delle unità non fornisce alcuna informazione sulla prontezza.
- **Revisione necessaria · freschezza non verificata:** le informazioni del passaporto sono lette da fonti registrate e non sono verificate. Il passaporto non è un’approvazione di conformità.
- **No current requirement rows returned:** ciò non stabilisce un insieme di obblighi vuoto.
- **Unknown / Unavailable:** il valore manca nella fonte o non è stato possibile leggerlo. Ciò non significa "Conforme" né "Pronto".

## Cosa non si può fare / attenzione
- Questa pagina non dice mai che un’unità è pronta per l’operazione o la partenza. La decisione di partenza spetta al Comandante e la decisione di rimorchio al Comandante del rimorchiatore.
- Un amministratore generico non può sostituire l’ammissione qualificata di una fonte né una decisione umana autorizzata.
- Se esci con modifiche non salvate ti viene chiesta una conferma; le modifiche vengono conservate finché non salvi o esci esplicitamente.
- I dati mostrati offline non sono verificati; riconnettiti e riprova.

## Se qualcosa va storto
- **Non hai accesso a questa area di lavoro:** controlla il tuo ruolo e il contesto dell’unità; se necessario contatta il tuo amministratore del tenant (13.03 §10).
- **No records found:** modifica la ricerca o il filtro del tipo di nave.
- **Accedi per continuare:** la tua sessione è terminata; usa di nuovo "Accedi".
- **The service is unavailable:** usa "Riprova"; se il problema persiste, segnalalo al supporto con uno screenshot, l’ora e i passaggi eseguiti.

## Pagine correlate
