---
key: fuel-bunker-log
title: Polttoaine, bunkkeri ja varannot
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Polttoaine, bunkkeri ja varannot -sivu säilyttää valitun laivan **polttoaine- ja bunkkeri-ilmoitukset** todentamattomina luonnoksina lähdehistorioineen. Se vastaa kysymykseen: ”Mitä polttoainetta vastaanotettiin tai kulutettiin, minkä ROB-arvon ilmoitimme ennen ja jälkeen, mitkä BDN-, toimittaja-, näyte- ja säiliöviitteet kuuluvat siihen, ja jatkuuko ilmoitettu ROB-ketju kirjauksesta toiseen?” Sivu ei korjaa määriä, ei laske päästöjä eikä osoita vaatimustenmukaisuutta tai todentamista.

## Kuka sitä käyttää
- **Konepäällikkö / konepäällystö:** kirjaavat polttoaineen vastaanotot, kulutuksen ja ROB-ilmoitukset todisteviitteineen (13.03 §5).
- **Päällikkö:** seuraa polttoaineilmoituksia päällikön tarkastamien noon-yhteenvetojen rinnalla (13.03 §2).

## Ennen kuin aloitat
- Valitse ylhäällä olevasta kontekstipalkista **oikea yritys ja laiva**. Jos laivaa ei ole valittu, sivu näyttää ”Valitse laiva”.
- Lukeminen vaatii oikeuden `operations.fuel.read`. Luominen ja tallentaminen vaativat oikeuden `operations.fuel.write` ja muokkausoikeudet tälle laivalle.
- Ajoissa on oltava päivämäärä, sekunnit ja Z tai nimenomainen UTC-siirtymä; paikallista aikaa ilman aikavyöhykettä ei hyväksytä.

## Vaihe vaiheelta
1. Valitse laiva. **Polttoaine- ja varantotietueet** listaa palautetut tietueet. Rajaa vain tätä sivua valinnalla **Suodata palautettua sivua**.
2. Lue luettelon alta **ROB-jatkuvuuden tarkistus**. Se näyttää taseen (ennen + vastaanotettu − kulutettu − jälkeen), **Jatkuvuus edelliseen kirjaukseen** ja **Tarkistusmerkinnät** kullekin ilmoitetulle polttoainetyypille.
3. Lue taustaksi **Päällikön tarkistamat Noon-polttoaine- ja ROB-yhteenvedot**. Ne näytetään ilmoitettuna tekstinä eikä niitä kopioida kirjanpitoon.
4. Paina **Uusi luonnostietue**. Täytä **Tietue- ja toimituskonteksti**, **Määrä- ja varantoilmoitukset** sekä **Polttoaineen ominaisuudet ja näytetietue**. Säilytä kussakin kentässä ilmoitettu arvo, **Puuttuu** tai **Tuntematon**.
5. Paina **Tallenna todentamaton luonnos**. Jokainen tallennus lisää version kohtaan **Kirjattu versiohistoria**.
6. Aloittaaksesi tallennetusta noon-raportista tai konelokista käytä kyseisellä sivulla painiketta **Avaa polttoainetietue tallennetusta lähteestä**. **Linkitetty päivittäisilmoitus** pysyy täällä vain luku -tilassa.

## Mitä tilat tarkoittavat
- **Luonnosilmoitus / Todentamaton luonnostietue:** henkilö ilmoitti arvot; mitään ei ole todennettu.
- **Tarkastus vaaditaan:** jokaisen tietueen vaatimustenmukaisuuden tarkastustila; vaatimustenmukaisuutta ei ole arvioitu.
- **Odottaa; todentamista ei osoitettu:** todentamisen tarkastustila; todentamista ei ole.
- **Poikkeamaa ei havaittu:** ilmoitetut luvut täsmäävät. Tämä ei ole todentaminen.
- **Tuntematon:** jatkuvuutta ei voida arvioida, esimerkiksi kun arvo puuttuu tai vanhempi historia on arvioidun jakson ulkopuolella. Tuntematon ei tarkoita ”Poikkeamaa ei havaittu” eikä ”Valmis”.
- **Puuttuu / Tuntematon:** erillään nollasta.

## Mitä et voi tehdä / varoitus
- Ihmisen tarkastus, operatiivinen tai oikeudellinen hyväksyntä, viimeistely ja offline-kirjaus eivät ole käytettävissä tällä sivulla.
- Järjestelmä merkitsee erot ihmisen tarkastettaviksi; se ei koskaan korjaa määrää eikä päätä polttoaineen vaatimustenmukaisuudesta.
- Vain uusimmat 100 tietuetta arvioidaan. Ilman säiliöviitettä usean säiliön aluksen aukko on kysymys puuttuvista säiliötiedoista, ei tietovirhe.
- Noon-raportin tai konelokin määriä, joiden yksikkö on ratkaisematta, ei koskaan muunneta vastaanotetuiksi, kulutetuiksi tai ROB-arvoiksi.

## Jos jokin menee vikaan
- **”The saved source changed.”:** syötteesi säilyvät; lataa uudelleen tai hylkää nimenomaisesti ennen uutta tallennusta.
- **”Outcome unknown.”:** käytä painiketta **Toista täsmällinen operatiivinen komento**; älä kirjoita uudelleen.
- **”Tarkista ilmoitetut arvot, nimenomaiset aikasiirtymät ja nykyinen lähdekäyttöoikeus.”:** korjaa merkitty kenttä tai jätä se tilaan Puuttuu tai Tuntematon.
- **Operatiivinen lähde tai käyttöoikeus ei ole käytettävissä:** käytä painiketta **Toista lähteen luku**; jos pääsy on edelleen evätty, tarkista roolisi ja laivakontekstisi vuokralaisen ylläpitäjän kanssa (13.03 §10).

## Liittyvät sivut
