---
key: tug-job-intake
title: Työn vastaanotto
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Työn vastaanotto jäsentää uuden **hinaajatyön pyynnön luonnokseksi** valitussa hinaajakontekstissa. Se vastaa kysymykseen: ”Mikä tässä työssä on avustettava alus, työn tyyppi, aikaikkuna, sijainti ja tarvittavat resurssit (hinaajamäärä, vetovoima)?” Tallennettu pyyntö on ilmoitusluonnos; se ei ole operatiivinen osoitus, kelpoisuus, lupa eikä turvallisuushyväksyntä.

## Kuka sitä käyttää
- **Hinaajaoperaatioiden koordinaattori / Maaoperaatiot:** jäsentävät pyynnön ja syöttävät avustettavan aluksen, työn tyypin, aikaikkunan ja vaaditun suorituskyvyn (13.03 §7).

## Ennen kuin aloitat
- Valitse ylhäällä olevasta kontekstipalkista **oikea yritys ja hinaaja**.
- Tarvitset hinaajan kirjoitusoikeuden. Ilman sitä lomake ei avaudu ja näytetään viesti ”Pääsy ei ole käytettävissä.”
- Keskeneräisiä luonnoksia voi tallentaa. Pakolliset operatiiviset tiedot ja valtuutettu tarkastus on ratkaistava ennen lähettämistä.

## Vaihe vaiheelta
1. Täytä kohdassa **Pyyntö ja avustettava alus** kentät **Pyynnön lähde**, **Asiakasviite**, **Avustettava alus**, **Avustettavan aluksen IMO**, **Työn tyyppi**, **Prioriteetti** ja **Pyynnön omistaja**. Luettelot, joista ei valita mitään, pysyvät tilassa ”Tuntematon”.
2. Syötä kohdassa **Pyydetty aika ja sijainti** **Pyydetty alku (UTC)**, **Pyydetty loppu (UTC, ehdollinen)**, **Toiminta-alue**, **Noutopiste** ja **Määränpää**. Ajat ovat UTC-aikaa.
3. Syötä kohdassa **Resurssitarpeet ja muistiinpanot** **Vaadittu BP (tonnia, tarkka desimaali)**, **Vaadittu hinaajamäärä**, **Erityisvaatimukset** ja **Huomautukset**.
4. Paina **Tallenna luonnos**. Onnistuessa työn tiedot avautuvat viestillä ”Luonnos tallennettu. Operatiivinen hyväksyntä ei ole edelleenkään käytettävissä.”
5. Hylkää muokkauksesi painikkeella **Hylkää muutokset**.
6. Kirjaa pyyntö suunnittelua varten työn tiedoissa painikkeella **Lähetä pyyntö**. Jos pakollisia tietoja puuttuu, lähettäminen estetään ja puuttuvat kentät luetellaan.

## Mitä tilat tarkoittavat
- **Luonnos:** pyyntö on tallennettu ja muokattavissa; sitä ei ole vielä lähetetty.
- **Lähetetty:** pyyntö on kirjattu suunnittelua varten. Se ei ole hyväksyntä, osoitus eikä valmiuspäätös.
- **Peruttu:** pyyntö on suljettu; sitä ei voi käynnistää uudelleen.
- **Tuntematon:** kentälle ei valittu arvoa. Tuntematon ei ole oletusarvo eikä koskaan korvaa arvoa ”Ei” tai ”Ei sovellu”.
- **Tarkista tämä arvo.:** palvelin hylkäsi tämän kentän; korjaa se ja tallenna uudelleen.

## Mitä et voi tehdä / varoitus
- Tämä sivu ei osoita hinaajaa eikä koskaan julista työtä valmiiksi. Osoitusehdotus on erillinen vaihe eikä koskaan ohita hinaajan päällikön aluksella tekemää turvallisuuspäätöstä (13.03 §7).
- Pyyntöä ei voi lähettää, kun pakollisia tietoja puuttuu; keskeneräisen luonnoksen voi silti tallentaa.
- Lähetetyn pyynnön kenttiä ei voi muuttaa tällä lomakkeella; peruutettua pyyntöä ei voi käynnistää uudelleen, luo sen sijaan uusi.
- Offline-tilassa luonnos säilyy vain tällä sivulla eikä sitä voi tallentaa.

## Jos jokin menee vikaan
- **Viesti ”Save outcome uncertain”:** täsmällinen yritys säilytetään; käytä yhteyden palattua painiketta **Yritä uudelleen** saman komennon lähettämiseen. Paikallisten syötteiden hylkääminen ei peru jo lähetettyä komentoa.
- **Viesti ”Ristiriita”:** luonnoksesi säilytetään; hylkää ja lataa uudelleen ennen uutta yritystä.
- **Tarkista kentät:** korjaa merkityt kentät.
- **Pääsy ei ole käytettävissä:** tarkista roolisi ja hinaajakontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
