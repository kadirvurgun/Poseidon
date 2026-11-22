---
key: pms-command-center
title: PMS-ohjauskeskus
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
PMS-ohjauskeskus kokoaa valitun aluksen huollon **poikkeukset** yhdelle näytölle. Se vastaa kysymykseen: ”Onko tällä aluksella myöhässä olevia kriittisiä töitä, avoimia työmääräyksiä, vikahälytyksiä tai kriittisten varaosien puutteita, ja miksi?” Sivu on neuvoa-antava poikkeusnäkymä; se ei ole kelpoisuus-, valmius- tai turvallisuuspäätös. Kunnossa olevia kohteita ei listata, vaan ne vain lasketaan.

## Kuka sitä käyttää
- **Konepäällikkö / tekninen tarkastaja:** seuraa myöhässä olevia töitä, avoimia työmääräyksiä, vikahälytyksiä ja kriittisten varaosien puutteita (13.03 §5).
- **Päällikkö:** näkee PMS- ja vikatietueiden vaikutuksen operatiivisiin rajoituksiin (13.03 §2).

## Ennen kuin aloitat
- Sivun näkeminen edellyttää kaluston lukuoikeutta (`equipment.read`).
- Kukin osio riippuu oman moduulinsa lukuoikeudesta. Esimerkiksi ilman vikojen lukuoikeutta osio **Kuntohälytykset** näyttää ”Käyttöoikeus tarvitaan: ei näytetä”.
- Valitse ensin alus. Ilman alusta sivu näyttää vain luettelon **Valitse alus.**

## Vaihe vaiheelta
1. Valitse alus luettelosta. Jos käyttöalueellasi ei ole yhtään alusta, sivu näyttää ”Laajuudessasi ei ole aluksia.”
2. Lue ylhäältä merkki **Poikkeusyhteenveto** ja aika **Viimeksi päivitetty**.
3. Käy läpi osiot: **Myöhässä olevat kriittiset työt**, **Erääntyvät, myöhästyneet ja tarkastettavat työt**, **Avoimet työmääräykset**, **Kuntohälytykset** ja **Kriittisten varaosien puutteet**.
4. Avaa kohteen kohdalla **Miksi** nähdäksesi kirjatut syykoodit ja mahdollisen **Havaittu**-ajan.
5. Siirry lähteeseen painikkeella **Avaa lähdetietue**. Vikakohde avaa sivun [Viat ja väliaikaiset toimenpiteet](/app/defects), työmääräyskohde avaa sivun [Työmääräyskeskus](/app/work-order-center).
6. Lue tiedot uudelleen painamalla **Päivitä**.

## Mitä tilat tarkoittavat
- **Estetty / Tarkastus vaaditaan / Huomio:** poikkeusyhteenvedon tila. Yksityiskohdat ovat osioiden kohteissa.
- **Ei avoimia poikkeuksia (neuvoa-antava):** luetut lähteet eivät palauttaneet avoimia poikkeuksia. Tämä ei ole valmius- tai kelpoisuustulos.
- **Kriittinen / Huomio / Tarkastus vaaditaan:** kohteen vakavuus.
- **Lähde ei käytettävissä: tuntematon, ei nolla:** osion lähdettä ei voitu lukea. Tuntematon ei tarkoita ”nolla”, ”ei ongelmia” eikä ”Valmis”.
- **Käyttöoikeus tarvitaan: ei näytetä:** sinulla ei ole oikeutta tähän osioon; sisältöä tai määrää ei näytetä.
- **Tästä lähteestä ei poikkeuksia:** osio luettiin eikä se palauttanut poikkeuksia. Kunnossa olevien kohteiden määrä näytetään erikseen.
- **Stale data / Ajantasaisuus tuntematon:** kohteen taustalla oleva lukema ei ole ajantasainen, tai sen ajantasaisuus on tuntematon.

## Mitä et voi tehdä / varoitus
- Sivu ei muuta yhtään tietuetta; se vain lukee. Työmääräykset, viat ja varaosat käsitellään omilla sivuillaan.
- Sivu ei koskaan ilmoita alusta ”valmiiksi” tai ”turvalliseksi”. Lähtöpäätös kuuluu päällikölle (13.03 §2).
- Vika lasketaan suljetuksi vain ihmisen kirjaaman sulkemistietueen kautta ja vain sille ilmoitusversiolle, joka suljettiin. Jos ilmoitus muuttuu myöhemmin, kohde palaa tarkastettavaksi.
- Offline-tilassa näytetyt tiedot voivat olla vanhoja, eikä **Päivitä** ole käytettävissä.

## Jos jokin menee vikaan
- **The record could not be loaded:** tarkista yhteytesi ja käytä painiketta **Lataa uudelleen**.
- **You do not have the server permission for this action or record:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Unexpected exception:** avaa **Miksi** ja käy lähdetietue läpi vastuullisen omistajan tai asiantuntijan (SME) kanssa (13.03 §10).
- **Application error:** ilmoita tukeen korrelaatiotunnus (Correlation ID), näyttö, aika ja vaiheet (13.03 §10).

## Liittyvät sivut
