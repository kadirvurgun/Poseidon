---
key: tug-daily-log
title: Hinaajan päiväkirja
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## Mihin tätä sivua käytetään
Hinaajan päiväkirja kirjaa yhdelle päivälle ja vahdille **työt, koneiden käyntituntimittarit, polttoaineen ja viat** ja vertaa niitä päivän hinaussuorituksiin **täsmäytystä** varten. Se vastaa kysymykseen: ”Montako työtä tänä päivänä/vahdissa päätettiin, vastaavatko mittarituntien lukemat suoritusaikoja, paljonko polttoainetta käytettiin ja mitä vikoja ilmoitettiin?” Täsmäytys on neuvoa-antava; päällikön tarkastus on erillinen ihmisen tekemä kirjaus.

## Kuka sitä käyttää
- **Miehistö / vahtihenkilö:** pitää vahtikirjausta reaaliajassa ja järjestyksessä (13.03 §6).
- **Hinaajan päällikkö / Päällikkö:** tarkastaa lähetetyn päiväkirjan ja varmentaa tai palauttaa sen (13.03 §3).

## Ennen kuin aloitat
- Avaa hinaaja kohdasta **Hinaajatoiminnot**.
- Päiväkirjan luominen ja muokkaaminen vaatii hinaajan kirjoitusoikeuden.
- Tarkastus edellyttää kyseiselle hinaajalle palvelimelle kirjattua **Nimitys**-tietoa (hinaajan päällikkö tai päällikkö). Päiväkirjan kirjoittanut tai lähettänyt henkilö ei voi tarkastaa sitä.

## Vaihe vaiheelta
1. Avaa olemassa oleva päiväkirja luettelosta tai valitse **Päiväkirjan päivämäärä** ja **Vahti** ja paina **Luo päiväkirja**.
2. Syötä **Aikavyöhyke (IANA)**.
3. Syötä kohdassa **Koneiden käyntituntimittarit** kullekin **Mittari**-kohdalle **Alkulukema**, **Loppulukema** ja tarvittaessa **Nollauksen tai ylivuodon näyttö**.
4. Syötä kohtaan **Käytetty polttoaine** arvo, yksikkö ja **Polttoainelähteen viite**.
5. Merkitse kohdassa **Päivän hinaussuoritukset** tähän päiväkirjaan kuuluvat suoritukset; täytä **Linked defect identifiers** ja **Päivän yhteenveto** (yhteenveto vähintään 20 merkkiä).
6. Paina **Tallenna**. Tarkista rivillä **Täsmäytys** päätetyt työt, suoritustunnit, mittaritunnit ja ilmoitetut viat.
7. Kun **Esteet**-luettelo on tyhjä, paina **Lähetä päällikön tarkastettavaksi**.
8. Valitse päällikkönä **Nimitys**, kirjoita peruste ja paina **Päällikkö: varmenna päiväkirja** tai **Päällikkö: palauta päiväkirja**.

## Mitä tilat tarkoittavat
- **Luonnos / Lähetetty / Palautettu / Varmennettu:** päiväkirjan tila.
- **Master review — Odottaa / Huomio / Tarkastettu:** Odottaa = odottaa tarkastusta; Huomio = palautettu tai lähetetty, vaikka täsmäytys ei ole Täsmäytetty; Tarkastettu = päällikön varmentama. Lähetetty päiväkirja ei ole varmennettu päiväkirja.
- **Täsmäytetty / Tarkastus vaaditaan / Keskeneräinen:** neuvoa-antavan vertailun tulos. Syykoodit luetellaan sen alla.
- **avoin aikaväli:** suoritus on yhä käynnissä; suoritustunnit eivät ole vielä lopullisia.
- **Turvallisuustapahtumat — Tuntematon:** tämä sivu ei laske turvallisuustapahtumia; johtopäätöstä ”ei tapahtumia” ei tehdä.
- **Synkronoitu / Odottaa synkronointia / Ristiriita / Epäonnistui:** synkronoinnin tila. Odottaa synkronointia osoittaa, ettei muutos ole vielä saapunut palvelimelle.

## Mitä et voi tehdä / varoitus
- Päiväkirjan kirjoittanut tai sen päällikön tarkastettavaksi lähettänyt henkilö ei voi varmentaa sitä.
- Täsmäytyksen tulos ei ole hyväksyntä; tarkastuksen tekee päällikkö.
- Kirjauksen päättäminen ja sen varmentaminen eivät ole sama toimenpide (13.03 §1).
- Offline-tilassa yksi tallentamaton muutos säilytetään tämän sivun muistissa ja lähetetään alkuperäisellä avaimellaan, kun yhteys palaa; pidä sivu auki. Lähettäminen ja tarkastus tehdään verkossa.
- Jos ristiriita ilmenee, älä oleta, että oma kirjauksesi voittaa automaattisesti (13.03 §6).

## Jos jokin menee vikaan
- **Viesti ”Palvelin ilmoitti puuttuvia tai estäviä kohteita.”:** poista kohdan **Esteet** koodit.
- **Viesti ”The record changed or the command conflicts.”:** tarkastele nykyistä päiväkirjaa painikkeella **Lataa uusin**; säilytetty paikallinen muutos hylätään.
- **Viesti ”The outcome is unknown.”:** **Yritä uudelleen** lähettää täsmälleen saman komennon uudelleen.
- **Ristiriita:** tälle sivulle säilytettyä muutosta ei koskaan yhdistetä; käytä painiketta **Lataa uusin**. Sivulla **Hinaajan offline-operaatiot** tallennetut ristiriitaiset päiväkirjamerkinnät ratkaistaan siellä; ne näkyvät myös kohdassa **Olennaisten ristiriitojen keskus** (13.03 §10).

## Liittyvät sivut
