---
key: audit-ready
title: Tracciabilità pronta per l’audit
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La pagina Tracciabilità pronta per l’audit mostra, fase per fase, le catene di tracciabilità a te assegnate per una nave: Requisito → controllo → evidenza → verifica → decisione, con stati, marche temporali e riferimenti della fonte dichiarati. Risponde alla domanda: "Su quale requisito si basa questa evidenza, chi l’ha verificata e quale decisione è stata registrata?" Una catena registrata è una registrazione umana, non un’approvazione né una dichiarazione di prontezza (13.03 §4, §8).

## Chi la usa
- **Utente Regulatory / Assurance:** controlla che nella catena Requisito→Controllo→Evidenza→Verifica non resti alcun record orfano (13.03 §8).
- **DPA / HSEQ:** mostra la traccia Rilievo→Azione→Verifica con le relative evidenze durante la preparazione di audit, PSC o ispezioni (13.03 §4).

## Prima di iniziare
- La pagina richiede la lettura dell’audit e la lettura della tracciabilità umana, oltre a una nomina attuale come lettore della tracciabilità per quella nave. Il permesso da solo non basta; senza la nomina non viene elencata alcuna catena.
- Scegli la nave corretta. L’elenco mostra solo le catene a te assegnate.
- La pagina si carica tramite una connessione attiva. Offline, i dati mostrati possono non essere aggiornati.

## Passo dopo passo
1. Scegli una nave in **Nave** (**Seleziona una nave**).
2. Apri una catena da **Catene assegnate**.
3. In **Fasi della tracciabilità** espandi ogni fase (Requisito, Controllo, Evidenza, Verifica, Decisione) ed esamina **Stato**, **Registrato il**, **Revisione**, **Hash del contenuto** e **Riferimento della fonte (dichiarato, non verificato)**.
4. Se sono elencati **Punti aperti**, trasmetti i codici al responsabile del record interessato.
5. Se esistono catene assegnate leggibili, compila un pacchetto in **Pacchetto di audit** con **Compila nuovo pacchetto**, aprilo da **Pacchetti compilati** e, se necessario, usa **Esporta JSON**.
6. Premi **Riprova** nell’intestazione per aggiornare i dati.

## Cosa significano gli stati
- **Catena registrata — solo registrazione umana, non un’approvazione:** ogni fase è registrata. Ciò non dichiara la nave o l’operazione pronta o conforme.
- **Revisione necessaria:** almeno una fase manca, è sconosciuta o non disponibile. Un input sconosciuto o illeggibile non viene mai mostrato come completo.
- **Non registrato:** per quella fase non esiste alcun record; ad esempio non è ancora stata registrata alcuna verifica o decisione.
- **Unavailable / UNKNOWN:** il record non può essere letto ora; il suo contenuto non viene presunto.
- **Non aggiornato: un input è stato sostituito o è scaduto; revisione necessaria.** Un’evidenza o una fonte è cambiata in seguito; esamina di nuovo la catena.
- **Riferimento non fornito:** non è stato inserito alcun riferimento della fonte; un riferimento è una dichiarazione, non un fatto verificato.
- Un’evidenza verificata non significa che l’azione sia stata efficace: Verificato ≠ Efficace (13.03 §4).
- **Pacchetto di audit:** "Compilazione di sistema; non è un documento ufficiale". Gli elementi al di fuori delle tue nomine vengono solo contati, non mostrati; gli elementi **Sostituito (conservato per la tracciabilità)** non sono mai trattati come attuali.

## Cosa non si può fare / attenzione
- In questa pagina non puoi modificare record, verificare o decidere; è di sola lettura. Verifiche e decisioni sono registrate dalla persona nominata nelle rispettive pagine.
- Non puoi vedere le catene non assegnate a te; le catene nascoste non vengono mai nominate e le catene assegnate non disponibili sono mostrate solo come conteggio.
- Gli hash di integrità e di contesto non costituiscono autorità né approvazione.

## Se qualcosa va storto
- **Non sei autorizzato per questa vista.** Controlla il tuo ruolo, la nave e il contesto della nomina; contatta il Tenant Admin (13.03 §10).
- **Nessuna catena assegnata a te per questa nave.** Controlla di aver selezionato la nave corretta; chiedi alla persona responsabile se è necessaria una nomina.
- **Impossibile caricare la tracciabilità.** Premi **Riprova**.
- **Catena non trovata o non più visibile.** La tua nomina potrebbe essere terminata oppure il record è uscito dal tuo ambito.
- Se un record non può essere verificato, controlla la sua evidenza e la sua provenienza e, se necessario, contatta il verificatore (13.03 §10).

## Pagine correlate
