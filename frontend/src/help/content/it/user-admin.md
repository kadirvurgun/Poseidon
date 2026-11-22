---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## A cosa serve questa pagina
La pagina Users and permissions gestisce le **appartenenze** (membership) nella vostra area di lavoro. Risponde alla domanda: "Cosa può vedere e fare questa persona, e su quali navi?" L'account stesso (nome utente, password, MFA) risiede nel provider di identità centrale (IdP), non in POSEIDON. Questa pagina **invita** quell'account a un'appartenenza e concede pacchetti di ruoli, permessi e ambito delle navi (13.03 §9). Un nuovo membro viene invitato: l'invito non concede nulla finché la persona non accede e lo accetta.

## Chi la usa
- **Tenant administrator:** assegna utente, ruolo, ambito di nave e di oggetto secondo il minimo privilegio, invita nuovi membri e gestisce deleghe e sospensioni (13.03 §9).
- Gli altri utenti con accesso in lettura alle appartenenze possono vedere l'elenco ma non modificarlo.
- La **persona invitata** apre solo il link di invito; non vede mai questa pagina.

## Prima di iniziare
- La visualizzazione richiede la lettura delle appartenenze; la modifica richiede la gestione delle appartenenze più la gestione a livello di tenant. Con il solo accesso in lettura vedete "You can view but not change memberships."
- La persona ha bisogno di un account nel provider di identità. **Expected IdP subject (optional)** limita l'invito a quel solo account; senza di esso, chiunque acceda con il link può accettarlo.
- Prevedete un canale fidato per consegnare il link di invito (ad esempio il sistema di messaggistica della vostra organizzazione). POSEIDON non invia e-mail.
- Potete concedere solo i permessi che possedete voi stessi. La vostra appartenenza viene modificata da un altro amministratore.
- L'amministrazione delle appartenenze richiede una connessione attiva al server; offline non si può modificare nulla.

## Passo dopo passo
1. Consultate i membri nell'elenco **Members**. Filtrate per **Stato** (ad esempio **Invited**) e **Role bundle**, poi premete **Apply**. Per cercare, inserite un prefisso dell'id di appartenenza o di account oppure il soggetto IdP esatto e premete **Cerca**.
2. Aprite un membro con **Open**. Esaminate **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** e **Delegations**.
3. Per invitare un membro premete **Add member**. Controllate **IdP issuer**, inserite facoltativamente **Expected IdP subject (optional)**, scegliete almeno un **Role bundle**, aggiungete se necessario **Individual permissions** e impostate l'ambito (intero tenant o almeno una nave). Poi premete **Create membership**.
4. Il membro si apre come **Invited** con l'**Invitation link**. Premete **Copy invitation link** e consegnate il link. Il link viene mostrato solo ora; funziona una sola volta e scade dopo 24 ore.
5. Se il link è andato perso o è scaduto, premete **Create a new invitation link**; il link precedente smette di funzionare. Per ritirare l'invito premete **Revoke invitation**.
6. La persona invitata apre il link, accede, controlla cosa concede l'invito e preme **Accept invitation**. Dopo un nuovo accesso lavora con la concessione che avete impostato; il membro mostra allora **Active** e **Invitation accepted**.
7. Per modificare la concessione di un membro esistente premete **Edit grant**, poi **Save grant**. Ogni salvataggio crea una nuova revisione della concessione; le revisioni precedenti restano in **Grant history**.
8. Per un passaggio di consegne temporaneo usate **Delegate from this member** e compilate il delegato, **Valid from**, **Valid until**, **Motivo** e **Delegated permissions**.
9. Per interrompere l'accesso usate **Suspend** in **Lifecycle**; **Resume** lo riapre. **Revoke membership** è definitivo.

## Cosa significano gli stati
- **Invited:** l'appartenenza attende la persona invitata. Non concede nulla e non riserva l'account da nessuna parte, nemmeno in un'altra organizzazione.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** lo stato dell'ultimo link di invito. Un link scaduto, sostituito o revocato non può mai essere accettato.
- **Active / Suspended / Revoked:** il ciclo di vita dell'appartenenza. La richiesta successiva di un membro sospeso o revocato viene negata.
- **Grant source — Managed here:** la concessione è stata data in questa pagina e può essere modificata qui.
- **Grant source — Operator configuration:** la concessione proviene dalla configurazione del server (bootstrap/break-glass) e non può essere modificata qui.
- **Grant source — No grant:** esiste un record ma non concede nulla.
- **Not held by you / You do not hold every permission of this bundle:** non potete concedere questo permesso o pacchetto perché non lo possedete.
- **Saved. The change takes effect on the member's next request.** L'accesso di un membro invitato inizia al primo accesso successivo all'accettazione.

## Cosa non si può fare / attenzione
- Non potete concedere un permesso, una nave o un accesso a livello di tenant che non possedete.
- Non potete modificare la vostra appartenenza; l'ultimo amministratore attivo non può essere rimosso.
- Un account può avere un'appartenenza accettata in una sola organizzazione. Un invito non sposta mai un account che appartiene già altrove; la persona invitata vede allora solo che l'invito non può essere accettato.
- Trattate il link di invito come una chiave: chiunque lo possieda e acceda può accettarlo, a meno che abbiate inserito un soggetto atteso. Non pubblicatelo in canali condivisi. Trasmettete sempre il link intero: la parte segreta dopo **#** viene letta solo dal browser e non raggiunge mai il server né i suoi log.
- I permessi di decisione, verifica e approvazione non sono mai in un pacchetto. Solo un amministratore che li possiede può concederli, uno per uno. Le azioni vincolate a una designazione (ad esempio decisioni sul rimorchiatore o sulla partenza) richiedono anche una designazione dell'operatore. La concessione di un pacchetto di ruoli non designa nessuno come Comandante o verificatore.
- Non potete approvare una delega di cui siete fonte o delegato. Una delega non supera mai l'autorità attuale della fonte e non è mai a livello di intero tenant.
- POSEIDON non memorizza password, conserva solo un'impronta del link di invito e non mostra mai i soggetti IdP nell'elenco.

## Se qualcosa va storto
- **This identity is configured by the operator, already a member here or already invited here.** Aprite invece il membro esistente, oppure revocate prima il vecchio invito.
- **La persona invitata vede "This invitation cannot be accepted":** il link è scaduto, è già stato usato, è stato sostituito o revocato, appartiene a un altro account, oppure il suo account appartiene già a un'organizzazione. Controllate lo stato di **Invitation** e, se necessario, premete **Create a new invitation link**.
- **Il link non è stato copiato:** selezionate il campo del link e copiatelo manualmente.
- **Exceeds your own authority:** scegliete un pacchetto o un ambito più ristretto, oppure chiedete a un amministratore che possiede il permesso.
- **The record changed meanwhile:** premete **Reload current state** e ripetete la modifica.
- **The outcome is unknown. Retry with the same request or reload.** Potete reinviare in sicurezza lo stesso comando.
- **Not the trusted identity provider:** controllate l'emittente. Se l'account proviene da un altro IdP, contattate l'amministratore di sistema (13.03 §10).

## Pagine correlate
