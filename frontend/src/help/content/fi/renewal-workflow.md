---
key: renewal-workflow
title: Todistuksen uusiminen
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Todistuksen uusiminen -sivu kirjaa todistuksen uusimisen valmistelun ja sisäisen työnkulun vaihe vaiheelta: valmistelu, hakemus, viranomaisen odottaminen, myönnetty korvaava todistus, todennus ja päättäminen. Se vastaa kysymykseen: ”Missä vaiheessa tämän todistuksen uusiminen on, ja kuka tekee seuraavan vaiheen?” Se on vain sisäisen työnkulun tietue; se ei osoita oikeudellista voimassaoloa eikä viranomaisen hyväksyntää (13.03 §1, §8).

## Kuka sitä käyttää
- **Päällikkö, konepäällikkö / tekninen tarkastaja:** valmistelee uusimisluonnoksen ja kirjaa vaatimukset ja hakemuksen.
- **Osoitettu uusimisen vastuutaho:** vie työnkulkua eteenpäin. Kyseessä on rooli, jota hoitavat jäsenet, joilla on uusimisen vastuutahon oikeus; työnkulkua ei voi avata eikä validoida ilman sellaista.
- **Todentaja ja päättäjä:** vertaa myönnettyä korvaavaa todistusta sen lähteeseen ja todentaa sen, ja eri henkilönä päättää uusimisen (13.03 §1, §8).

## Ennen kuin aloitat
- Katselu edellyttää todistusten lukuoikeutta. Luonnoksen valmistelu edellyttää todistusten kirjoitusoikeutta ja uusimisen kirjoitusoikeutta kyseisellä aluksella; todentaminen edellyttää uusimisen todennusoikeutta; päättäminen edellyttää uusimisen päättämisoikeutta.
- Valitse ensin oikea alus kohdassa **Valitse alus** (13.03 §1).
- Uusittavalla todistuksella on oltava voimassa oleva tallennettu versio. Korvattua todistusta ei voi uusia.
- Työnkulun vaiheet edellyttävät toimivaa yhteyttä. Offline-tilassa näet ilmoituksen ”Olet offline-tilassa. Vaiheet eivät ole käytettävissä ennen yhteyden palautumista.”

## Vaihe vaiheelta
1. Avaa **Uusimistietue** kohdasta **Kirjatut uusimisluonnokset**. Uutta uusimista varten siirry painikkeella **Valitse uusittava todistus** sivulle [Todistusten ohjauskeskus](/app/certificate-command-center) ja paina tietueen kohdalla **Valmistele uusiminen**.
2. Täytä **Todistuksen uusimislomake**: **Ilmoitettu uusimisen eräpäivä**, katsastusikkuna, **Suunniteltu paikka**, **Osoitettu uusimisen vastuutahon rooli** sekä **Katsastuksen vaatimusilmoitus**, **Asiakirjojen vaatimusilmoitus** ja **Toimenpiteiden vaatimusilmoitus**. Jätä arvo, jota et tiedä, tilaan **Tuntematon**.
3. Tallenna luonnos painikkeella **Tallenna todentamaton luonnos**.
4. Valitse kohdassa **Uusimisen työnkulku** roolillesi avoin vaihe kohdasta **Käytettävissä olevat vaiheet**, esimerkiksi **Vahvista vaatimukset täytetyiksi**, **Kirjaa hakemus** tai **Odota viranomaisen päätöstä**. Täytä pakolliset kentät ja paina **Tallenna vaihe**.
5. Kun viranomainen myöntää uuden todistuksen, käytä toimintoa **Kirjaa myönnetty korvaava** ja valitse se. Sen omaa asiakirjaa käytetään näyttönä.
6. Todentaja käyttää toimintoa **Varmenna korvaava** ja asettaa kohdat **Myöntäjä täsmää**, **Todistuksen numero täsmää**, **Kohde täsmää**, **Päivämäärät täsmäävät** ja **Soveltamisala täsmää** lähteeseen vertaamalla.
7. Eri henkilö päättää uusimisen painikkeella **Päätä ja korvaa**. Jos viranomainen hylkäsi hakemuksen, käytä toimintoa **Kirjaa viranomaisen hylkäys**; jos valmistelusta luovutaan, käytä toimintoa **Peru valmistelu**.

