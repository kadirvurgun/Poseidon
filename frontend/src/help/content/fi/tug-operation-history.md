---
key: tug-operation-history
title: Hinaajaoperaatioiden historia
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Operaatiohistoria näyttää hinaajatöiden, suoritusten, päiväkirjojen ja näytön **vain luku -historian**. Se vastaa kysymykseen: ”Mitkä työt päättyivät millä tuloksella, varmennettiinko suoritukset, onko näyttökirjausta olemassa ja mitkä päiväkirjat kuuluvat näihin töihin?” Peruutetut ja hätäkeskeytetyt kirjaukset pysyvät näkyvissä; mitään ei poisteta eikä nimetä uudelleen.

## Kuka sitä käyttää
- **Hinaajaoperaatioiden koordinaattori / Maaoperaatiot:** seuraavat päätettyjen töiden näytön täydellisyyttä, tapahtuma- ja historiakirjauksia (13.03 §7).
- **Hinaajan päällikkö:** näkee oman hinaajansa aiemmat työt ja suoritusten aikajanat (13.03 §3).

## Ennen kuin aloitat
- Avaa hinaaja kohdasta **Hinaajatoiminnot**; ilman valittua hinaajaa sivu näyttää viestin ”Valitse hinaaja jatkaaksesi.”
- Hinaajan lukuoikeus riittää; sivu ei muuta mitään tietoja.
- Historia luetaan palvelimelta; sitä ei voi päivittää offline-tilassa.

## Vaihe vaiheelta
1. Aseta kohdassa **Suodattimet** halutessasi **Hinaajan ID**, **Alkaen (UTC-päivä)**, **Päättyen (UTC-päivä)** ja **Tulos** ja paina sitten **Käytä suodattimia**. Päivämääräväli on enintään 400 päivää.
2. Lue taulukon kultakin riviltä **Työ** (työn numero, avustettava alus, työn tyyppi, suunnitelman ja tiedotustilaisuuden tila), **Toiminnan aika**, **Tulos** ja **Näyttö**.
3. Näet kohdassa **Suoritukset** kunkin suorituksen tilan, varmennustilan, keston, päällikön kirjauksen ja näyttömerkintöjen määrän; siirry suorituskonsoliin painikkeella **Avaa suorituksen aikajana**.
4. Avaa liittyvä päiväkirja kohdan **Päivälokit** linkeistä. Huomautus ”lokiin linkittämättä” osoittaa, ettei päiväkirja linkitä tätä suoritusta.
5. Käytä vanhemmille kirjauksille painiketta **Lataa lisää**. **Luettu** näyttää, milloin tiedot luettiin.

## Mitä tilat tarkoittavat
- **Ei aloitettu / Käynnissä / Valmis (kirjattu) / Hätäkeskeytetty / Peruttu / Sekalainen:** työn kokonaistulos. Valmis (kirjattu) ei tarkoita, että työ olisi varmennettu.
- **Varmennettu, todiste kirjattu / Varmennettu, ei todistemerkintää / Varmennus hylätty / Todiste varmentamaton / Tuntematon:** näytön ja varmennuksen tila.
- **varmennus odottaa / varmennettu / varmennus hylätty:** yksittäisen suorituksen varmennustila.
- **Ei mitään:** tälle työlle ei ole linkitettyä kirjausta. Tämä ei tarkoita, ettei kirjausta olisi tarvittu.

## Mitä et voi tehdä / varoitus
- Tällä sivulla ei muokata, poisteta eikä varmenneta kirjauksia.
- Ei hiljaisia poistoja: peruutetut työt sekä hätäkeskeytetyt tai hylätyt suoritukset pysyvät historiassa.
- Completed ≠ Verified: varmennus on toisen henkilön kirjaus.
- Näytetyt tiedot ovat tila hetkellä **Luettu**; niitä ei päivitetä offline-tilassa.

## Jos jokin menee vikaan
- **Viesti ”The filter is not valid”:** tarkista, että hinaajan ID on UUID ja ettei päivämääräväli ylitä 400 päivää.
- **Viesti ”Historiaa ei voitu ladata.”:** tarkista yhteytesi ja paina **Yritä uudelleen**.
- **”Sinulla ei ole pääsyä tähän hinaajien piiriin.”:** tarkista roolisi ja hinaajakontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Kirjausta ei voi varmentaa:** tarkista näyttöviite ja sen lähde suorituksen aikajanalta; ota yhteyttä varmentajaan (13.03 §10).

## Liittyvät sivut
