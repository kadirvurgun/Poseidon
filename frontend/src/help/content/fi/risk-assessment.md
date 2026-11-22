---
key: risk-assessment
title: Riskinarviointi
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Riskinarviointi-sivulle kirjataan aluksella tehtävän työn tai operaation vaarat, mahdolliset seuraukset, hallintakeinot, alku- ja jäännösriski sekä työn keskeytysehdot. Se vastaa kysymykseen: ”Onko tämän työn vaarat ja hallintakeinot käyty läpi, tarkastettu ja osallistujien kuittaamat?” Sivu on **vain kehityskohde**: täällä annettu hyväksyntä on kirjattu ihmisen tarkastus, ei työlupa, asiantuntijan riskin hyväksyntä eikä HSE-portin läpäisy (13.03 §2, §4).

## Kuka sitä käyttää
- **Päällikkö ja hinaajan päällikkö:** teettävät arvioinnin laivalla tai hinaajalla tehtävää työtä varten ja tekevät työn keskeytyspäätöksen. Päätösvalta pysyy heillä (13.03 §2, §3).
- **DPA / HSEQ:** valvovat riskienhallintaa ja kirjaavat tarkastuspäätöksen (13.03 §4).
- **Crew / OOW / Engineer:** osallistujina kuittaavat hyväksytyn arvioinnin tai kiistävät sen omasta puolestaan (13.03 §6).

## Ennen kuin aloitat
- Valitse alus avataksesi sen riskinarvioinnit. Lukeminen vaatii riskien lukuoikeuden; luonnoksen kirjoittaminen vaatii riskien kirjoitusoikeuden ja kyseisen aluksen muokkauslaajuuden. Tarkastuspäätös vaatii riskien hyväksymisoikeuden; kuittaus vaatii riskien kuittausoikeuden.
- Luokitukset vaativat käyttöalueella julkaistun riskimenetelmän (matriisin). Ilman sitä luokitukset jäävät tilaan **Ei arvioitu**. Käyttöalueen HSEQ-omistaja määrittää matriisin ja toinen henkilö julkaisee sen (Riskimallikirjasto-sivu).
- Riskikomennot vaativat voimassa olevan online-yhteyden; offline-tilassa mitään ei aseteta jonoon eikä hyväksytä.

## Vaihe vaiheelta
1. Valitse alus ja avaa arviointi luettelosta tai paina **Uusi arviointi**.
2. Syötä **Arvioinnin numero**. Voit syöttää **Työn tyyppi** -kentän ja valita sopivan mallin kohdasta **Aloita julkaistusta mallista (vain kopio)**; malli vain kopioidaan uuteen luonnokseen.
3. Täytä **Otsikko**, **Toiminta- / työkonteksti**, **Työn tyyppi**, **Työn laajuus** ja **Riskimenetelmä**. Valitse kohdassa **Työn keskeytys vaaditaan** **Kyllä**, **Ei** tai **Tuntematon** ja kirjoita tarvittaessa **Stop-work conditions**.
4. Syötä painikkeella **Lisää vaara** jokaiselle vaaralle **Työvaihe**, **Potential consequences**, **Alkuriski** ja **Jäännösriski** (Todennäköisyys, Vakavuus, Perustelu). Lisää painikkeella **Lisää hallintakeino** **Hallintakeinot**, **Hallintakeinon vastuuhenkilö** ja **Barrier references**.
5. Kirjoita kohtaan **Osallistujat** yksi toimijatunnus ja rooli riviä kohden. Linkitä tarvittaessa liittyvät tietueet painikkeella **Lisää linkki**.
6. Paina **Tallenna luonnos** ja sitten **Lähetä tarkastettavaksi**. **Estävät kohdat** luettelee, mitä puuttuu.
7. Tarkastaja kirjoittaa vähintään 10 merkin **Peruste**-tekstin ja painaa **Hyväksy (ihmisen tarkastustietue)** tai **Palauta**.
8. Hyväksynnän jälkeen jokainen osallistuja painaa omasta puolestaan **Kuittaa** tai perusteen kanssa **Kiistä**. Jos muutos tarvitaan, käytä painiketta **Muuta (avaa uuden luonnoksen)**.

## Mitä tilat tarkoittavat
- **Luonnos / Tarkastuksessa / Palautettu / Hyväksytty (ihmisen tietue):** arvioinnin elinkaari. Hyväksytty on ihmisen tarkastustietue, ei työlupa.
- **Ei arvioitu / Menetelmää ei määritetty:** luokitusta ei ole. Järjestelmä ei käytä oletusmatriisia, kertolaskua eikä matala/keskitaso/korkea-rajaa.
- **Työn keskeytys vaaditaan — Tuntematon:** päätöstä ei ole vielä tehty; sitä ei koskaan lueta tilaksi ”Ei”.
- **Kuittaus ei sovellu ennen hyväksyntää / Kuittaukset odottavat / Kaikki osallistujat kuittasivat / Kiistetty – ratkaisematta:** osallistujien kuittaustila. Kiistää ei koskaan suljeta jonkun toisen kuittauksella.
- **Muuttunut kiinnityksen jälkeen / Ei käytettävissä:** linkitetty tietue on muuttunut kiinnitetyn revision jälkeen tai sitä ei voi enää lukea; tarkasta se uudelleen.

## Mitä et voi tehdä / varoitus
- Tarkastuskierroksen laatija tai lähettäjä ei voi hyväksyä samaa arviointia (tehtävien eriyttäminen).
- Jokainen kuittaa vain omasta puolestaan ja vain täsmälleen hyväksytyn revision.
- Täällä annettu hyväksyntä ei ole työlupa, asiantuntijan riskin hyväksyntä eikä HSE-portti. Päätös työn aloittamisesta tai keskeyttämisestä pysyy päälliköllä tai hinaajan päälliköllä.
- Mallista aloitetussa luonnoksessa ei ole luokitusta, allekirjoitusta, osallistujien vahvistusta eikä hyväksyntää.
- Offline-tilassa arviointia ei voi lähettää, hyväksyä eikä kuitata.

## Jos jokin menee vikaan
- **Estetty, kunnes luetellut kohdat on täydennetty.** Täydennä estävät kohdat ja lähetä uudelleen.
- **This record changed elsewhere.** Paina **Lataa uudelleen** nähdäksesi nykyisen revision ja toista toiminto.
- **Sinulla ei ole oikeutta tähän toimintoon (tehtävien eriyttäminen tai laajuus).** Pyydä toista valtuutettua henkilöä tarkastamaan tai tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Riskityötila ei ole käytettävissä nykyisillä oikeuksillasi.** Tarkista oikeutesi.
- Jos luokitus näyttää odottamattomalta, tarkista riskimenetelmä ja sen revisio; kysy hallintakeinon vastuuhenkilöltä tai HSEQ-omistajalta (13.03 §10).

## Liittyvät sivut
