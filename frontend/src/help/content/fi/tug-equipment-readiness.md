---
key: tug-equipment-readiness
title: Hinausvarusteiden valmius
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tämä sivu näyttää valitun hinaajan **hinausvarusteiden ilmoitukset** (hinausvinssi, hinauskoukku, hinausköysi, hätävapautus, sammutusjärjestelmä FiFi) ja niiden **Konepäällikön varmennukset**. Se vastaa kysymykseen: ”Mitkä ovat kunkin varusteen SWL, testipäivät, kunto ja kriittisyys, kuka sen varmensi ja mitä valmiustarkastus sanoo?” Arvio on vain neuvoa-antava ja syöttää valmiustarkastusta; se ei ole GO-päätös.

## Kuka sitä käyttää
- **Hinaajan päällikkö:** tarkistaa vinssin, hinausköyden, hinauskoukun / ketjustopparin, hätävapautuksen, FiFi:n ja kriittisten vikojen näytön (13.03 §3).
- **Konepäällikkö:** varmentaa tai jättää varmentamatta ilmoitukset ja kirjaa kriittisen vian vaikutuksen valmiuteen (13.03 §5).

## Ennen kuin aloitat
- Avaa hinaaja kohdasta **Hinaajatoiminnot**; tämä näkymä ei vaadi osoitusta.
- Ilmoituksen lisääminen tai muokkaaminen vaatii hinaajan kirjoitusoikeuden ja kaluston kirjoitusoikeuden.
- Varmennuksen kirjaaminen edellyttää kyseiselle hinaajalle palvelimelle kirjattua **Konepäällikkö**-nimitystä.

## Vaihe vaiheelta
1. Lue kokonaismerkki riviltä **Arvio**.
2. Tarkista kustakin varustekortista tulosmerkki, syykoodi, **SWL (t)**, **Seuraava testi erääntyy**, **Kunto** ja **Kriittinen**.
3. Paina uutta ilmoitusta varten **Lisää varuste**; täytä **Varusteen tyyppi**, **Nimike**, **SWL (t)**, **Viimeisin testi**, **Seuraava testi erääntyy**, **Kunto**, **Kriittinen**, **PMS-kohteen tunnus**, **Todistuksen tunnus** ja **Huomautukset** ja paina sitten **Tallenna ilmoitus**.
4. Muuta olemassa olevaa ilmoitusta sen kortin painikkeella **Muokkaa**.
5. Valitse konepäällikkönä **Nimitys**, kirjoita peruste ja käytä valintaa **Varmennettu** tai **Ei varmennettu** toiminnolla **Kirjaa varmennus**. ”Ei varmennettu” vaatii perusteen.
6. Näet varmennushistorian versioineen ja aikoineen kohdassa **Konepäällikön varmennukset** ja alareunassa taulukon **Lähdetarkistukset**.

## Mitä tilat tarkoittavat
- **Valmis / Ehdollinen / Ei valmis / Tarkastus vaaditaan:** neuvoa-antava arvio. Valmis ei ole GO-päätös.
- **Hyväksytty / Ei läpäissyt / Tuntematon:** yhden varusteen tarkastustulos.
- **Käyttökuntoinen / Heikentynyt / Vikaantunut / Tuntematon:** ilmoitettu kunto. Jos kuntoa ei valita, se pysyy tilassa ”Tuntematon”; sitä ei lasketa käyttökuntoiseksi.
- **Ei arvioitu:** kriittisyyttä ei ole arvioitu. Tämä ei tarkoita ”Ei”.
- **Varmennettu / Ei varmennettu:** konepäällikön ihmisen tekemä kirjaus.
- **myöhempi versio korvasi:** varmennus kuului ilmoituksen vanhempaan versioon; nykyinen versio tarvitsee uuden varmennuksen.

## Mitä et voi tehdä / varoitus
- Tallentaminen kirjaa todentamattoman ilmoituksen. Jokainen muutos vaatii uuden konepäällikön varmennuksen.
- Kirjauksen päättäminen ja sen varmentaminen eivät ole sama toimenpide; varmennus odottaa valtuutettua henkilöä (13.03 §1).
- Varusteen tyyppiä ei voi muuttaa tallentamisen jälkeen; lisää toiselle tyypille uusi ilmoitus.
- Sivu ei koskaan julista hinausvarusteita turvallisiksi; päätös pysyy hinaajan päälliköllä (13.03 §3).
- Komennot ovat poissa käytöstä offline-tilassa.

## Jos jokin menee vikaan
- **Viesti ”The record changed”:** tarkastele nykyistä ilmoitusta painikkeella **Lataa uudelleen**.
- **Viesti ”The outcome is uncertain”:** **Yritä uudelleen** lähettää saman komennon.
- **Kirjausta ei voi varmentaa:** tarkista näyttö ja PMS-/todistuslähde; ota yhteyttä varmentajaan (13.03 §10).
- **Ei valtuutusta:** tarkista roolisi, hinaajasi ja nimityskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
