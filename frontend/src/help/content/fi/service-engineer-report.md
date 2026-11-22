---
key: service-engineer-report
title: Huolto- ja insinööriraportit
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tällä sivulla kirjataan ja luetellaan valitun aluksen **huolto- ja insinöörikäyntien raportit**. Se vastaa kysymykseen: ”Kuka teki mitä työtä millä laitteella ja mihin työmääräykseen, mitkä osat ja mittaukset kirjattiin, ja onko raportti varmennettu?” Raportti linkitetään laitetunnukseen, työmääräykseen ja vikoihin, joten se on sidottu työmääräyksen ja laitteen historiaan (13.03 §5). Varmennettu raportti on ihmisen kirjaus; se ei sulje vikaa eikä työmääräystä.

## Kuka sitä käyttää
- **Konepäällikkö / tekninen isännöitsijä:** linkittää raportit laite- ja työmääräyshistoriaan, seuraa puuttuvia tietoja ja varmentaa raportit, joita ei itse laatinut (13.03 §5).
- **Konemestari:** syöttää raportin luonnoksena huollon jälkeen ja lähettää sen varmennettavaksi (13.03 §6).

## Ennen kuin aloitat
- Jos alusta ei ole valittu, sivu näyttää tekstin **Valitse alus.** ja luettelee käyttöalueesi alukset. Valitse oikea alus.
- Raporttien näkeminen vaatii huoltoraporttien ja laitteiden lukuoikeuden. **Uusi raportti** -painike näkyy vain kirjoitusoikeudella ja online-tilassa.
- Pidä laitteen, työmääräyksen, vian ja näyttötiedoston tunnukset valmiina. Työmääräys-, vika- ja näyttökentät näkyvät vain, jos voit lukea kyseiset tietueet.

## Vaihe vaiheelta
1. Avaa raportti **Raportit**-luettelosta tai aloita uusi luonnos painikkeella **Uusi raportti**.
2. Täytä **Raportin nro**, **Palveluntarjoaja**, **Insinööri**, **Huoltopäivä**, **Syy ja laajuus** ja **Tehty työ**.
3. Syötä tarvittaessa käytetyt osat ja mittaukset painikkeilla **Lisää osa** ja **Lisää mittaus**. Jos et tiedä mittaustulosta, jätä se tilaan **Tuntematon**.
4. Syötä kohdassa **Linkit** **Laitteen tunnus**, **Työmääräyksen tunnus** sekä linkitettyjen vikojen ja näyttötiedostojen tunnukset (yksi riviä kohden).
5. Tallenna painikkeella **Tallenna luonnos**. Jos raportissa näkyy **Puuttuu ennen lähettämistä** -luettelo, täydennä ensin nämä kohdat.
6. Lähetä raportti painikkeella **Lähetä varmennettavaksi**.
7. Kirjoita lähetettyyn raporttiin, jota et itse laatinut, vähintään 10 merkin **Peruste** ja käytä painiketta **Kirjaa varmennus** tai **Palauta laatijalle**.
8. Muuttaaksesi varmennettua raporttia kirjoita peruste ja käytä painiketta **Korjaa (uusi versio)**. Jokainen versio säilyy kohdassa **Muuttumaton historia**.

## Mitä tilat tarkoittavat
- **Luonnos / Lähetetty / Palautettu:** raportin elinkaari. Lähettäminen ei ole varmennus.
- **Varmennettu (kirjattu ihmisen päätös) / Muu kuin laatija varmensi:** valtuutettu henkilö, joka ei laatinut eikä lähettänyt raporttia, kirjasi varmennuksen. Suoritettu ja varmennettu eivät ole sama asia.
- **Odottaa varmennusta:** raportti on lähetetty eikä sitä ole vielä varmennettu.
- **Aiempi versio varmennettiin, tämä ei:** korjauksen jälkeen uusi versio on varmennettava uudelleen.
- **nykyinen versio / muuttunut linkityksen jälkeen / ei saatavilla:** linkitetyn laitteen, työmääräyksen tai vian tila verrattuna linkitettyyn versioon.
- **Hyväksytty siirto, ei varmennettua näyttöä:** liitetty tiedosto on vain hyväksytty siirto.
- **Tuntematon**-mittaustulos ei tarkoita ”Rajoissa”.

## Mitä et voi tehdä / varoitus
- Et voi varmentaa raporttia, jonka itse laadit tai lähetit; palvelin hylkää sen.
- Varmennettu raportti ei sulje vikaa eikä työmääräystä, eikä se osoita laitteen olevan turvallinen.
- Näytön alkuperäiskappaleita ei muuteta; korjaus avaa uuden version ja vanha versio säilyy historiassa (13.03 §1).
- Offline-tilassa mitään ei kirjata. Kun yhteys palaa, **Yritä samaa komentoa uudelleen** lähettää saman komennon uudelleen.

## Jos jokin menee vikaan
- **Vaaditut tiedot tai tietueet puuttuvat:** täydennä puuttuvien luettelo ja tallenna uudelleen.
- **Joku muu muutti tätä tietuetta:** avaa uusin versio painikkeella **Lataa uudelleen** ja tallenna sitten uudelleen.
- **Sinulla ei ole palvelimen oikeutta tähän toimintoon tai tietueeseen:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Raporttia ei voi varmentaa:** tarkista linkitetyt tietueet ja näyttötiedostot; keskustele valtuutetun varmentajan kanssa (13.03 §10).
- **Tietueen lataus epäonnistui:** lähetä tukeen näkymä, aika ja vaiheet (13.03 §10).

## Liittyvät sivut
