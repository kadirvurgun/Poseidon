---
key: risk-template-library
title: Libreria modelli di rischio
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## A cosa serve questa pagina
La libreria modelli di rischio gestisce, con revisioni, i modelli di valutazione dei rischi usati in tutto il tenant (tipo di lavoro, pericoli, controlli predefiniti, barriere richieste, fonti SMS e di requisiti) e i metodi di rischio (matrice). Risponde alla domanda: "Quale modello pubblicato si applica a questo tipo di lavoro, e quale revisione è in vigore?" La pagina è **solo un obiettivo di sviluppo**: una pubblicazione qui è un record umano HSEQ registrato, non un permesso di lavoro, un'accettazione del rischio da parte di un esperto né il superamento di un gate HSE (13.03 §4).

## Chi la usa
- **DPA / HSEQ:** nell'ambito della governance del rischio, preparano, rivedono e pubblicano revisioni dei modelli, barriere e la matrice di rischio (13.03 §4).
- **Comandante, Comandante del rimorchiatore ed equipaggio:** leggono i modelli pubblicati e inviano **Suggerimenti dell’equipaggio** per un modello.

## Prima di iniziare
- La lettura richiede il permesso di lettura dei modelli. Scrivere e pubblicare modelli richiede l'ambito a livello di tenant e il permesso di scrittura o di pubblicazione dei modelli. Inviare un suggerimento richiede solo il permesso di suggerimento sui modelli.
- Creare e pubblicare un metodo di rischio sono permessi separati e richiedono l'ambito a livello di tenant.
- I comandi sui rischi richiedono una connessione online attiva; offline nulla viene messo in coda o approvato.

## Passo dopo passo
1. Consultate i modelli nell'elenco. Ogni riga mostra lo stato, la revisione **Attuale** e il conteggio dei **Suggestions**. Aprite un modello.
2. Per un nuovo modello premete **Nuovo modello**. Compilate **Codice modello**, **Titolo**, **Tipo di lavoro**, **Ambito nave / rimorchiatore**, **Responsabile dell’approvazione**, **Motivo della modifica**, **Barriere richieste**, **Fonti SMS / requisiti** e **Pericoli**, poi premete **Salva bozza**.
3. Su una revisione in bozza premete **Invia in revisione**. Gli elementi mancanti vengono elencati come elementi bloccanti.
4. Chi pubblica inserisce **In vigore dal** e un **Motivo** di almeno 10 caratteri e preme **Pubblica (registrazione umana HSEQ)** o **Restituisci**.
5. Per modificare un modello pubblicato scrivete un motivo e premete **Avvia nuova revisione**; la revisione precedente non cambia.
6. L'equipaggio scrive un suggerimento in **Invia suggerimento** e lo invia. Una persona autorizzata lo esamina con **Accetta** o **Rifiuta** e un motivo.
7. In **Metodi di rischio (matrice)** usate **Nuovo metodo** per inserire **Codice metodo**, **Likelihood codes**, **Severity codes** e **Matrix cells**; un'altra persona lo pubblica con **Pubblica metodo**.

## Cosa significano gli stati
- **Bozza / In revisione / Restituito / Pubblicato / Sostituito:** il ciclo di vita della revisione del modello.
- **Attuale / Pianificato / Non efficace:** la vista di validità. L'ultima revisione pubblicata la cui data di efficacia è arrivata è **Attuale**; una con data futura è **Pianificato**. Una revisione futura non sostituisce mai in anticipo quella precedente.
- **Sconosciuto:** il campo non è compilato; un valore vuoto non viene mai riempito con un valore predefinito.
- **No published risk method: ratings stay not assessed.** Il sistema non offre alcuna matrice o soglia predefinita.

## Cosa non si può fare / attenzione
- Un autore di una revisione o chi l'ha inviata non può pubblicare la stessa revisione; la pubblicazione è un record umano separato (separazione dei compiti).
- La pubblicazione non è un permesso di lavoro, un'accettazione del rischio da parte di un esperto né un catalogo di produzione approvato.
- Un modello viene applicato a una valutazione dei rischi solo come **copia**; non porta con sé valutazioni numeriche, firme, conferme dei partecipanti, approvazioni né prese visione. Quando un modello cambia, le valutazioni esistenti non cambiano da sole.
- Una revisione pubblicata non viene mai modificata; per cambiarla avviate una nuova revisione.

## Se qualcosa va storto
- **Bloccato finché gli elementi elencati non sono completati.** Completate gli elementi bloccanti e inviate di nuovo.
- **This record changed elsewhere.** Premete **Ricarica** per vedere la revisione corrente.
- **Non sei autorizzato a questa azione (separazione dei compiti o ambito).** La pubblicazione deve essere eseguita da un'altra persona autorizzata che non è autrice della revisione; se manca un permesso contattate l'amministratore del tenant (13.03 §10).
- **Questa azione non è più valida per lo stato attuale. Ricarica.** Lo stato della revisione è cambiato.

## Pagine correlate