## Mitä tilat tarkoittavat
- **Luonnos / Valmistelussa:** uusimista valmistellaan; lomaketta voi muokata.
- **Valmis hakemukseen / Hakemus kirjattu / Odottaa viranomaista:** hakemusvaihe. Viranomaisen päätös tehdään järjestelmän ulkopuolella.
- **Myönnetty, varmentamaton:** korvaava todistus on kirjattu, mutta kukaan ei ole vielä verrannut sitä lähteeseensä.
- **Tarkastajan varmentama:** todentaja kirjasi vertailun; uusiminen ei ole vielä päätetty.
- **Päätetty:** sisäinen työnkulku on suljettu ja korvaava todistus korvaa alkuperäisen. Se ei ole päätös todistuksen oikeudellisesta voimassaolosta.
- **Peruttu / Hylätty:** työnkulku pysähtyi.
- Vaatimus voi olla **Valmis / Ei vaadita / Tuntematon**. **Tuntematon** ei tarkoita ”Ei vaadita”; palvelin ei vie työnkulkua eteenpäin tuntemattoman vaatimuksen kohdalla.

## Mitä et voi tehdä / varoitus
- Henkilö, joka kirjasi myönnetyn korvaavan todistuksen tai syötti kyseisen korvaavan todistuksen, ei voi todentaa sitä. Päättäjän on oltava eri henkilö kuin todentaja, myönnetyn korvaavan todistuksen kirjannut henkilö ja korvaavan todistuksen laatija.
- Todennus ja päättäminen ovat yksinomaan ihmisen päätöksiä; Completed ≠ Verified.
- Suunnitteluilmoitukset eivät osoita suoritettuja katsastuksia, hyväksyttyjä hakemuksia, matkakelpoisuutta eikä todennusta.
- Korvaavan todistuksen luonnoslinkit eivät korvaa alkuperäistä. Vanha todistus sulkeutuu vain päättämisvaiheen kautta jäljitettävällä korvauslinkillä (13.03 §1).
- Jokainen vaihe kirjataan muuttumattomana versiona; historia säilyy kohdassa **Kirjattu versiohistoria**.
- Ilman nimenomaisesti osoitettua vastuutahoa järjestelmä ei koskaan oletusarvoisesti valitse ylläpitäjää.

## Jos jokin menee vikaan
- **Roolillesi ei ole vaiheita tässä tilassa.** Seuraava vaihe kuuluu toiselle roolille; tarkista oikeutesi ja nimityksesi ja ota tarvittaessa yhteyttä Tenant Adminiin (13.03 §10).
- **The renewal changed after it was loaded.** tai **Tämä ei ole nykyinen versio.** Paina **Lataa uudelleen** ja tarkista nykyinen tila.
- **Lopputulos on tuntematon. Uudelleenyritys lähettää saman pyynnön uudelleen.** Käytä painiketta **Yritä samaa pyyntöä uudelleen**.
- **Hylätty tehtävien eriyttämisen vuoksi: kirjasit tämän uusimisen aiemman vaiheen, joten tämän vaiheen on tehtävä eri henkilö.** Tietue ei muuttunut; uudelleenlataus ei auta. Jätä vaihe toiselle valtuutetulle henkilölle.
- **Vaihetta ei tallennettu. Tarkista tiedot ja yritä uudelleen.** Tarkista pakolliset kentät ja täsmäysvalinnat.
- Jos korvaavaa todistusta ei voi todentaa, tarkista asiakirja ja lähteen alkuperäketju ja ohjaa se todentajalle (13.03 §10).

## Liittyvät sivut
