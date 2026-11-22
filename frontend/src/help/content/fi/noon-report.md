---
key: noon-report
title: Keskipäiväraportti
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Keskipäiväraporttisivu säilyttää valitun laivan **päivittäiset keskipäiväilmoitukset** luonnoksina kirjatun lähdeversion kanssa. Se vastaa kysymykseen: ”Mitä ilmoitimme raporttiajasta, matkasta, sijainnista, kuljetusta matkasta, säästä, lastista, koneiston tunneista, polttoaineesta ja varannoista (ROB), ja onko päällikkö kirjannut tarkastuksen juuri tästä versiosta?” Tallennettu luonnos on todentamaton ilmoitus. Sen tallentaminen ei osoita päällikön tai konepäällikön hyväksyntää, eikä sivu tee siitä päästö-, polttoainevarmistus- tai valmiuspäätelmiä.

## Kuka sitä käyttää
- **Raportin laativa upseeri (Crew / OOW):** täyttää ja tallentaa keskipäiväluonnoksen oikealle laivalle ja päivälle (13.03 §6).
- **Päällikkö:** tarkastaa tallennetun raportin ja kirjaa tarkastuksen ihmisen tekona (13.03 §2). Vain nimitys, jonka tehtävä on päällikkö, voi kirjata keskipäiväraportin tarkastuksen.

## Ennen kuin aloitat
- Valitse ylhäällä olevasta kontekstipalkista **oikea yritys ja laiva**. Jos laivaa ei ole valittu, sivu näyttää ”Valitse laiva”.
- Lukeminen vaatii oikeuden `daily.noon.read`. Luonnosten luominen ja tallentaminen vaativat oikeuden `daily.noon.write` ja muokkausoikeudet tälle laivalle; ilman niitä näet ilmoituksen ”Vain lukuoikeus. Muokkausta ja tallennusta ei ole myönnetty.”
- Tarkastuksen kirjaaminen vaatii oikeuden `daily.noon.write` muokkausoikeuksin tälle laivalle, oikeuden `daily.noon.review` sekä voimassa olevan päällikön nimityksen juuri tälle tietueelle.

## Vaihe vaiheelta
1. Valitse laiva. **Kirjatut luonnokset** -luettelo näyttää jokaisen raportin päivämäärän ja version mukaan merkillä ”Luonnos · tarkastus vaaditaan”. Selaa painikkeilla **Edelliset luonnokset** / **Lisää luonnoksia**.
2. Paina **Uusi luonnos** avataksesi **Uusi päivittäisluonnos**, tai avaa listattu raportti kohtaan **Muokkaa kirjattua luonnosta**.
3. Täytä ryhmät (**Raportti ja matka**, **Sijainti ja toiminta**, **Sää ja lasti**, **Koneisto, polttoaine ja varannot**). Valitse kullekin kentälle **Tietojen tila**: **Tunnettu**, **Tuntematon** tai **Puuttuu**. Keskeneräiset luonnokset voidaan tallentaa.
4. Paina **Tallenna luonnos**. Sivu vahvistaa ”Draft saved. Human review and operational approval remain separate.” Jokainen tallennus lisää version kohtaan **Kirjattu versiohistoria**.
5. Tallennetussa raportissa päällikkö avaa kohdan **Henkilön tarkastuksen kirjaaminen**, valitsee **Valtuutetun henkilön nimitys**- ja **Tarkastusmerkintä**-arvot (”Kirjaa tarkastettu ilmoitus (arvioimaton)” tai ”Palauta korjattavaksi”), kirjoittaa **Tarkastuksen perustelu** -tekstin ja painaa sitten **Kirjaa tarkastusmerkintä**.
6. Jos polttoaine tai satamatapahtuma on kirjattava, käytä toimintoja **Käytä tallennettua operatiivista lähdettä** ja **Avaa polttoainetietue tallennetusta lähteestä** tai **Avaa satamatapahtuma tallennetusta lähteestä**. Määriä, yksiköitä ja hyväksyntöjä ei kopioida.

## Mitä tilat tarkoittavat
- **Luonnos · ilmoitukset todentamattomia / Todentamaton ilmoitus:** henkilö on ilmoittanut arvot; kukaan ei ole todentanut niitä.
- **Tunnettu / Tuntematon / Puuttuu:** kentän tietotila. Tuntematon ei tarkoita ”Ei”, ”Valmis” tai ”Ei sovellu”, eikä Puuttuu ole nolla.
- **Odottaa · tarkastusta ei kirjattu:** tälle versiolle ei ole kirjattu ihmisen tarkastusta.
- **Henkilömetatiedot kirjattu · arvioimaton / Kirjattu · arvioimaton:** näytetylle versiolle on päällikön tarkastusmerkintä. Tarkastuksen kirjaaminen ei osoita todentamista, lähettämistä eikä päällikön turvallisuushyväksyntää.
- **Historiallinen versio:** tarkastusmerkintä kuuluu vanhempaan versioon eikä tarkasta nykyistä tallennettua luonnosta.

## Mitä et voi tehdä / varoitus
- Järjestelmä ei koskaan tarkasta tai viimeistele raporttia. Tarkastuksen kirjaaminen on nimitetyn päällikön tekemä ihmisen teko (13.03 §2).
- Lokin minkä tahansa version laatija ei voi tarkastaa sitä. Laatijalle ei tarjota nimitystä, ja sivu ilmoittaa ”You authored this log revision; an independent reviewer must record the review.”; palvelin myös hylkää tällaisen tarkastuksen tehtävien eriyttämisen vuoksi. Jos päällikkö laati raportin, riippumattoman tarkastajan, jolla on tarkastusvaltuus, on silti kirjattava tarkastus.
- Uusi tallennus luo uuden version; aiempi tarkastus jää omaan versioonsa. Tarkasta uudelleen muutosten jälkeen.
- Kirjoitettu vapaatekstiviite ei voi korvata valtuutettua hakua; jätä kenttä tilaan Puuttuu tai Tuntematon.
- Offline-tilassa vain erikseen luokitellut kentät voivat mennä salattuun jonoon. Tarkastus-, hyväksyntä- ja todentamiskentät eivät ole mukana.

## Jos jokin menee vikaan
- **”The record changed. Your inputs are retained.”:** joku tallensi uudemman version. Hylkää ja lataa uudelleen nimenomaisesti ennen uutta yritystä.
- **”Save outcome unknown”:** kentät on jäädytetty; käytä painiketta **Toista täsmällinen tallennettu komento**, älä kirjoita uudelleen.
- **”No current source-authorized human appointment is available for this record.”:** tarkista roolisi ja laivakontekstisi vuokralaisen ylläpitäjän kanssa (13.03 §10).
- **”Henkilön tekemä ristiriidan ratkaisu vaaditaan”:** offline-kirjaus on ristiriidassa palvelimen kanssa. Avaa **Tarkasta ristiriita** tai ristiriitakeskus ja valitse nimenomaisesti; mitään ei yhdistetä automaattisesti (13.03 §10).
- **Päivittäiset lähteet eivät ole käytettävissä tai pääsy evätty:** käytä painiketta **Yritä lähteiden lukua uudelleen**; jos tilanne jatkuu, ilmoita aika ja vaiheesi tukeen.

## Liittyvät sivut
