---
key: defects
title: Viat ja suojaukset
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Viat ja väliaikaiset toimenpiteet -sivu sisältää valitun aluksen **kirjatut viat, rajoitukset ja ilmoitetut väliaikaiset toimenpiteet**. Se vastaa kysymykseen: ”Kuinka kriittinen tämä vika on, mikä suojatoimi tai käyttörajoitus on kirjattu, onko korjaava toimenpide todennettu ja mikä vielä estää sulkemisen?” Jokainen vaihe on ihmisen kirjaus; sivu ei myönnä turvallisuus- eikä käyttövaltuutta.

## Kuka sitä käyttää
- **Konepäällikkö / tekninen tarkastaja:** kirjaa kriittisen vian suojatoimen tai rajoituksen ja valmiusvaikutuksen sekä seuraa korjaavaa toimenpidettä (13.03 §5).
- **Päällikkö:** näkee vian vaikutuksen operatiivisiin rajoituksiin (13.03 §2).
- **Konemestari / miehistö:** ilmoittaa havaitsemansa vian (13.03 §6).

## Ennen kuin aloitat
- Lukeminen vaatii oikeudet `defect.read` ja `equipment.read`. Ilmoitukset ja elinkaarikirjaukset vaativat oikeuden `defect.write`.
- Todentaminen vaatii oikeuden `defect.verify`, vaikuttavuus oikeuden `defect.effectiveness` ja sulkeminen oikeuden `defect.close`, kukin sisäänkirjautuneen ihmiskäyttäjän kanssa.
- Valitse oikea alus kohdasta **Valitse alus tai hinaaja**.

## Vaihe vaiheelta
1. Valitse alus. Suodata tarvittaessa **Kirjatut ilmoitukset** valinnalla **Suodata palautetut vanhenemishavainnot** ja avaa ilmoitus.
2. Uutta vikaa varten paina **Uusi vikailmoitus**, täytä kentät ja paina **Tallenna arvioimaton ilmoitus**. Keskeneräiset luonnokset voidaan tallentaa.
3. Paina tallennetun ilmoituksen **Vian elinkaari** -osiossa **Aloita elinkaari**.
4. Valitse kriittisyys ja kirjoita lausunto (vähintään 10 merkkiä) toiminnolla **Kirjaa arviointi**.
5. Käytä toimintoa **Kirjaa suojatoimi tai rajoitus** ja valitse **Toimenpiteen tyyppi** -arvoksi **Suojatoimi** tai **Käyttörajoitus**. Väliaikaiselle toimenpiteelle syötä päättymisaika (UTC).
6. Kuvaa tehty työ toiminnolla **Kirjaa korjaava toimenpide**.
7. Valtuutettu henkilö, joka ei kirjannut korjaavaa toimenpidettä, kirjaa **Kirjaa varmennus** ja sen jälkeen **Kirjaa vaikuttavuus**.
8. Kun **Sulkemisen esteet** -kohtia ei enää ole, valtuutettu henkilö, joka ei myöskään kirjannut korjaavaa toimenpidettä, kirjaa **Sulje ja palauta käyttöön**. Kirjaukset näkyvät kohdassa **Elinkaaren kirjaukset**.

## Mitä tilat tarkoittavat
- **Havaitseminen / Arviointi / Suojatoimi / Toimenpide / Todenna / Vaikuttavuus:** elinkaaren vaihe.
- **Suljettu (kirjattu ihmisen päätös):** sulkeminen on ihmisen päätös; se ei ole vihreä ”korjattu”-tulos.
- **Kriittinen sääntö pätee:** kriittistä vikaa ei voi sulkea ilman suojatoimea.
- **Aktiivinen / Päättynyt — estävä / Poistettu / Päättyi sulkemiseen:** suojatoimen tai rajoituksen tila. Päättynyt väliaikainen toimenpide on uusittava tai poistettava.
- **Kriittinen vika ilman voimassa olevaa suojatoimea / Kriittisyys tuntematon eikä suojatoimea:** hälytyksiä. Nämä viat näkyvät myös kohdassa **Kuntohälytykset** sivulla [PMS-komentokeskus](/app/pms-command-center).
- **Tuntematon:** kriittisyyttä ei ole arvioitu. Tuntematon ei tarkoita ”Matala” eikä ”Ei-kriittinen”.

## Mitä et voi tehdä / varoitus
- Jos kirjasit korjaavan toimenpiteen, et voi kirjata todentamista, vaikuttavuutta etkä käyttöön palauttamista; ne vaativat oman valtuutuksensa ja eri henkilön.
- Todennettu, Vaikuttava ja Suljettu ovat erillisiä ihmisen kirjauksia. Ilmoitettu vaihe tai toimenpide ei valtuuta käyttöä, ei todenna vaikuttavuutta eikä salli sulkemista.
- Jos ilmoitus muuttuu todentamisen jälkeen, sulkeminen estetään ja tietue palaa tarkastukseen.
- Laitteen kriittisyyttä ei kopioida tämän vian arvioinniksi.
- Offline-tilassa nykyiset vikalähteet eivät ole käytettävissä; offline-tallennusta ei ole.

## Jos jokin menee vikaan
- **Joku muu muutti tätä tietuetta:** lue nykyinen tietue painikkeella **Lataa uudelleen** ja kirjaa uudelleen.
- **Tallennuksen lopputulos on tuntematon:** **Yritä samaa komentoa uudelleen** lähettää saman komennon.
- **Sinulla ei ole palvelimen oikeutta tähän toimintoon tai tietueeseen:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Sulkemisen esteet:** täydennä luettelossa puuttuva kirjaus. Jos tietuetta ei voida todentaa, tarkista todisteet todentajan kanssa (13.03 §10).

## Liittyvät sivut
