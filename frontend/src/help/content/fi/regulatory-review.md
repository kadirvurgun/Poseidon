---
key: regulatory-review
title: Asiantuntijan sääntelytarkastus
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Asiantuntijan sääntelytarkastus kirjaa kampanjat, joissa ihmisasiantuntija tarkastaa laivan tai hinaajan lähde- ja sovellettavuuskohdat. Se vastaa kysymykseen: ”Mitkä tämän aluksen lähde- ja vaatimuskohdat valtuutettu tarkastaja on vahvistanut, ja mitkä ovat yhä avoinna ja seurannassa?” Se on vain ihmisen tekemän tarkastuksen tietue: järjestelmä ei hyväksy yhtään vaatimusta, ei aktivoi yhtään sääntöä tai lupaa eikä väitä luokitus- tai tyyppihyväksyntää (13.03 §8).

## Kuka sitä käyttää
- **Regulatory / Assurance -käyttäjä:** avaa tarkastuskampanjoita, kirjaa kohtien ratkaisut ja sulkee kampanjan (13.03 §8).
- **Nimitetty tarkastaja (SME):** vahvistaa tai ottaa seurantaan kohtia oman lähdetoimivaltansa rajoissa.
- **DPA / HSEQ:** seuraa avoimia ja seuraamattomia kohtia (13.03 §4).

## Ennen kuin aloitat
- Katselu edellyttää sääntelytarkastuksen lukuoikeutta. Kampanjoiden avaaminen ja sulkeminen edellyttää hallintaoikeutta; ratkaisujen kirjaaminen edellyttää ratkaisuoikeutta ja voimassa olevaa tarkastajan nimitystä tähän laajuuteen.
- Valitse välilehdillä **Tarkastuksen laajuus** joko **Alus** tai **Hinaaja** ja avaa sitten oikea alus kohdasta **Valitse alus** (13.03 §1).
- Lähteiden tulisi olla rekisteröity virallisiksi ja versioiduiksi sivulla [Lähdeluonnokset](/app/sources) (13.03 §8).
- Offline-tilassa tiedot voivat olla vanhentuneita, ja komennot ovat poissa käytöstä.

## Vaihe vaiheelta
1. Valitse laajuus ja alus. Jos kampanjaa ei ole, paina **Avaa tarkastuskampanja**.
2. Tarkista kohdasta **Lähteiden omistajuusmatriisi** kunkin lähdeviranomaisen luokka, avoimien ja vahvistettujen kohtien määrät sekä vastuuroolit.
3. Valitse kohta kohdasta **Tarkistettavat kohteet**.
4. Täytä lomakkeessa **Kirjaa tarkastusratkaisu** kentät **Tarkastajan nimitys**, **Ratkaisu**, **Lähteen viranomainen** ja **Vastuurooli**. Jos kohdalla ei ole rekisteröityä lähdettä, anna **Virallisen lähteen viite** ja **Lähdeversio / painos**. Avoinna pysyvälle kohdalle anna **Seurattavan tiketin tunnus**.
5. Kirjoita kenttään **Tarkastajan huomautus** 10–2000 merkin perustelu ja paina **Kirjaa**.
6. Kun kaikki kohdat on ratkaistu, valitse kohdassa **Sulje kampanja** **Sulkemisen tulos**, kirjoita **Sulkemisen syy** ja paina **Sulje kampanja**.

## Mitä tilat tarkoittavat
- Kampanja: **Avoin**, **Tarkastuksessa**, **Suljettu avoimin kohdin**, **Suljettu**.
- Ratkaisu: **Vahvistettu**, **Ei sovellu (vahvistettu)**, **Tarkastus vaaditaan**, **Monitulkintainen**. Kun kriittinen tieto on tuntematon, kohta pysyy tilassa **Tarkastus vaaditaan**; tuntematonta kohtaa ei koskaan lasketa tilaksi ”Ei sovellu”.
- **Ehdokas (vahvistamaton):** lähdettä ei ole vielä sidottu todennettuun viralliseen lähteeseen.
- **Vanhentunut — kiinnitys muuttui** ja **Aluksen profiili muuttui:** lähde tai aluksen profiili muuttui ratkaisun jälkeen; kohta on tarkastettava uudelleen.
- **Seuraamattomat avoimet kohdat:** avoimet kohdat, joilla ei ole seurattavaa tikettiä. Niin kauan kuin määrä on suurempi kuin nolla, kampanjaa ei voi sulkea.
- Luokat **Lainsäädäntö**, **Luokitussääntö**, **Yhtiön SMS** ja **Ohje — ei lakia** eivät koskaan korvaa toisiaan (13.03 §8).

## Mitä et voi tehdä / varoitus
- Kampanjan avannut henkilö ei voi kirjata siihen ratkaisuja; ilman tarkastajan nimitystä ratkaisut ovat vain luku -tilassa.
- Tarkastaja, jonka pätevyyttä ei ole todennettu, voi ottaa avoimia kohtia seurantaan mutta ei voi vahvistaa. Vanhentunutta kohtaa ei voi vahvistaa.
- Vahvistaminen edellyttää rekisteröityä lähdettä tai virallisen lähteen viitettä painoksineen. Älä jätä ketjuun Vaatimus → Hallintakeino → Näyttö → Todennus orpoa tai lähteetöntä tietuetta (13.03 §8).
- **Suljettu** edellyttää, että jokainen kohta on vahvistettu; muuten sulje avoimin kohdin niin, että jokainen niistä on seurannassa.
- Älä ilmoita huomautuksessa luokitus- tai tyyppihyväksyntää. Hinaajan laajuudessa mikään hinausraja ilman hallittua lähdettä ei ole oikeudellinen oletus.
- Ratkaisuja kirjaavat vain ihmiskäyttäjät; tekoäly- ja automaatiotilit eivät voi kirjata niitä.

## Jos jokin menee vikaan
- **Tietue muuttui. Lataa uudelleen ja tarkista ennen jatkamista.** Paina **Lataa uudelleen**.
- **Lopputulos on epävarma. Uudelleenyritys lähettää saman komennon.** Käytä painiketta **Yritä uudelleen**.
- **Tarkastussäännöt hylkäsivät tämän komennon.** Tarkista, onko kohta vanhentunut, sekä lähdeviite ja seurattava tiketti.
- **Sinulla ei ole pääsyä tämän aluksen sääntelytarkastukseen.** tai **Sinulla ei ole oikeutta tähän toimintoon.** Tarkista roolisi ja käyttöalueesi; ota tarvittaessa yhteyttä Tenant Adminiin (13.03 §10).
- Jos sovellettavuus näyttää odottamattomalta, tarkista lähde- ja kontekstisyötteet ja keskustele hallintakeinon omistajan tai asiantuntijan (SME) kanssa (13.03 §10).

## Liittyvät sivut
