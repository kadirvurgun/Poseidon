---
key: deck-daily-log
title: Kannen päiväloki
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Kannen päiväloki säilyttää valitun laivan **kansivahdin kirjaukset** luonnoksina kirjatun lähdeversion kanssa. Se vastaa kysymykseen: ”Mitä vahti ilmoitti tälle päivälle ja jaksolle sijainnista, suunnasta, nopeudesta, säästä, näkyvyydestä, navigoinnista, satama- ja lastitöistä, painolastista, kansitöistä, turvallisuushavainnoista ja vahdinvaihdosta, ja onko juuri tälle versiolle kirjattu tarkastus?” Tallennettu merkintä on todentamaton ilmoitus, ei hyväksytty loki.

## Kuka sitä käyttää
- **Vahtipäällikkö (Crew / OOW):** pitää vahtikirjausta reaaliajassa ja aikajärjestyksessä (13.03 §6).
- **Päällikkö tai yliperämies:** tarkastaa tallennetun lokin ja kirjaa tarkastuksen ihmisen tekona (13.03 §2). Vain nimitys, jonka tehtävä on päällikkö tai yliperämies, voi kirjata kansilokin tarkastuksen.

## Ennen kuin aloitat
- Valitse ylhäällä olevasta kontekstipalkista **oikea yritys ja laiva**. Jos laivaa ei ole valittu, sivu näyttää ”Valitse laiva”.
- Lukeminen vaatii oikeuden `daily.deck.read`. Luominen ja tallentaminen vaativat oikeuden `daily.deck.write` ja muokkausoikeudet tälle laivalle; muuten näet ilmoituksen ”Vain lukuoikeus. Muokkausta ja tallennusta ei ole myönnetty.”
- Tarkastuksen kirjaaminen vaatii oikeuden `daily.deck.write` muokkausoikeuksin tälle laivalle, oikeuden `daily.deck.review` sekä voimassa olevan päällikön tai yliperämiehen nimityksen juuri tälle tietueelle.

## Vaihe vaiheelta
1. Valitse laiva. **Kirjatut luonnokset** listaa jokaisen lokin päivämäärän ja version mukaan merkinnällä ”Luonnos · tarkastus vaaditaan”.
2. Paina **Uusi luonnos** luodaksesi **Uusi päivittäisluonnos**, tai avaa listattu loki kohtaan **Muokkaa kirjattua luonnosta**.
3. Täytä ensin **Vahtikonteksti** (**Lokipäivä**, **Vahti / Ajanjakso**, **Vahtipäällikön viite**). Upseeriluettelo tarjoaa vain henkilöitä, jotka ovat käytettävissä ilmoitettuna lokipäivänä.
4. Täytä **Navigointi ja sää**, **Satama- ja lastitoiminnot** sekä **Kansityöt ja luovutus**. Valitse kullekin kentälle **Tunnettu**, **Tuntematon** tai **Puuttuu**.
5. Paina **Tallenna luonnos** vahdin aikana tai heti sen jälkeen. Jokainen tallennus lisää version kohtaan **Kirjattu versiohistoria**; aiemmat versiot pysyvät muuttumattomina.
6. Päällikkö tai yliperämies avaa kohdan **Henkilön tarkastuksen kirjaaminen**, valitsee **Valtuutetun henkilön nimitys**- ja **Tarkastusmerkintä**-arvot, kirjoittaa **Tarkastuksen perustelu** -tekstin ja painaa **Kirjaa tarkastusmerkintä**.

## Mitä tilat tarkoittavat
- **Luonnos · ilmoitukset todentamattomia / Todentamaton ilmoitus:** henkilön ilmoittama; ei todennettu.
- **Tunnettu / Tuntematon / Puuttuu:** kentän tietotila. Tuntematon ei tarkoita ”Ei”, ”Valmis” tai ”Ei sovellu”.
- **Odottaa · tarkastusta ei kirjattu:** tälle versiolle ei ole ihmisen tarkastusta.
- **Henkilömetatiedot kirjattu · arvioimaton:** tälle versiolle on tarkastusmerkintä. Se ei osoita todentamista, lähettämistä eikä päällikön turvallisuushyväksyntää.
- **Kirjaa tarkastettu ilmoitus (arvioimaton) / Palauta korjattavaksi:** kaksi tarkastusmerkinnän tyyppiä, jotka tarkastaja voi kirjata.
- **Historiallinen versio:** tarkastus kuuluu vanhempaan versioon, ei nykyiseen luonnokseen.

## Mitä et voi tehdä / varoitus
- Järjestelmä ei koskaan tarkasta tai viimeistele lokia. Tarkastus on nimitetyn päällikön tai yliperämiehen tekemä ihmisen teko (13.03 §2).
- Lokin minkä tahansa version laatija ei voi tarkastaa sitä. Laatijalle ei tarjota nimitystä, ja sivu ilmoittaa ”You authored this log revision; an independent reviewer must record the review.”; palvelin myös hylkää tällaisen tarkastuksen tehtävien eriyttämisen vuoksi.
- Pidä merkinnät reaaliaikaisina ja aikajärjestyksessä; älä kirjoita vanhaa vahtia uudelleen näyttämään uudelta (13.03 §6).
- Älä muuta toisen henkilön tarkastettua tietuetta; myöhempi tallennus on uusi versio ja vaatii uuden tarkastuksen (13.03 §6).
- Offline-kirjaus kattaa vain erikseen luokitellut kentät. Offline-ristiriitaa ei koskaan yhdistetä automaattisesti; älä oleta, että sinun tietueesi voittaa (13.03 §6).

## Jos jokin menee vikaan
- **”The record changed. Your inputs are retained.”:** hylkää ja lataa uudelleen nimenomaisesti ennen uutta yritystä.
- **”Save outcome unknown”:** käytä painiketta **Toista täsmällinen tallennettu komento**; älä kirjoita kenttiä uudelleen.
- **Upseerin viite ei ole käytettävissä:** jätä kenttä tilaan Puuttuu tai Tuntematon; kirjoitettu nimi ei voi korvata hakua.
- **”Henkilön tekemä ristiriidan ratkaisu vaaditaan”:** avaa **Tarkasta ristiriita** tai ristiriitakeskus ja valitse nimenomaisesti (13.03 §10).
- **Nimitystä ei ole saatavilla:** tarkista roolisi ja laivakontekstisi vuokralaisen ylläpitäjän kanssa (13.03 §10).

## Liittyvät sivut
