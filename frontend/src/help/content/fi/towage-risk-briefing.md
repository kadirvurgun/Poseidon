---
key: towage-risk-briefing
title: Hinausriski ja tiedotustilaisuus
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tällä sivulla laaditaan ja julkaistaan lähetetyn hinaajatyön **hinausriski- ja tiedotustilaisuuskirjaus** ja kerätään osallistujien **kuittaukset**. Se vastaa kysymykseen: ”Mistä olosuhteista, vaaroista, ehkäisevistä ja lieventävistä esteistä, työn keskeytysvaltuudesta ja hätäjärjestelyistä tiedotettiin, mikä on linkitetyn riskinarvioinnin tila ja onko jokainen osallistuja kuitannut julkaistun version?” Tiedotustilaisuuden julkaiseminen on nimitetyn päällikön tai hinaajan päällikön ihmisen tekemä kirjaus; se ei ole GO-päätös eikä lupa. Puutteellinen tiedotustilaisuus estää READY-tilan.

## Kuka sitä käyttää
- **Hinaajan päällikkö / Päällikkö:** laatii ja julkaisee tiedotustilaisuuden ja varmistaa, ettei READY-päätöstä tehdä ennen kuin riski ja tiedotustilaisuus ovat valmiit (13.03 §3).
- **Miehistö ja muut osallistujat (luotsi, perämies, kansimiehistö, konemestari):** kuittaavat tai kiistävät julkaistun tiedotustilaisuuden oikeassa roolikontekstissa (13.03 §6).
- **Hinaajaoperaatioiden koordinaattori:** seuraa tiedotustilaisuuden ja kuittausten tilaa (13.03 §7).

## Ennen kuin aloitat
- Avaa hinaaja kohdasta **Hinaajatoiminnot** ja valitse työ kohdasta **Valitse lähetetty hinaustyö**.
- Työn **Hinaussuunnitelma** on oltava hyväksytty ja linkitetty riskinarviointi hyväksytty; muuten tiedotustilaisuutta ei voi julkaista.
- Julkaiseminen edellyttää koordinoivalle hinaajalle palvelimelle kirjattua **Nimitys**-tietoa (hinaajan päällikkö tai päällikkö). Julkaiseminen vaatii myös riskinarvioinnin lukuoikeuden ja koordinoivan hinaajan kirjoitusoikeuden. Kuittaus on vain osallistujaluettelossa oleville henkilöille.

## Vaihe vaiheelta
1. Lue ylimmältä riviltä **Tila**, **Versio**, **Kuittaus**, **Riskin tila** ja **Suunnitelman tila**.
2. Näet kohdassa **Valmiuden lähtötieto (puutteellinen tiedotustilaisuus estää READY-tilan)** valmiustarkastukseen lähetetyt tulokset ja syykoodit.
3. Täytä **Tiedotustilaisuudessa vahvistetut olosuhteet** (sää, virtaus, näkyvyys), **Vaarat**, **Ehkäisevät esteet**, **Lieventävät esteet**, **Työn keskeytysvaltuus** ja **Hätäjärjestelyt**.
4. Syötä **Linkitetyn riskinarvioinnin tunnus**; syötä kohdassa **Osallistujat** painikkeella **Lisää** kunkin osallistujan **Henkilön tunnus** ja **Rooli**.
5. Tallenna painikkeella **Luo luonnos** tai **Tallenna luonnos**; kun **Puuttuu ennen lähettämistä** on tyhjä, valitse **Nimitys** ja paina **Julkaise tiedotustilaisuus**.
6. Lue osallistujana julkaistu versio ja paina **Kuittaa**. Jos olet eri mieltä, kirjoita vähintään 10 merkin **Peruste** ja paina **Kiistä**.
7. Jos tiedotustilaisuutta on muutettava, kirjoita peruste ja käytä painiketta **Muuta**. Luettelot **Kuittaukset** ja **Versiot** näyttävät historian.

## Mitä tilat tarkoittavat
- **Luonnos / Julkaistu:** tiedotustilaisuuden tila. Julkaistu tarkoittaa, että kuittaukset voivat alkaa; se ei yksinään ole valmiustulos.
- **Ei julkaistu / Odottaa / Valmis / Kiistetty:** osallistujien kuittausten tila. Tila on **Valmis** vasta, kun jokainen osallistuja on kuitannut julkaistun version.
- **Hyväksytty ja kuitattu / Huomio / Estetty / Tarkastus vaaditaan:** linkitetyn riskinarvioinnin tila.
- **Hyväksytty / Ei läpäissyt / Tuntematon:** valmiustarkastukseen lähetetty tarkastustulos. Puuttuva tai julkaisematon tiedotustilaisuus ja odottavat kuittaukset ovat **Ei läpäissyt** -tulos, joka estää READY-tilan.

## Mitä et voi tehdä / varoitus
- READY-päätöstä ei tehdä ennen kuin riski ja tiedotustilaisuus ovat valmiit ja osallistujien kuittaukset on kirjattu (13.03 §3).
- Kiistäminen muuttaa kuittausten tilaksi **Kiistetty** ja lähettää valmiustarkastukseen Fail-tuloksen, jolloin READY ja GO estyvät. Peruste näkyy kohdassa **Kuittaukset**. Kuittausta ei voi muuttaa saman version osalta; kiistan poistamiseksi tiedotustilaisuutta muutetaan (uusi versio), se julkaistaan uudelleen ja jokainen osallistuja kuittaa sen uudelleen.
- Jos suunnitelma tai riskinarviointi muuttuu tiedotustilaisuuden jälkeen, tiedotustilaisuus on tarkastettava uudelleen; aiemmat kuittaukset eivät päde uuteen versioon.
- Kukaan ei kuittaa toisen puolesta; jokainen osallistuja kirjaa oman kuittauksensa (13.03 §6).
- Tältä sivulta ei lähetetä komentoja offline-tilassa. Kuittauksen voi asettaa jonoon sivulla **Hinaajan offline-operaatiot**.

## Jos jokin menee vikaan
- **Viesti ”Pakollisia osioita puuttuu.”:** täydennä puuttuvat koodit; tarkista, että suunnitelma ja riskinarviointi on hyväksytty.
- **Viesti ”The record changed”:** tarkastele nykyistä tiedotustilaisuutta painikkeella **Lataa uudelleen**.
- **Viesti ”The outcome is unknown”:** **Yritä uudelleen** lähettää saman pyynnön uudelleen.
- **Ei valtuutusta:** tarkista roolisi, hinaajasi ja nimityskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
