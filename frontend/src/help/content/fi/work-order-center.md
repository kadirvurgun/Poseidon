---
key: work-order-center
title: Työmääräykset
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Työmääräyskeskus sisältää valitun aluksen **Työmääräysluonnokset ja ilmoitetut suoritustietueet**. Se vastaa kysymykseen: ”Mitä työmääräyksiä tällä aluksella on, ja mitkä niistä on vapautettu, suoritettu, mihin on lähetetty todisteet ja minkä ihminen on todentanut?” Jokainen tallennettu versio säilyy muuttumattomassa historiassa. Todentaminen ja sulkeminen eivät ole käyttöön palauttaminen, lupa eivätkä riskihyväksyntä.

## Kuka sitä käyttää
- **Konepäällikkö / Technical Superintendent:** avaa, vapauttaa, todentaa ja sulkee suunnitellut ja suunnittelemattomat työmääräykset (13.03 §5).
- **Konemestari / miehistö:** aloittaa osoitetun työn sekä kirjaa ja lähettää lukemat, varaosat, valokuva- ja asiakirjatodisteet (13.03 §6).

## Ennen kuin aloitat
- Lukeminen vaatii oikeudet `workorder.read`, `equipment.read` ja `maintenance.read`. Luonnokset vaativat oikeuden `workorder.write`, vapautus `workorder.release`, suoritus `workorder.execute`, todentaminen `workorder.verify` ja sulkeminen `workorder.close`.
- Vapautus, todentaminen ja sulkeminen edellyttävät sisäänkirjautunutta ihmiskäyttäjää.
- Valitse oikea alus kohdasta **Valitse alus tai hinaaja**.

## Vaihe vaiheelta
1. Valitse alus. Suodata tätä sivua kohdassa **Kirjatut työmääräykset** kentällä **Hae palautettuja työmääräysnumeroita** ja avaa tietue.
2. Uutta työtä varten paina **Uusi työmääräysluonnos**, täytä kentät ja paina **Tallenna arvioimaton luonnos**. Keskeneräisiä luonnoksia voi tallentaa.
3. Jos sinulla on vapautusoikeus, valitse kohdassa **Suoritus ja todisteet** kohdan **Pakollisten todisteiden käytäntö** alta **Määrittele pakolliset todisteet**, merkitse todistelajit ja paina **Vapauta työmääräys**. Jos vapautat määrittelemättä, todentaminen pysyy estettynä, kunnes vapauttaja käyttää toimintoa **Muuta pakollisia todisteita** → **Tallenna pakolliset todisteet**.
4. Suorittaja painaa **Aloita työ**, täyttää kohdat **Lukemat**, **Kulutetut varaosat**, **Valokuva- ja asiakirjatodisteet**, **Tehty työ** ja **Havainnot**, painaa **Tallenna todisteet** ja sitten **Lähetä todisteet**.
5. Valtuutettu todentaja, joka ei suorittanut työtä, tarkastelee todisteet. Hän painaa **Kirjaa varmennus** tai valitsee **Palautuksen syy** ja painaa **Palauta suorittajalle**.
6. Todennetulle työmääräykselle valtuutettu henkilö painaa **Kirjaa sulkeminen**.
7. Näet aiemmat versiot kohdassa **Muuttumaton työmääräyshistoria**. Jos seisokkikirjaus tarvitaan, käytä painiketta **Avaa linkitetty seisokkiluonnos**.

## Mitä tilat tarkoittavat
- **Ei vapautettu suoritettavaksi. / Vapautettu / Käynnissä:** työtä ei ole vielä vapautettu, se on vapautettu tai sitä suoritetaan.
- **Todisteet lähetetty:** suorittaja ilmoitti työn ja todisteet. Tämä ei tarkoita todennettua.
- **Todennettu (ihmisen kirjaus):** kirjannut valtuutettu todentaja, joka ei suorittanut työtä. **Suljettu (ihmisen kirjaus)** on erillinen vaihe.
- **Pakolliset todisteet puuttuvat / Olemassa, odottaa ihmisen todentamista / Ihmisen todentama:** pakollisten todisteiden tarkastuksen tila.
- **Tarkistus vaaditaan: käytäntöä ei ole määritetty:** pakollisia todisteita ei määritelty. Määrittämätön ei koskaan tarkoita, ettei mitään vaadittaisi; todentaminen pysyy estettynä.
- **Todentajan palauttama:** työ palautettiin suorittajalle näytetyllä syyllä.

## Mitä et voi tehdä / varoitus
- Jos suoritit työn, et voi todentaa etkä sulkea sitä. Sivu sanoo ”Suoritit tämän työn itse, joten et voi todentaa tai sulkea sitä.”
- Todisteet lähetetty, Todennettu ja Suljettu ovat erillisiä ihmisen kirjauksia. Sivu ei koskaan julista laitetta käyttövalmiiksi.
- Yhteenveto ”Tallennettu työmääräyslähde” kuvaa tallennetun luonnoksen kentät ja toteaa: ”Vapautus, todisteiden kirjaus, riippumaton todentaminen ja sulkeminen kirjataan suorituspaneeliin. Toimitettu todiste hyväksytään vain riippumattomalla ihmisen todentamisella, ei koskaan työn tehneen henkilön toimesta; erillistä todisteen hyväksymisvaihetta ei ole, eikä todentaminen ole päätös käyttöön palauttamisesta.” Nämä toimenpiteet tehdään kohdassa **Suoritus ja todisteet**.
- Lukemat säilytetään ilmoitetussa muodossa; yksikkömuunnosta tai kynnysarvoa ei sovelleta.
- Offline-todistejono toimii vain, kun hyväksytty offline-käytäntö on olemassa ja työ on käynnissä. Ristiriitoja ei koskaan yhdistetä automaattisesti; käytä painiketta **Avaa ristiriitakeskus**, ja ihminen päättää.

## Jos jokin menee vikaan
- **The record changed or this action is not allowed in its current state:** lue nykyinen tietue painikkeella **Lataa uudelleen** ja yritä uudelleen.
- **Outcome unknown:** **Toista sama komento** lähettää saman komennon; se ei luo uutta tietuetta.
- **Permission or human authority is missing:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **The required-evidence gate rejected this action:** täydennä puuttuvat todistelajit. Jos tietuetta ei voi todentaa, tarkista todisteet ja niiden alkuperä todentajan kanssa (13.03 §10).

## Liittyvät sivut
