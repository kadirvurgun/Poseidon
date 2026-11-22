---
key: companies
title: Yritykset
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## Mihin tätä sivua käytetään
Yritykset-sivu listaa työtilasi ne **yritystietueet**, jotka sinulla on oikeus nähdä. Se vastaa kysymykseen: ”Mitkä yritykset tähän työtilaan on määritelty, ja mitkä ovat niiden koodi ja nimi?” Alus- ja hinaajatietueet kuuluvat yritykselle, joten oikea yritys on kontekstin valinnan ensimmäinen vaihe.

## Kuka sitä käyttää
- **Tenant Admin:** luo ja muokkaa yritystietueita vähimpien oikeuksien periaatteella (13.03 §9).
- **Maaoperaatiot:** löytävät oikean yrityskontekstin.

## Ennen kuin aloitat
- Tämän sivun näkeminen vaatii yritysten lukuoikeuden. Ilman sitä näytetään ilmoitus ”Sinulla ei ole pääsyä tähän työtilaan.”
- Tietueen luominen vaatii luontioikeuden. Se, voitko muokata tietuetta, riippuu palvelimen kyseiselle tietueelle myöntämästä oikeudesta.

## Vaihe vaiheelta
1. Näet luettelossa kunkin yrityksen koodin ja nimen. Käytä painikkeita **Edellinen** / **Seuraava** muille sivuille.
2. Hae kirjoittamalla kenttään **Hae koodilla tai nimellä** ja painamalla **Hae**.
3. Valitse yrityksen nimi avataksesi **Tiedot**-osion.
4. Uutta yritystä varten paina **Uusi tietue**, kirjoita koodi ja nimi ja paina **Tallenna**. Kaikki kentät ovat pakollisia.
5. Muuta olemassa olevaa tietuetta tietojen **Muokkaa**-painikkeella.
6. Avaa **Näytä oikeushenkilön tietue** nähdäksesi oikeushenkilön tiedot; tämä osio on vain luku -tilassa.

## Mitä tilat tarkoittavat
- **Tietueita ei löytynyt:** hakusi tai oikeusalueesi perusteella ei palautunut yhtään tietuetta.
- **Oikeushenkilö: Luonnos – keskeneräinen, ei aktiivinen / Aktiivinen / Ei aktiivinen:** oikeushenkilötietueen tila. Luonnos tarkoittaa, että arvoja puuttuu vielä ennen aktivointia.
- **Tuntematon:** arvo ei ole tietueessa. Tuntematon ei tarkoita ”Ei” eikä ”pätevä”.
- **Tallennettu:** tietue kirjoitettiin palvelimelle. Tämä tietue ei luo mitään operatiivista hyväksyntää.

## Mitä et voi tehdä / varoitus
- Yritystietueen luominen tai muokkaaminen ei ole operatiivinen, vaatimustenmukaisuus- tai turvallisuushyväksyntä.
- Oikeushenkilön arvoja ei muuteta tältä sivulta; ne muuttuvat vain auditoitujen HR-komentojen kautta. Rekisteritunnisteet näytetään peitettyinä.
- Kaksi yritystä ei voi käyttää samaa koodia.
- Jos poistut sivulta tallentamattoman luonnoksen kanssa, sinulta kysytään, hylätäänkö se vai jatketaanko muokkausta.

## Jos jokin menee vikaan
- **Tietue muuttui toisessa toiminnossa:** luonnoksesi säilyy. Hylkää luonnos ladataksesi nykyisen tietueen ja tee muutoksesi uudelleen.
- **Koodi on jo käytössä:** syötä eri koodi.
- **Lopputulosta ei voitu vahvistaa:** kentät pysyvät lukittuina, kunnes lopputulos tiedetään; toista sama komento turvallisesti.
- **Oikeutesi on voinut muuttua / Ei pääsyä:** tarkista roolisi ja työtilakontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Palvelu ei ole käytettävissä:** käytä painiketta ”Yritä uudelleen”; jos ongelma jatkuu, ilmoita siitä tukiviitteen kanssa.

## Liittyvät sivut
