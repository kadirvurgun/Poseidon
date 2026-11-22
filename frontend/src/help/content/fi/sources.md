---
key: sources
title: Lähdeluonnokset
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Lähdeluonnokset-sivu säilyttää organisaatiosi käyttämät sääntely- ja sääntölähteet (yleissopimus, lippuvaltio, luokituslaitos, yhtiö tai paikallinen sääntö) **versioituina luonnostietueina**. Se vastaa kysymykseen: ”Minkä virallisen lähteen mihin revisioon tämä vaatimus perustuu?” Ylhäällä oleva huomautus on voimassa: ”Tenant-local DRAFT · Declared and unverified. Dates and hashes confer no legal authority. Source references are stored as text.” (13.03 §8).

## Kuka sitä käyttää
- **Regulatory / Assurance -käyttäjä:** rekisteröi virallisen, versioidun lähteen ja ilmoittaa sen voimaantulopäivän ja elinkaaren (13.03 §8).
- **DPA / HSEQ:** lukee sitä nähdäkseen, mihin lähderevisioon auditointi tai havainto viittaa (13.03 §4).

## Ennen kuin aloitat
- Tarkastelu vaatii koko käyttöalueen lukuoikeuden ja sääntelyluonnosten lukuoikeuden. Luonnosten luominen ja tallentaminen vaativat lisäksi sääntelyluonnosten kirjoitusoikeuden; ilman sitä **Uusi luonnos** -painiketta ei näytetä.
- Alinäkymät vaativat omat oikeutensa, aina koko käyttöalueen lukulaajuudella. **Elinkaari ja soveltuvuus** vaatii soveltuvuuden lukuoikeuden; sääntöjen muokkaus vaatii sääntöjen kirjoitusoikeuden, tarkastuksen pyytäminen sääntöjen tarkastusoikeuden ja arviointi soveltuvuuden arviointioikeuden; pätevöinti ja tosiseikkojen vahvistus vaativat sääntelyn pätevöinti- tai tosiseikkojen vahvistusoikeuden sekä palvelimelle kirjatun nimityksen. **Kontrollit ja näytöt** vaatii kontrollien, näyttöjen, tiedostojen ja soveltuvuuden lukuoikeuden; linkittäminen vaatii kontrollien tai näyttöjen kirjoitusoikeuden ja aluksen muokkausoikeuden, ja hyväksyntä vaatii kontrollien hyväksymisoikeuden.
- Pidä lähteen virallinen teksti ja sen versio- ja muutostiedot käsillä. POSEIDON ei lataa eikä todenna lähdetekstiä; sinä ilmoitat sen.
- Tallennus vaatii toimivan palvelinyhteyden. Offline-tilassa muutoksesi säilyvät välilehdellä mutta niitä ei tallenneta.

## Vaihe vaiheelta
1. Näet kirjatut lähdeluonnokset luettelossa. Hae kirjoittamalla kenttään **Search drafts** ja painamalla **Hae**. Siirry sivulta toiselle painikkeilla **Edellinen** ja **Seuraava**.
2. Paina uudelle lähteelle **Uusi luonnos**.
3. Valitse kohdassa **Draft fields** ensin jokaiselle kentälle **Tila**: **Puuttuu**, **Tuntematon** tai **Known (declared)**. Voit syöttää arvon vasta valittuasi **Known (declared)**. Kentät ovat **Otsikko**, **Ilmoitettu viranomainen**, **Lähdeviite (vain teksti)**, **Versio- / muutosilmoitus**, **Ilmoitettu voimaantulopäivä**, **Ilmoitus / alkuperä** ja **Lähteen havaintoaika (UTC-siirtymällä)**.
4. Paina **Tallenna luonnos**. Onnistuessa näet ilmoituksen ”Draft saved. Unverified.”; avaa tietue painikkeella **Open saved draft and history**.
5. Avaa olemassa oleva luonnos luettelosta, muuta sitä ja tallenna uudelleen. Jokainen tallennus luo uuden **Versio**-merkinnän; vanhemmat versiot säilyvät kohdassa **Muuttumaton historia** sisältöineen ja **Eheystiiviste (ei valtuus)** -arvoineen.
6. Siirry soveltuvuussääntöihin tai kontrolli–näyttö-ketjuun luettelon yläpuolella olevilla painikkeilla **Elinkaari ja soveltuvuus** tai **Kontrollit ja näytöt**. Palaa painikkeella **Back to register**.

## Mitä tilat tarkoittavat
- **LUONNOS:** tallennettu tila, joka näkyy luettelossa ja tietueen otsikossa. Jokainen tämän sivun lähdetietue on luonnos; se ei ole virallinen käyttöönotto eikä voimassaolopäätös.
- **Puuttuu:** kenttään ei ole vielä syötetty tietoa. Myöhemmät vaiheet (esimerkiksi soveltuvuuden arviointi) ovat estettyjä niin kauan kuin vaadittuja tietoja puuttuu.
- **Tuntematon:** tietoa on etsitty, mutta sitä ei tiedetä. Järjestelmä ei koskaan lue tilaa Tuntematon tilaksi ”ei” tai ”ei sovellu”; kun kriittinen tieto on tuntematon, tulos pysyy tarkastuksessa (REVIEW_REQUIRED).
- **Known (declared):** sinä ilmoitit arvon. Ilmoitus ei ole todennettua tietoa.
- **Eheystiiviste (ei valtuus):** osoittaa, ettei revision sisältö ole muuttunut; se ei ole todiste oikeudellisesta pätevyydestä.

## Mitä et voi tehdä / varoitus
- Et voi muuttaa tallennettua revisiota. Kun lähde muuttuu, tallenna uusi revisio; vaatimukset pysyvät sidottuina vanhempaan revisioon ja historia säilyy (13.03 §1).
- Älä oleta, että luokituslaitoksen, lippuvaltion, yhtiön ja paikalliset sääntölähteet korvaavat toisiaan; kirjaa kukin omaksi lähteekseen (13.03 §8).
- Tämä sivu ei virallisesti hyväksy lähdettä eikä anna kenellekään vaatimustenmukaisuus-, lupa- tai valmiustulosta. Lähteen hyväksyntä ja riippumaton tarkastus kuuluvat ihmisten toimivaltaan.
- Voimaantulopäivät ja tiivisteet eivät anna oikeudellista valtuutusta.

## Jos jokin menee vikaan
- **Permission denied.** Tarkista roolisi ja käyttöalueesi; pyydä tarvittaessa apua Tenant Adminilta (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Joku muutti tietuetta ennen sinua. Avaa nykyinen revisio painikkeella **Lataa tallennettu luonnos uudelleen** ja tee muutoksesi uudelleen.
- **Check field states, bounds, dates and source revision.** Kentän arvo tai päivämäärän muoto on virheellinen.
- **Service unavailable. Your draft is retained.** Kun yhteys palaa, lähetä sama komento uudelleen painikkeella **Retry frozen command**.
- **Sign in to continue; retain your draft.** Istuntosi päättyi; käytä linkkiä **Kirjaudu sisään**.
- Jos sovellusvirhe jatkuu, ilmoita tukeen korrelaatiotunnus, näkymä, aika ja vaiheet (13.03 §10).

## Liittyvät sivut
