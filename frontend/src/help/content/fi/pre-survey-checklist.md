---
key: pre-survey-checklist
title: Katsastusta edeltävä tarkistuslista
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Katsastusta edeltävä tarkistuslista seuraa todistuksen katsastuksen valmistelutehtäviä vastuuhenkilön, määräpäivän, näytön ja riippumattoman ihmisen todennuksen kanssa. Se vastaa kysymykseen: ”Mitkä tämän katsastuksen valmistelukohdat ovat avoinna, millä on näyttö ja mitkä toinen henkilö on todentanut?” Valmius on neuvoa-antava eikä koskaan ole katsastuksen hyväksyntä (13.03 §1, §8).

## Kuka sitä käyttää
- **Päällikkö, konepäällikkö / tekninen tarkastaja:** aloittaa tarkistuslistan, vastaa kohtiin ja liittää näytön.
- **Todentaja:** vertaa jonkun toisen vastaamia kohtia niiden näyttöön ja todentaa tai hylkää ne (13.03 §1).
- Pelkällä lukuoikeudella käyttäjät näkevät vain ilmoituksen ”Vain luku roolillesi.”

## Ennen kuin aloitat
- Katselu edellyttää todistusten lukuoikeutta. Tarkistuslistan aloittaminen ja siihen vastaaminen edellyttää kyseisen aluksen katsastussuunnitteluoikeutta; todentaminen edellyttää katsastusten tarkastusoikeutta.
- Valitse oikea alus kohdasta **Valitse alus** (13.03 §1).
- Todistuksen katsastusikkuna tulisi ilmoittaa sivulla [Todistusten ohjauskeskus](/app/certificate-command-center).
- Sivu edellyttää ajantasaista verkkolukua. Offline-tilassa näet ilmoituksen ”Offline-tila: ajantasainen verkkohaku tarvitaan.”

## Vaihe vaiheelta
1. Katso aluksen listat kohdasta **Tarkistuslistat** tai paina sivulla [Katsastussuunnittelu](/app/survey-planner) rivin painiketta **Avaa tarkistuslista** tai **Aloita tarkistuslista**.
2. Uutta listaa varten käytä kohtaa **Valitse todistus**, valitse **Pohja** ja paina **Luo tarkistuslista**. Pohja on aloituspohja, ei hyväksytty luettelo.
3. Täytä kohdassa **Katsastussuunnitelman tiedot** kentät **Suunniteltu alku**, **Suunniteltu loppu**, **Suunniteltu paikka (teksti)** ja tarvittaessa **Satamakäynti**.
4. Anna kohdassa **Kohdat** kullekin kohdalle **Vastuurooli**, **Määräpäivä**, **Vastaus** ja tarvittaessa **Havainto** ja **Kommentti**. Linkitä näyttö painikkeella **Lisää näyttö** ja paina sitten **Tallenna**. Jokainen tallennus luo uuden version.
5. Todentajana valitse jonkun toisen vastaamissa kohdissa **Päätös**-kohdasta **Todenna** tai **Hylkää**, anna syy ja paina **Kirjaa varmennus**.
6. Seuraa kohtia **Valmius** ja **Esteet**.

## Mitä tilat tarkoittavat
- Kohdan vastaus: **Avoin**, **Valmis**, **Vaatii huomiota**, **Estetty**, **Ei sovellu**. Kohta, jonka vastaus on tuntematon, pysyy tilassa **Avoin**; sitä ei koskaan lasketa tilaksi ”Ei sovellu” tai ”Valmis”.
- Todennus: **Odottaa**, **Todennettu (ihminen)**, **Hylätty**. Vastattu kohta pysyy tilassa **Odottaa**, kunnes se on todennettu.
- Valmius: **Estetty**, **Keskeneräinen**, **Valmisteltu, odottaa ihmisen vahvistusta**. Paraskin tila odottaa ihmisen vahvistusta; järjestelmä ei koskaan sano ”katsastus hyväksytty”.
- **Kriittinen / Pakollinen / Valinnainen:** kohdan tärkeys. Ratkaisemattomat kriittiset ja pakolliset kohdat heikentävät valmiutta ja näkyvät kohdassa **Esteet**.
- **Matka: tuntematon (ei matkamoduulia):** matkaan sopivuutta ei voi vielä arvioida, ja se pysyy tuntemattomana.

## Mitä et voi tehdä / varoitus
- Et voi todentaa kohtaa, johon olet itse vastannut: ”Vastasit tähän kohtaan; toisen henkilön on tarkastettava se.”
- Kohdan vastaaminen tilaan **Valmis** ei todenna sitä; Completed ≠ Verified. Jokainen todennuspäätös on sidottu juuri siihen vastaukseen ja näyttöön, jonka näet.
- Valmius ei ole katsastuksen hyväksyntä, varaus eikä todistuksen voimassaolo. Koordinointi luokituslaitoksen, lippuvaltion ja viranomaisten kanssa on ulkoista, eikä sitä simuloida täällä.
- Pohjan kohdat eivät ole hyväksytty luettelo; valtuutettu henkilö arvioi, sopivatko ne alukselle.

## Jos jokin menee vikaan
- **Tarkistuslista on muuttunut avaamisen jälkeen. Lataa se uudelleen ennen muokkausta.** Paina **Lataa nykyinen uudelleen**.
- **Lopputulos ei ole vielä tiedossa. Uusi yritys lähettää saman pyynnön.** Käytä painiketta **Lähetä sama pyyntö uudelleen**.
- **Kirjaamattomat kohdat:** korjaa luetellut kohdat ja tallenna uudelleen.
- **Sinulla ei ole pääsyä tähän näkymään.** Tarkista roolisi, aluksesi ja kohteen käyttöalue; ota tarvittaessa yhteyttä Tenant Adminiin (13.03 §10).
- Jos kohtaa ei voi todentaa, tarkista sen näyttö ja lähde ja ohjaa se todentajalle (13.03 §10).

## Liittyvät sivut
