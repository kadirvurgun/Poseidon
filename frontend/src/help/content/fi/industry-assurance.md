---
key: industry-assurance
title: Toimialan varmennus (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Toimialan varmennus -sivu tuo säiliöaluksen SIRE 2.0 -tarkastustulokset työkirjasta, asettaa muutokset ihmisen tarkastettaviksi, linkittää havainnot turvallisuustoimenpiteisiin ja kirjaa kenttänäytöt. Se vastaa kysymykseen: ”Mitkä tämän tarkastuksen havainnot ovat avoinna, mihin on linkitetty toimenpide, ja missä näyttö on?” Tuonnit ovat luonnoksia: eri henkilö hyväksyy jokaisen muuttuneen kohdan. Havainnot pysyvät avoimina ja todentamattomina; tämä sivu ei koskaan sulje tai todenna niitä. Ketju on havainto → toimenpide → todentaminen, ja todentaminen sekä vaikuttavuus ovat erillisiä ihmisen kirjauksia toimenpiteessä sivulla Turvallisuustapahtumat ja läheltä piti -tilanteet (13.03 §4, §8).

## Kuka sitä käyttää
- **DPA / HSEQ:** linkittää tarkastushavainnot toimenpiteisiin ja seuraa havainto → toimenpide → todentaminen -ketjua (13.03 §4).
- **Regulatory / Assurance -käyttäjä:** hallitsee sarakemäppäyksiä ja valmistelee tai tarkastaa tuonteja (13.03 §8).
- **Päällikkö ja laivan henkilöstö:** kirjaavat havainnon kenttänäytön (valokuvat).

## Ennen kuin aloitat
- Valitse alus. SIRE 2.0 koskee vain säiliöaluksia. Jos laivatyyppiä ei ole ilmoitettu alusprofiilissa, tuonnit pysyvät tilassa **tarkastus vaaditaan**.
- Lukeminen vaatii tarkastusten lukuoikeuden. Tuonti, tuonnin hyväksyminen, näytön kirjoittaminen, havaintojen linkittäminen ja mäppäysten kirjoittaminen ovat erillisiä oikeuksia. Toimenpiteen linkittäminen vaatii lisäksi turvallisuustapahtumien lukuoikeuden; näytön kirjaaminen vaatii lisäksi tiedostojen lukuoikeuden.
- Sarakeskeema on synteettinen; lisensoitua OCIMF-muotoa ei käytetä.
- Offline-tilassa vain valokuvat asetetaan jonoon tälle laitteelle; tuonnit ja päätökset vaativat yhteyden.

## Vaihe vaiheelta
1. **Valitse alus** ja tarkista **Sovellettavuus**-rivi.
2. Syötä tarvittaessa kohdassa **Sarakemäppäykset** **Mäppäyksen avain**, kirjoita yksi rivi saraketta kohden (otsikko=kenttä) ja paina **Tallenna uutena versiona**.
3. Valitse kohdassa **Valmistele tuonti** **Mäppäysversio**, **Tarkastuksen viite**, **Tarkastuspäivä** ja **Työkirja (.xlsx)** ja paina sitten **Lähetä tarkastettavaksi**.
4. Toinen henkilö valitsee kohdassa **Tuonnin tarkastus** kullekin muuttuneelle kohdalle **Hyväksy** tai **Säilytä nykyinen**, kirjoittaa vähintään 10 merkin **Peruste**-tekstin ja painaa **Hyväksy tuonti** tai **Hylkää tuonti**.
5. Avaa tarkastus **Tarkastukset**-luettelosta. Tarkastele kohtia **Kohdat** ja **Havainnot**.
6. Linkittääksesi havainnon toimenpiteeseen syötä **Turvallisuustoimenpiteen tunniste** ja paina **Linkitä toimenpide**.
7. Kenttänäyttöä varten syötä **Media**, **Puhtaan tiedoston tunniste**, **Tiedoston revisio** ja **Tiedoston tarkistussumma (SHA-256)** ja paina **Kirjaa kenttänäyttö**. Offline-tilassa painike muuttuu muotoon **Tallenna tälle laitteelle**; lähetä se verkossa painikkeella **Lähetä nyt**.

## Mitä tilat tarkoittavat
- **Säiliöalus: sovellettavissa / Ei sovellu / Alustyyppi tuntematon: tarkastus tarvitaan:** tuntemattomalla laivatyypillä tulos pysyy tilassa **Tarkastus vaaditaan**; sitä ei koskaan oleteta soveltumattomaksi.
- **Odottaa tarkastusta / Hyväksytty / Hylätty:** tuonnin tila.
- **Uusi / Muuttunut / Ennallaan / Ei tässä tuonnissa:** kohta verrattuna aiemmin hyväksyttyyn tuontiin. Kohta, joka ei ole tässä tuonnissa, on **Säilytetään (ei koskaan poisteta)**.
- **Kielteinen havainto / Myönteinen havainto / Ei havaintoa / Tuntematon:** kohdan havainto.
- **Avoin / Todentamaton:** havainnot näkyvät täällä aina avoimina ja todentamattomina.
- **Ei linkitettyä toimenpidettä / Toimenpide linkitetty:** onko havainto linkitetty turvallisuustoimenpiteeseen. Linkitetty toimenpide ei tarkoita, että havainto on todennettu tai toimenpide vaikuttava.
- **Tallennettu verkossa / Kirjattu offline-tilassa:** miten näyttö kirjattiin.

## Mitä et voi tehdä / varoitus
- Tuonnin valmistellut henkilö ei voi tarkastaa sitä: ”Valmistelit tämän tuonnin itse; toisen on tarkastettava se.”
- Tämä sivu ei sulje eikä todenna havaintoja. Todennettu ei ole Vaikuttava; vaikuttavuus on erillinen ihmisen tarkastus toimenpiteessä.
- Videonäyttö ei ole käytettävissä, ennen kuin operaattori ottaa käyttöön skannatun videotiedostotyypin.
- Myöhemmästä tuonnista puuttuvaa kohtaa ei koskaan poisteta; historia säilytetään.

## Jos jokin menee vikaan
- **Pyyntö hylättiin:** ratkaise sen alla luetellut syyt.
- **Tietue muuttui sillä välin. Lataa uudelleen ja tarkista.** Paina **Lataa uudelleen**.
- **Lopputulos on tuntematon (yhteys katkesi).** Käytä painiketta **Lähetä sama pyyntö uudelleen**.
- **Sinulla ei ole oikeutta lukea toimialan varmennustietoja.** tai **Sinulla ei ole oikeutta tähän.** Tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- Jos näyttöä ei voida todentaa, tarkista tiedoston tunniste, revisio ja SHA-256-tarkistussumma; ota tarvittaessa yhteyttä todentajaan (13.03 §10).

## Liittyvät sivut
