---
key: engine-daily-log
title: Koneen päiväloki
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Koneen päiväloki säilyttää valitun laivan **konevahdin kirjaukset** luonnoksina kirjatun lähdeversion kanssa. Se vastaa kysymykseen: ”Mitä konevahti ilmoitti koneen tilasta ja tunneista, polttoaineen ja voiteluöljyn kulutuksesta, makeasta vedestä, kriittisistä mittauksista, hälytyksistä, vioista, polttoaineen vastaanotosta, pilssistä ja ympäristökontrolleista, ja onko konepäällikkö kirjannut tarkastuksen juuri tästä versiosta?” Tallennettu merkintä on todentamaton ilmoitus. Luonnostietoja ei muuteta päästö-, polttoainevarmistus- tai valmiuspäätelmiksi.

## Kuka sitä käyttää
- **Vahtikonemestari (Crew / OOW / Engineer):** pitää konevahdin kirjausta reaaliajassa ja aikajärjestyksessä (13.03 §6).
- **Konepäällikkö:** tarkastaa tallennetun lokin, linkittää sen työmääräyksiin ja vikoihin ja kirjaa tarkastuksen ihmisen tekona (13.03 §5). Vain nimitys, jonka tehtävä on konepäällikkö, voi kirjata konelokin tarkastuksen.

## Ennen kuin aloitat
- Valitse ylhäällä olevasta kontekstipalkista **oikea yritys ja laiva**.
- Lukeminen vaatii oikeuden `daily.engine.read`; tallentaminen vaatii oikeuden `daily.engine.write` ja muokkausoikeudet tälle laivalle. Tarkastuksen kirjaaminen vaatii oikeuden `daily.engine.write` muokkausoikeuksin tälle laivalle, oikeuden `daily.engine.review` sekä voimassa olevan konepäällikön nimityksen juuri tälle tietueelle.
- Työmääräysten tai vikojen linkittäminen vaatii myös lukuoikeuden näihin tietueisiin.

## Vaihe vaiheelta
1. Valitse laiva. **Kirjatut luonnokset** listaa jokaisen lokin päivämäärän ja version mukaan merkinnällä ”Luonnos · tarkastus vaaditaan”.
2. Paina **Uusi luonnos**, tai avaa listattu loki kohtaan **Muokkaa kirjattua luonnosta**.
3. Täytä **Konevahdin konteksti**, **Koneen tila ja tunnit**, **Kulutus ja tuotanto**, **Mittaukset, hälytykset ja viat** sekä **Työ- ja ympäristökonteksti**. Valitse kullekin kentälle **Tunnettu**, **Tuntematon** tai **Puuttuu**.
4. Paina **Tallenna luonnos**. Jokainen tallennus lisää version kohtaan **Kirjattu versiohistoria**.
5. Käytä tallennetussa lokissa kohtaa **Työmääräykset ja viat**: valitse **Tietuetyyppi** ja **Tietue**, sitten **Linkitä**. Linkki kiinnittää yhden täsmällisen version; **Poista linkki** poistaa sen.
6. Avaa tarvittaessa **Avaa polttoainetietue tallennetusta lähteestä** tai **Avaa seisokkitietue tallennetusta lähteestä**. Määriä ja yksiköitä ei kopioida.
7. Konepäällikkö avaa kohdan **Henkilön tarkastuksen kirjaaminen**, valitsee **Valtuutetun henkilön nimitys**- ja **Tarkastusmerkintä**-arvot, kirjoittaa **Tarkastuksen perustelu** -tekstin ja painaa **Kirjaa tarkastusmerkintä**.

## Mitä tilat tarkoittavat
- **Todentamaton ilmoitus / Tunnettu / Tuntematon / Puuttuu:** ilmoitetut arvot ja niiden tietotila. Tuntematon ei tarkoita ”Ei”, ”Valmis” tai ”Ei sovellu”.
- **Odottaa · tarkastusta ei kirjattu:** tälle versiolle ei ole ihmisen tarkastusta.
- **Henkilömetatiedot kirjattu · arvioimaton:** tälle versiolle on konepäällikön tarkastusmerkintä. Se ei osoita todentamista eikä konepäällikön kuittausta.
- **Nykyinen versio / Uudempi versio on olemassa:** onko linkitetty työmääräys tai vika muuttunut linkittämisen jälkeen. Linkki on vain alkuperätieto; se ei todenna, sulje eikä hyväksy kyseistä tietuetta.
- **haku ei saatavilla — TUNTEMATON:** linkitettyä tietuetyyppiä ei voi lukea käyttöoikeuksillasi.

## Mitä et voi tehdä / varoitus
- Järjestelmä ei koskaan tarkasta tai viimeistele lokia. Tarkastus on nimitetyn konepäällikön tekemä ihmisen teko (13.03 §5).
- Lokin minkä tahansa version laatija ei voi tarkastaa sitä. Laatijalle ei tarjota nimitystä, ja sivu ilmoittaa ”You authored this log revision; an independent reviewer must record the review.”; palvelin myös hylkää tällaisen tarkastuksen tehtävien eriyttämisen vuoksi.
- Pidä merkinnät reaaliaikaisina ja aikajärjestyksessä; älä muuta toisen henkilön tarkastettua tietuetta (13.03 §6).
- Vian linkittäminen tässä ei kirjaa rajoitusta eikä sulje vikaa; tee se vika- tai työmääräyssivulla (13.03 §5).
- Linkit vaativat voimassa olevan verkkoyhteyden; offline-tilassa mitään ei linkitetä. Offline-ristiriitoja ei koskaan yhdistetä automaattisesti.

## Jos jokin menee vikaan
- **”Merkintä on muuttunut. Lataa se uudelleen ennen linkkien muuttamista.”:** lataa uudelleen ja linkitä sitten uudelleen.
- **”Save outcome unknown”:** käytä painiketta **Toista täsmällinen tallennettu komento**; älä kirjoita uudelleen.
- **”Henkilön tekemä ristiriidan ratkaisu vaaditaan”:** avaa **Tarkasta ristiriita** tai ristiriitakeskus ja valitse nimenomaisesti (13.03 §10).
- **Nimitystä ei ole saatavilla tai pääsy evätty:** tarkista roolisi ja laivakontekstisi vuokralaisen ylläpitäjän kanssa (13.03 §10).

## Liittyvät sivut
