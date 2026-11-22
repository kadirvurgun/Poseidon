---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## Mihin tätä sivua käytetään
Users and permissions -sivulla hallitaan työtilasi **jäsenyyksiä**. Se vastaa kysymykseen: ”Mitä tämä henkilö voi nähdä ja tehdä, ja millä aluksilla?” Itse tili (käyttäjänimi, salasana, MFA) on keskitetyssä identiteetintarjoajassa (IdP), ei POSEIDONissa. Tämä sivu **kutsuu** kyseisen tilin jäsenyyteen ja myöntää roolipaketit, oikeudet ja alusten käyttöalueen (13.03 §9). Uusi jäsen kutsutaan: kutsu ei myönnä mitään ennen kuin henkilö kirjautuu sisään ja hyväksyy sen.

## Kuka sitä käyttää
- **Tenant administrator:** määrittää käyttäjän, roolin, aluksen ja kohteen käyttöalueen vähimpien oikeuksien periaatteella, kutsuu uusia jäseniä ja hallitsee delegointeja ja keskeytyksiä (13.03 §9).
- Muut käyttäjät, joilla on jäsenyyksien lukuoikeus, voivat tarkastella luetteloa mutta eivät muuttaa sitä.
- **Kutsuttu henkilö** avaa vain kutsulinkin; hän ei koskaan näe tätä sivua.

## Ennen kuin aloitat
- Tarkastelu vaatii jäsenyyksien lukuoikeuden; muuttaminen vaatii jäsenyyksien hallintaoikeuden sekä koko vuokralaisen laajuisen hallintaoikeuden. Pelkällä lukuoikeudella näet viestin ”You can view but not change memberships.”
- Henkilöllä on oltava tili identiteetintarjoajassa. **Expected IdP subject (optional)** rajaa kutsun tähän yhteen tiliin; ilman sitä kuka tahansa linkillä sisäänkirjautuva voi hyväksyä sen.
- Suunnittele luotettava kanava kutsulinkin luovuttamiseen (esimerkiksi organisaatiosi oma viestintäkanava). POSEIDON ei lähetä sähköpostia.
- Voit myöntää vain oikeuksia, jotka sinulla itselläsi on. Oman jäsenyytesi muokkaa toinen ylläpitäjä.
- Jäsenyyksien hallinta vaatii toimivan palvelinyhteyden; offline-tilassa mitään ei voi muuttaa.

## Vaihe vaiheelta
1. Näet jäsenet luettelossa **Members**. Suodata kohdilla **Status** (esimerkiksi **Invited**) ja **Role bundle** ja paina **Apply**. Hae syöttämällä jäsenyyden tai tilin tunnuksen alku tai tarkka IdP-subject ja paina **Search**.
2. Avaa jäsen painikkeella **Open**. Tarkastele kohtia **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** ja **Delegations**.
3. Kutsu jäsen painamalla **Add member**. Tarkista **IdP issuer**, syötä halutessasi **Expected IdP subject (optional)**, valitse vähintään yksi **Role bundle**, lisää tarvittaessa **Individual permissions** ja aseta käyttöalue (koko vuokralainen tai vähintään yksi alus). Paina sitten **Create membership**.
4. Jäsen avautuu tilassa **Invited**, ja näkyvissä on **Invitation link**. Paina **Copy invitation link** ja luovuta linkki. Linkki näytetään vain nyt; se toimii kerran ja vanhenee 24 tunnin kuluttua.
5. Jos linkki katosi tai vanheni, paina **Create a new invitation link**; edellinen linkki lakkaa toimimasta. Peru kutsu painamalla **Revoke invitation**.
6. Kutsuttu henkilö avaa linkin, kirjautuu sisään, tarkistaa, mitä kutsu myöntää, ja painaa **Accept invitation**. Kirjauduttuaan uudelleen hän työskentelee asettamallasi myönnöllä; jäsen näkyy silloin tilassa **Active** ja **Invitation accepted**.
7. Muuta olemassa olevan jäsenen myöntöä painamalla **Edit grant** ja sitten **Save grant**. Jokainen tallennus luo uuden myönnön version; vanhemmat versiot säilyvät kohdassa **Grant history**.
8. Käytä tilapäiseen luovutukseen kohtaa **Delegate from this member** ja täytä delegoitava, **Valid from**, **Valid until**, **Reason** ja **Delegated permissions**.
9. Lopeta pääsy painikkeella **Suspend** kohdassa **Lifecycle**; **Resume** avaa sen uudelleen. **Revoke membership** on lopullinen.

