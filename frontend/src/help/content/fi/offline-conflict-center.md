---
key: offline-conflict-center
title: Olennaisten ristiriitojen keskus
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Olennaisten ristiriitojen keskus listaa valitulle alukselle tai hinaajalle **olennaiset ristiriidat offline-muutostesi ja palvelimen nykyisen tietueen välillä** ja antaa henkilön ratkaista ne perustellulla päätöksellä. Se vastaa kysymykseen: ”Mitkä offline-muutoksistani törmäsivät uudempaan palvelintietueeseen, ja miten, kuka ja millä perusteella se ratkaistaan?” Mitään ei tässä korvata eikä ratkaista automaattisesti.

## Kuka sitä käyttää
- **Miehistö / hinaajan päällikkö / päällikkö / konepäällikkö:** näkevät ja ratkaisevat omat ristiriitaiset tietueensa offline-työskentelyn jälkeen. Kun ristiriita ilmenee, älä oleta, että oma tietueesi voittaa automaattisesti (13.03 §6).
- Olennainen ristiriita vaatii ihmisen ratkaisun; tämä sivu on 13.03 §10:n kulun ”Olennainen ristiriita → ristiriitakeskus” kohde.

## Ennen kuin aloitat
- Sivu listaa vain itse kirjaamasi ristiriidat ja vaatii lukuoikeuden vähintään yhteen luettavaan lähteeseen (esimerkiksi miehistön sijoitus, hinaajaoperaatiot, päivittäinen kirjaus, työmääräyksen todiste (kaluston ja huollon lukuoikeudella), todistuksen uusimisluonnos). Mikä tahansa luettava lähde avaa sivun; luetellut oikeudet ovat vaihtoehtoja.
- Luettelo ja päätökset vaativat voimassa olevan palvelinyhteyden. Offline-tilassa paikalliset muutoksesi säilyvät laitteella.
- Valitse alus tai hinaaja **Alus**-luettelosta.

## Vaihe vaiheelta
1. Valitse alus kohdasta **Alus**. Taulukko avautuu sarakkeineen **Lähde**, **Tietue**, **Versiot**, **Tallennettu**, **Palvelin vastaanotti** ja **Tila**.
2. Jos näkyvissä on huomautus ”Ei mukana:”, lähteitä, joihin sinulla ei ole lukuoikeutta, ei ole luettelossa.
3. Paina tilassa **Avoin — päätös tarvitaan** olevalla rivillä **Tarkastele**.
4. Vertaa kohdassa **Ristiriidan tarkastelu** kunkin kentän arvoja **Ennen tallennusta**, **Tallennettu** ja **Palvelin**. Mitään valintaa ei ole tehty valmiiksi.
5. Valitse kullekin arvolle nimenomaisesti **Säilytä palvelimen arvo** tai **Käytä tallennettua arvoa**, täytä **Ratkaisun syy (pakollinen)** ja paina **Lähetä ratkaisu**.
6. Poistaaksesi vain tämän laitteen paikallisen kopion valitse vahvistus kohdassa **Hylkää paikallinen muutokseni** ja paina **Hylkää paikallinen muutos**.
7. Palaa luetteloon painikkeella **Sulje tarkastelu**.

## Mitä tilat tarkoittavat
- **Avoin — päätös tarvitaan:** ristiriita odottaa ihmisen päätöstä.
- **Korvattu uudemmalla ristiriidalla:** samalle tietueelle on uudempi ristiriita; tarkastele uutta.
- **Ratkaistu nimenomaisella päätöksellä:** joku ratkaisi sen perustellulla päätöksellä. Tämä ei ole operatiivinen hyväksyntä.
- **Ei luovutettu / Ei annettu / Tuntematon:** arvo on piilotettu valtuutuksen vuoksi, sitä ei kirjattu tai sitä ei voitu lukea. Tuntematonta arvoa ei koskaan tulkita tyhjäksi eikä ”Ei”-arvoksi.
- **Server receipt recorded for your explicit decision:** palvelin vastaanotti päätöksesi. Tämä kuitti ei ole operatiivinen hyväksyntä.

## Mitä et voi tehdä / varoitus
- Ristiriitoja ei koskaan yhdistetä tai ratkaista automaattisesti; jokainen arvo vaatii nimenomaisen ihmisen valinnan ja perusteen (13.03 §1).
- Keskus ratkaisee miehistön sijoitusristiriidat suoraan. Muille lähteille se näyttää ”Resolve this conflict from the owning record's offline panel.”; esimerkiksi hinaajatietueet ratkaistaan sivulla **Hinaajan offline-operaatiot**.
- Paikallisen muutoksen hylkääminen poistaa vain laitteen kopion; palvelintietue ei muutu ja ristiriita pysyy kirjattuna perusteltuun päätökseen asti.
- Toisella laitteella tallennettu muutos on ratkaistava sillä laitteella; mitään ei hylätä.
- Lähetys tarkistaa uudelleen käyttöoikeutesi ja tietueiden versiot; se ei myönnä operatiivista hyväksyntää.

## Jos jokin menee vikaan
- **”The submission outcome is unknown” -ilmoitus:** käytä ennen uutta lähetystä painiketta **Tarkista lähetyksen tulos**.
- **”The server record changed after your review” / ”The server record changed again” -ilmoitus:** päätöstäsi ei sovellettu; tarkastele ristiriitaa uudelleen.
- **”Material conflicts could not be loaded. Nothing was resolved.”:** paina **Yritä uudelleen**.
- **”You do not have access to the conflict center.” / ”Conflict access is unavailable for this vessel.”:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
