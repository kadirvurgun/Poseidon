---
key: audit
title: Toimintahistoria
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Toimintahistoria-sivu näyttää **vain luku** -muotoisen, tekijään yhdistetyn kirjauksen liiketoimintatietueiden muutoksista valtuutetulla käyttöalueellasi. Se vastaa kysymykseen: ”Milloin tämä tietue muuttui, millä toiminnolla ja minkä toimijan ja roolikontekstin alla?” Tietoturvatapahtumat on rajattu erikseen, eivätkä ne näy täällä (13.03 §4, §9).

## Kuka sitä käyttää
- **DPA / HSEQ:** jäljittää havainto-, toimenpide- ja todentamistietueita (13.03 §4).
- **Regulatory / Assurance -käyttäjä:** tarkistaa tietueen historian ennen auditointia tai tarkastusta (13.03 §8).
- **Tenant Admin:** auditoi jäsenyys- ja oikeusmuutoksia sekä delegointien alkamis- ja päättymisaikoja (13.03 §9).

## Ennen kuin aloitat
- Katselu vaatii auditoinnin lukuoikeuden. Ilman sitä näet ilmoituksen ”Auditointioikeus vaaditaan.”
- Vain ne tietueet listataan, jotka kuuluvat sinulle valtuutettuun vuokralais- ja aluskäyttöalueeseen.
- Jos etsit tiettyä tietuetta, pidä sen **Aluksen tunniste** tai **Kohteen tunniste** valmiina. Muiden sivujen historialinkit avaavat tämän sivun valmiiksi suodatettuna.

## Vaihe vaiheelta
1. Täytä suodattimet: **Aluksen tunniste**, **Kohteen tunniste**, **Kohteen tyyppi** ja **Toiminto** (**Kaikki**, **Luotu**, **Päivitetty**, **Poistettu**).
2. Paina **Suodata**. Palauta alkutila painikkeella **Tyhjennä suodattimet**.
3. Tarkista **Aika**-, **Kohde**- ja **Toiminto**-sarakkeet kohdassa **Valtuutetut tietueet**. Siirry sivujen välillä painikkeilla **Ensimmäinen sivu** ja **Seuraava**.
4. Avaa **Tietueen tiedot** painamalla rivin kohdelinkkiä. Lue **Toimijan viite**, **Osoitettujen roolien viitteet**, **Luotetut valtuudet**, **Peruste**, **Metatiedot**, **Resurssin versio** ja **Korrelaatio**.
5. Jos muuttumattoman version todiste on olemassa, näet tietueen sellaisena kuin se oli painamalla **Avaa täsmällinen muuttumaton versio**. Sulje tiedot painamalla **Sulje**.

## Mitä tilat tarkoittavat
- **Luotu / Päivitetty / Poistettu:** tietueeseen kohdistuneen toiminnon laji.
- **Kirjattu:** metatiedot tallennettiin kokonaan.
- **Historialliset metatiedot puutteellisia / Historiallinen tunniste ei ole käytettävissä / Historiallinen konteksti ei ole käytettävissä:** vanhemmat tietueet eivät säilyttäneet tätä tietoa. Puuttuvaa tietoa ei koskaan täydennetä arvaamalla.
- **Arvoja ei luovuteta:** kentän arvo on piilotettu yksityisyyden vuoksi; vain muutoksen tosiasia näytetään.
- **Muuttumattoman version todiste ei ole käytettävissä tälle tietueelle.** Tälle tietuetyypille ei ole versiotilannekuvaa.

## Mitä et voi tehdä / varoitus
- Sivu on vain luku -tilassa. Toimintahistorian merkintää ei voi muokata eikä poistaa.
- Tietueen näkyminen historiassa ei tarkoita, että työ olisi todennettu tai vaikuttava; Todennettu ≠ Vaikuttava (13.03 §4).
- Tämä sivu kirjaa liiketoimintamuutoksia. Yksinään se ei ole kattava käyttäjätoimintojen auditointi eikä todiste tietosuojan vaatimustenmukaisuudesta; tietoturvatapahtumat säilytetään erikseen.
- Luettelo näyttää vain sinulle valtuutetun käyttöalueen. Käyttöalueesi ulkopuolisia tietueita ei listata; se, ettet näe tietuetta, ei tarkoita, ettei sitä olisi koskaan muutettu.

## Jos jokin menee vikaan
- **Auditointioikeus vaaditaan.** Tarkista roolisi ja käyttöalueesi; ota tarvittaessa yhteyttä Tenant Admin -käyttäjään (13.03 §10).
- **Kirjaudu sisään jatkaaksesi.** Kirjaudu uudelleen.
- **Historia ei ole käytettävissä. Tarkista suodattimet ja yritä uudelleen.** Tarkista, että tunnisteet ovat täydelliset ja oikeat, ja paina sitten **Yritä uudelleen**.
- **Mikään valtuutettu tietue ei vastaa näitä suodattimia.** Laajenna suodattimia.
- Jos sovellusvirhe jatkuu, ilmoita siitä tukeen korrelaatiotunnisteen, näkymän, ajan ja vaiheidesi kanssa (13.03 §10).

## Liittyvät sivut