## Mitä tilat tarkoittavat
- **Invited:** jäsenyys odottaa kutsuttua henkilöä. Se ei myönnä mitään eikä varaa tiliä mistään, ei myöskään mistään muusta organisaatiosta.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** viimeisimmän kutsulinkin tila. Vanhentunutta, korvattua tai peruttua linkkiä ei voi koskaan hyväksyä.
- **Active / Suspended / Revoked:** jäsenyyden elinkaari. Keskeytetyn tai perutun jäsenen seuraava pyyntö evätään.
- **Grant source — Managed here:** myöntö annettiin tällä sivulla, ja sitä voi muuttaa täällä.
- **Grant source — Operator configuration:** myöntö tulee palvelimen määrityksestä (bootstrap/break-glass), eikä sitä voi muokata täällä.
- **Grant source — No grant:** kirjaus on olemassa, mutta se ei myönnä mitään.
- **Not held by you / You do not hold every permission of this bundle:** et voi myöntää tätä oikeutta tai pakettia, koska sinulla ei ole sitä.
- **Saved. The change takes effect on the member's next request.** Kutsutun jäsenen pääsy alkaa hyväksymisen jälkeisestä seuraavasta kirjautumisesta.

## Mitä et voi tehdä / varoitus
- Et voi myöntää oikeutta, alusta tai koko vuokralaisen laajuista pääsyä, jota sinulla ei ole.
- Et voi muuttaa omaa jäsenyyttäsi; viimeistä aktiivista ylläpitäjää ei voi poistaa.
- Tilillä voi olla hyväksytty jäsenyys vain yhdessä organisaatiossa. Kutsu ei koskaan siirrä tiliä, joka jo kuuluu muualle; kutsuttu henkilö näkee silloin vain, ettei kutsua voi hyväksyä.
- Käsittele kutsulinkkiä kuin avainta: kuka tahansa, jolla se on ja joka kirjautuu sisään, voi hyväksyä sen, ellet syöttänyt odotettua subjectia. Älä julkaise sitä jaetuilla kanavilla. Välitä aina koko linkki: merkin **#** jälkeinen salainen osa luetaan vain selaimessa eikä se koskaan päädy palvelimelle tai sen lokeihin.
- Päätös-, varmennus- ja hyväksyntäoikeudet eivät koskaan ole paketissa. Vain ylläpitäjä, jolla ne on, voi myöntää ne yksitellen. Nimitykseen sidotut toimenpiteet (esimerkiksi hinaaja- tai lähtöpäätökset) vaativat lisäksi operaattorin nimityksen. Roolipaketin myöntäminen ei nimitä ketään päälliköksi eikä varmentajaksi.
- Et voi hyväksyä delegointia, jossa olet lähde tai delegoitava. Delegointi ei koskaan ylitä lähteen nykyistä valtuutta eikä ole koskaan koko vuokralaisen laajuinen.
- POSEIDON ei tallenna salasanoja, säilyttää kutsulinkistä vain sormenjäljen eikä koskaan näytä IdP-subjecteja takaisin luettelossa.

## Jos jokin menee vikaan
- **This identity is configured by the operator, already a member here or already invited here.** Avaa sen sijaan olemassa oleva jäsen tai peru ensin vanha kutsu.
- **Kutsuttu henkilö näkee viestin ”This invitation cannot be accepted”:** linkki on vanhentunut, käytetty, korvattu tai peruttu, kuuluu toiselle tilille, tai hänen tilinsä kuuluu jo johonkin organisaatioon. Tarkista **Invitation**-tila ja paina tarvittaessa **Create a new invitation link**.
- **Linkkiä ei kopioitu:** valitse linkkikenttä ja kopioi se käsin.
- **Exceeds your own authority:** valitse suppeampi paketti tai käyttöalue tai pyydä apua ylläpitäjältä, jolla oikeus on.
- **The record changed meanwhile:** paina **Reload current state** ja tee muutoksesi uudelleen.
- **The outcome is unknown. Retry with the same request or reload.** Voit turvallisesti lähettää saman komennon uudelleen.
- **Not the trusted identity provider:** tarkista issuer. Jos tili tulee toisesta IdP:stä, ota yhteyttä järjestelmänvalvojaan (13.03 §10).

## Liittyvät sivut
