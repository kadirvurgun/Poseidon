---
key: tug-command
title: Hinaajan komento
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Mihin tätä sivua käytetään
Valikossa nimellä **Hinaajan komento** näkyvä sivu näyttää valitun hinaajan **lähdekontekstin**. Se vastaa kysymykseen: ”Mitkä rooli-, tyyppi-, vetovoima- (BP) ja kalustoilmoitukset tälle hinaajalle on kirjattu, mistä ne tulevat ja kuinka ajantasaisia ne ovat?” Sivu näyttää vain ilmoitukset ja niiden lähteet; se ei anna operatiivista valmiutta, kelpoisuutta, lupaa eikä turvallisuushyväksyntää. Ylhäällä oleva merkki sanoo sen: ”Lähdeilmoitukset · operatiivinen valmius tuntematon”.

## Kuka sitä käyttää
- **Hinaajan päällikkö:** vahvistaa oman hinaajansa roolin, BP:n ja kaluston kontekstin ennen työn alkua (13.03 §3).
- **Hinaajaoperaatioiden koordinaattori / Maaoperaatiot:** varmistavat olevansa oikeassa hinaajakontekstissa ennen siirtymistä työtaululle ja työn vastaanottoon (13.03 §7).
- **Miehistö:** näkee oman roolinäkymänsä ja heille avoimet laivatilan näkymät (13.03 §6).

## Ennen kuin aloitat
- Tarvitset hinaajatilan ja hinaajan lukuoikeuden. Ilman niitä sivu näyttää viestin ”Pääsy ei ole käytettävissä.”
- Jos kontekstia ei ole valittu, sivu luettelee valtuutetut hinaajat kohdassa **Valitse hinaaja**; avaa yksi.
- Kalustoluettelo vaatii myös kaluston lukuoikeuden; ilman sitä kaluston lähde näkyy merkinnällä ”unavailable or not granted”.

## Vaihe vaiheelta
1. Avaa hinaaja luettelosta **Valitse hinaaja**. Otsikon alla oleva rivi **Hinaajakonteksti** näyttää hinaajan koodin, nimen ja tilan.
2. Lue kohdista **Rooli (palvelimen määritys)** ja **Hinaajatilan valikkonäkymä** palvelimen sinulle määrittämä rooli ja valikkonäkymä.
3. Näet kohdasta **Tilan näkyvyys**, ovatko laivatilan näkymät avoinna. Osio **Laivatilan näkymät** linkittää vain sinulle myönnettyihin näkymiin.
4. Tarkista kohdista **Hinaajatyyppi**, **Type source / revision** ja **Type source observation / policy**, mistä lähteestä ja versiosta tyyppi tulee.
5. Lue **Profiilin BP (todentamaton)**, **BP source / version / provenance** ja **BP freshness / observation**. Tämä on ilmoitettu arvo, ei mitattu.
6. Näet kohdassa **Kalustokonteksti** kalustoilmoitukset, niiden tasot, versiot ja alkuperän.
7. Siirry sivun välilehdillä kohtaan **Työtaulu** tai (kirjoitusoikeudella) **Työn vastaanotto**, tai paina (kirjoitusoikeudella) **Uusi pyyntö**.

## Mitä tilat tarkoittavat
- **Tuntematon — ei roolimääritystä:** palvelimella ei ole sinulle roolimääritystä tällä hinaajalla. Tämä ei tarkoita ”ei roolia, ei ongelmaa”; ylläpitäjän määritys tarvitaan.
- **Roolinimet eivät ole saatavilla / Valikkonäkymä ei ole saatavilla:** lähdettä ei voi nyt lukea. Puuttuvaa tietoa ei tulkita tyhjäksi.
- **Hinaajatilalle ei ole määritetty näkymää:** sinulle ei ole määritetty valikkonäkymää tässä tilassa.
- **Piilotettu hinaajatilassa. Tilojen välinen rooli vaaditaan.:** laivatilan näkymät vaativat nimenomaisesti valtuutetun tilojen välisen roolin.
- **Profiilin BP (todentamaton):** ilmoitettu vetovoima. Se ei ole todennettu testitulos.
- **Kalustoilmoituksia ei palautettu. Todennettu operatiivinen soveltuvuus pysyy tuntemattomana.:** tyhjä luettelo ei koskaan tarkoita, että kalusto olisi soveltuva.
- **Tuntematon / Ei käytettävissä:** lähdearvo puuttuu tai sitä ei voitu lukea. Tuntematon ei koskaan korvaa arvoa ”Valmis” tai ”Ei”.

## Mitä et voi tehdä / varoitus
- Tällä sivulla ei muokata mitään kenttää eikä tuoteta valmiustulosta. Operatiivinen päätös ja turvallisuusvaltuus pysyvät aluksella olevalla hinaajan päälliköllä (13.03 §3).
- Maalta tehty osoitusehdotus ei koskaan ohita hinaajan päällikön aluksella tekemää turvallisuuspäätöstä (13.03 §7).
- Vaikka tilojen välinen oikeus olisi ilmoitettu, jokainen laivatilan näkymä vaatii silti oman palvelinvaltuutuksensa.
- Offline-tilassa nykyisiä profiilin lähteitä ei näytetä; muodosta yhteys uudelleen tarkistaaksesi käyttöoikeudet ja lähteiden versiot.

## Jos jokin menee vikaan
- **Pääsy ei ole käytettävissä:** tarkista roolisi, hinaajasi ja kohteen konteksti; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Hälytys ”Current identity and vessel access must be checked”:** paina **Tarkista käyttöoikeus uudelleen**.
- **Tietoja ei saatavilla:** tarkista yhteytesi ja paina **Yritä uudelleen**.
- **Odottamattomat BP- tai kaluston arvot:** kirjaa lähde- ja versiokentät muistiin ja tarkastele niitä lähteen omistajan kanssa; jos kirjausta ei voi todentaa, tarkista sen näyttö ja alkuperä (13.03 §10).

## Liittyvät sivut
