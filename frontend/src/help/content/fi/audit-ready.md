---
key: audit-ready
title: Auditointivalmis jäljitys
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Auditointivalmis jäljitys -sivu näyttää sinulle osoitetut aluksen jäljitysketjut vaihe vaiheelta: vaatimus → kontrolli → todiste → todentaminen → päätös, tiloineen, aikaleimoineen ja ilmoitettuine lähdeviitteineen. Se vastaa kysymykseen: ”Mihin vaatimukseen tämä todiste perustuu, kuka sen todensi ja mikä päätös kirjattiin?” Kirjattu ketju on ihmisen tekemä kirjaus, ei hyväksyntä eikä valmiuslausunto (13.03 §4, §8).

## Kuka sitä käyttää
- **Regulatory / Assurance -käyttäjä:** tarkistaa, ettei vaatimus→kontrolli→todiste→todentaminen-ketjuun jää orpoa tietuetta (13.03 §8).
- **DPA / HSEQ:** esittää havainto→toimenpide→todentaminen-polun todisteineen valmistautuessaan auditointiin, PSC-tarkastukseen tai katsastukseen (13.03 §4).

## Ennen kuin aloitat
- Sivu vaatii auditoinnin lukuoikeuden ja ihmisjäljityksen lukuoikeuden sekä voimassa olevan jäljityksen lukijan nimityksen kyseiselle alukselle. Pelkkä oikeus ei riitä; ilman nimitystä yhtään ketjua ei listata.
- Valitse oikea alus. Luettelo näyttää vain sinulle osoitetut ketjut.
- Sivu latautuu verkkoyhteyden kautta. Offline-tilassa näytetyt tiedot voivat olla vanhentuneita.

## Vaihe vaiheelta
1. Valitse alus kentässä **Alus** (**Valitse alus**).
2. Avaa ketju kohdasta **Osoitetut ketjut**.
3. Laajenna kohdassa **Jäljityksen vaiheet** jokainen vaihe (vaatimus, kontrolli, todiste, todentaminen, päätös) ja tarkista **Tila**, **Kirjattu**, **Versio**, **Sisällön tiiviste** ja **Lähdeviite (ilmoitettu, todentamaton)**.
4. Jos **Avoimet kohdat** -kohtia on listattu, välitä koodit kyseisen tietueen omistajalle.
5. Kun luettavia osoitettuja ketjuja on, kokoa paketti kohdassa **Auditointipaketti** painikkeella **Kokoa uusi paketti**, avaa se kohdasta **Kootut paketit** ja käytä tarvittaessa painiketta **Vie JSON**.
6. Päivitä tiedot painamalla otsikon **Yritä uudelleen** -painiketta.

## Mitä tilat tarkoittavat
- **Tallennettu ketju — vain ihmisen kirjaus, ei hyväksyntä:** jokainen vaihe on kirjattu. Tämä ei julista alusta tai operaatiota valmiiksi tai vaatimustenmukaiseksi.
- **Tarkastus vaaditaan:** vähintään yksi vaihe puuttuu, on tuntematon tai ei ole käytettävissä. Tuntematonta tai lukukelvotonta syötettä ei koskaan näytetä valmiina.
- **Ei kirjattu:** vaiheelle ei ole tietuetta; esimerkiksi todentamista tai päätöstä ei ole vielä kirjattu.
- **Ei käytettävissä / UNKNOWN:** tietuetta ei voi nyt lukea; sen sisältöä ei oleteta.
- **Vanhentunut: syöte korvattiin tai vanheni; tarkastus vaaditaan.** Todiste tai lähde muuttui myöhemmin; tarkista ketju uudelleen.
- **Viitettä ei annettu:** lähdeviitettä ei syötetty; viite on ilmoitus, ei todennettu tosiasia.
- Todennettu todiste ei tarkoita, että toimenpide oli vaikuttava: Todennettu ≠ Vaikuttava (13.03 §4).
- **Auditointipaketti:** ”Järjestelmän kokoama; ei virallinen tallenne”. Nimitystesi ulkopuoliset kohteet vain lasketaan, niitä ei näytetä; **Korvattu (säilytetty jäljitettävyyttä varten)** -kohteita ei koskaan käsitellä voimassa olevina.

## Mitä et voi tehdä / varoitus
- Tällä sivulla et voi muokata tietueita, todentaa tai päättää; sivu on vain luku -tilassa. Nimitetty henkilö kirjaa todentamisen ja päätökset niiden omilla sivuilla.
- Et näe ketjuja, joita ei ole osoitettu sinulle; piilotettuja ketjuja ei koskaan nimetä, ja osoitetut mutta ei käytettävissä olevat ketjut näytetään vain lukumääränä.
- Eheys- ja kontekstitiivisteet eivät ole valtuutus eivätkä hyväksyntä.

## Jos jokin menee vikaan
- **Sinulla ei ole oikeutta tähän näkymään.** Tarkista roolisi, aluksesi ja nimityskontekstisi; ota yhteyttä Tenant Admin -käyttäjään (13.03 §10).
- **Tälle alukselle ei ole osoitettu sinulle ketjuja.** Tarkista, että valitsit oikean aluksen; kysy vastuuhenkilöltä, tarvitaanko nimitys.
- **Jäljitystä ei voitu ladata.** Paina **Yritä uudelleen**.
- **Ketjua ei löytynyt tai se ei ole enää näkyvissä sinulle.** Nimityksesi on voinut päättyä tai tietue on poistunut käyttöalueeltasi.
- Jos tietuetta ei voida todentaa, tarkista sen todisteet ja alkuperä ja ota tarvittaessa yhteyttä todentajaan (13.03 §10).

## Liittyvät sivut
