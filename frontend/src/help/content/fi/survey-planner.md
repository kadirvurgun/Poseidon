---
key: survey-planner
title: Katsastussuunnittelija
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Katsastussuunnittelija näyttää ilmoitetut katsastusikkunat laivaston kalenterissa yhdessä matka- ja satamasopivuuden kanssa. Se vastaa kysymykseen: ”Mitkä todistukset millä aluksilla ovat valitsemallani aikavälillä katsastusikkunassa, jonka avautuminen on edessä tai joka on jo ohi, ja onko valmistelun tarkistuslista olemassa?” Se on vain suunnittelun apuväline: ei viranomaisen aikataulu, varaus eikä katsastuksen tulos (13.03 §8).

## Kuka sitä käyttää
- **Päällikkö, konepäällikkö / tekninen isännöitsijä:** näkevät tulevat katsastukset ja aloittavat valmistelun tarkistuslistan ajoissa (13.03 §5).
- **Regulatory / Assurance -käyttäjä:** seuraa ilmoitettuja ikkunoita ja puuttuvia ilmoituksia koko laivastossa (13.03 §8).

## Ennen kuin aloitat
- Tarkastelu vaatii todistusten lukuoikeuden. Vain alukset, joihin sinulla on pääsy, luetellaan.
- Ikkunat tulevat todistustietueen ilmoituksista. Jos ikkunaa ei ole ilmoitettu, rivi näkyy kohdassa **Ikkunaa ei ilmoitettu**; syötä lähdeasiakirjan päivämäärät sivulla [Todistusten komentokeskus](/app/certificate-command-center).
- Sivu vaatii ajantasaisen verkkoluvun. Offline-tilassa näet ilmoituksen ”Offline-tila: ajantasainen verkkohaku tarvitaan.”

## Vaihe vaiheelta
1. Valitse kohdassa **Kalenterin aikaväli** **Alkaen** ja **Päättyen** ja paina **Näytä**.
2. Lue yhteenvetoriviltä avoimien, tulevien, myöhässä olevien ja tuntemattomien määrät.
3. Tarkista jokaiselta riviltä **Todistus**, **Myöntäjä**, **Ilmoitettu lippu**, **Säädösperuste**, **Ilmoitettu voimassaolo**, **Katsastusikkuna** ja **Matka-/satamasopivuus**.
4. Paina **Avaa todistus** nähdäksesi todistuksen.
5. Käytä painiketta **Avaa tarkistuslista**, jos valmistelulista on olemassa, tai **Aloita tarkistuslista**, jos ei ole, siirtyäksesi sivulle [Katsastusta edeltävä tarkistuslista](/app/pre-survey-checklist).

## Mitä tilat tarkoittavat
- **Tuleva / Ikkuna auki:** sijainti suhteessa ilmoitettuun ikkunaan.
- **Ikkuna ohi, täyttymistä ei kirjattu:** ikkuna on päättynyt, eikä mikään tietue osoita katsastuksen tehdyksi. Se ei myöskään todista katsastuksen jääneen väliin; tarkista tietueet.
- **Ikkuna tuntematon:** päivämäärä puuttuu tai sitä ei voi lukea. Tuntematon ikkuna ei koskaan tarkoita ”ei ongelmaa”; se vaatii tarkastuksen.
- Matka-/satamasopivuus: **Ehdollinen**, **Ei sovi**, **Tuntematon**. Sivu ei koskaan anna ”sopii”-hyväksyntää.
- Tarkistuslistan valmius: **Estetty**, **Keskeneräinen**, **Valmisteltu, odottaa ihmisen vahvistusta**.

## Mitä et voi tehdä / varoitus
- Suunnittelija ei varaa mitään eikä jätä mitään viranomaiselle. Luokituslaitoksen, lippuvaltion ja viranomaisten kanssa koordinointi tapahtuu järjestelmän ulkopuolella, eikä sitä simuloida täällä.
- Kalenterin näyttämä tieto ei ole katsastuksen tulos eikä todistuksen voimassaolo. Matka- ja katsastuspäätökset kuuluvat valtuutetuille henkilöille.
- Sivu on vain luku -tilassa; muutokset tehdään todistus- ja tarkistuslistasivuilla.

## Jos jokin menee vikaan
- **Vain osa laivastosta näytetään; rajaa aikaväliä.** Valitse lyhyempi kalenterin aikaväli.
- **Tällä aikavälillä ei ole todistuksia, joilla on katsastusikkuna.** Laajenna aikaväliä tai tarkista todistusten ilmoitukset.
- **Lataus epäonnistui. Yritä uudelleen.** Paina **Yritä uudelleen**. Jos ongelma jatkuu, ilmoita siitä tukeen ja kerro näkymä, aika ja vaiheet (13.03 §10).
- **Sinulla ei ole pääsyä tähän näkymään.** Tarkista roolisi ja alustesi laajuus; ota tarvittaessa yhteyttä Tenant Adminiin (13.03 §10).

## Liittyvät sivut
