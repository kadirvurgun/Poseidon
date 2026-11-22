---
key: voice-event-draft
title: Puheesta luotu tapahtumaluonnos
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Puheesta luotu tapahtumaluonnos -sivu muuttaa lyhyen puhutun tai kirjoitetun muistiinpanon turvallisuustapahtumailmoituksen **luonnosehdotuksiksi**. Se vastaa kysymykseen: ”Poimittiinko tapahtuman tyyppi, sijainti, aika, tapahtumien kulku ja välitön toimenpide oikein siitä, mitä sanoin tai kirjoitin?” Ehdotukset tulevat kiinteistä paikallisista säännöistä, ja kukin niistä jäljitetään omiin sanoihisi luottamustason kera. Ihminen muokkaa ja vahvistaa luonnoksen; mitään ei lähetetä, suljeta eikä ilmoiteta automaattisesti (13.03 §1, §6).

## Kuka sitä käyttää
- **Crew / OOW / Engineer ja Päällikkö:** tekevät nopean muistiinpanon kentällä ja aloittavat turvallisuustapahtumailmoituksen (13.03 §2, §6).
- **DPA / HSEQ:** seuraa kohtaa **Mitattu käytettävyys (tämä alus)**. Nämä luvut ovat operatiivinen havainto, eivät tarkkuusväite.

## Ennen kuin aloitat
- Valitse käyttöalueeseesi kuuluva alus. Luonnoksen luominen vaatii puheluonnosten kirjoitusoikeuden ja muokkausoikeuden kyseiseen alukseen.
- Vahvistetun luonnoksen siirtäminen turvallisuustapahtuman luonnokseksi vaatii lisäksi turvallisuustapahtumien ilmoitusoikeuden.
- Yksityisyys: ääntä ei tallenneta. Puhe käyttää vain tämän asennuksen paikallista moottoria; jos sellaista ei ole asennettu, kirjoita tai liitä muistiinpano. Vältä nimiä ja henkilökohtaisia terveystietoja.
- Offline-tilassa mitään ei lähetetä.

## Vaihe vaiheelta
1. Paina **Uusi ääni- tai tekstimuistiinpano**.
2. Jos paikallinen moottori on määritetty, paina **Nauhoita paikallisella moottorilla** ja sitten **Lopeta ja litteroi**. Muuten kirjoita lyhyt muistiinpano kenttään **Muistiinpanon teksti**: mitä, missä, milloin, mitä teit.
3. Tarkista **Muistiinpanon päivä**. Sitä käytetään vain sanojen kuten ”tänään” tai ”eilen” tulkitsemiseen.
4. Paina **Luo luonnosehdotukset**.
5. Tarkista jokainen kenttä: **Tapahtuman tyyppi**, **Milloin tapahtui (paikallista aikaa)**, **Sijainti aluksella**, **Paikan tarkennus**, **Mitä tapahtui**, **Välitön toimenpide**. Kunkin kentän alla näet ehdotuksen luottamustason, säännön ja muistiinpanosi vastaavan osan. Lue myös **Sääntöjen huomiot**.
6. Korjaa väärät ehdotukset ja paina **Tallenna muutokset**. Kun **Vielä vaaditaan ennen vahvistusta:** on tyhjä, paina **Vahvista luonnos**. Vahvistaminen kirjaa, että tarkistit jokaisen kentän; se ei lähetä eikä sulje tapahtumaa.
7. Jos vahvistuksen jälkeen luotiin turvallisuustapahtuman luonnos, siirry linkillä **Avaa turvallisuustapahtuman luonnos** sivulle Turvallisuustapahtumat ja läheltä piti -tilanteet ja täydennä ja lähetä se siellä. Jos luonnosta ei ole vielä luotu ja sinulla on oikeus, käytä painiketta **Luo turvallisuustapahtuman luonnos nyt**.
8. Jos muistiinpano ei ole tapahtuma tai se on virheellinen, valitse **Peruste**, lisää tarvittaessa **Huomautus (pakollinen kohdassa Muu)** ja paina **Hylkää luonnos**.

## Mitä tilat tarkoittavat
- **Luonnos (vahvistamaton):** kukaan ei ole vielä vahvistanut ehdotuksia.
- **Henkilön vahvistama:** kentät on tarkistettu; tämä ei ole turvallisuustapahtumailmoitus, eikä mitään ole lähetetty.
- **Hylätty:** hylkäyksen peruste kirjataan arviointia varten.
- **Ehdotettu, muuttamaton / Muuttamasi / Syöttämäsi / Tyhjä:** kentän alkuperä. Ehdotettu, muuttamaton on ehdotus, joka ihmisen on vielä tarkistettava.
- **Kirjoitettu tai liitetty / Paikallinen puhemoottori:** miten muistiinpano syötettiin. Paikallisen moottorin tekstin muokkaaminen muuttaa sen kirjoitetuksi muistiinpanoksi.
- Säännön huomautukset, kuten **Aika ilman päivää; päivää ei oletettu**, osoittavat puuttuvaa tietoa; järjestelmä ei arvaa puuttuvaa arvoa.
- Merkintä kuten **2 missing**: kuinka monta luonnoksen pakollista kenttää on luettelossa vielä tyhjänä.

## Mitä et voi tehdä / varoitus
- Ehdotukset ovat vain luonnos; luottamustaso ei takaa oikeellisuutta. Tarkista jokainen kenttä itse ennen vahvistamista.
- Tämä sivu ei lähetä, sulje eikä ilmoita turvallisuustapahtumaa. Ilmoitus lähetetään sivulla Turvallisuustapahtumat ja läheltä piti -tilanteet.
- Jos näyttöön viitataan, liitä se itse; sivu ei liitä näyttöä.
- **Vahvista luonnos** ei ole käytettävissä, kun muutoksia on tallentamatta; tallenna ne ensin.
- Ilman turvallisuustapahtumien ilmoitusoikeutta vahvistetusta luonnoksesta ei luoda turvallisuustapahtuman luonnosta.

## Jos jokin menee vikaan
- **Speech capability unavailable: no local engine is configured.** tai **Transcription was not completed.** Kirjoita tai liitä muistiinpano.
- **Mikrofonin käyttöä ei sallittu.** Tarkista selaimen lupa tai kirjoita muistiinpano.
- **The record changed meanwhile.** Paina **Lataa uudelleen** ja tee muokkauksesi uudelleen.
- **You are offline. Nothing was sent.** Kun yhteys on päällä, käytä painiketta **Yritä samaa komentoa uudelleen**.
- **Sinulla ei ole oikeutta tähän toimintoon.** Tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
