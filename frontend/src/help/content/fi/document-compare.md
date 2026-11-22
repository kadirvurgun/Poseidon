---
key: document-compare
title: Asiakirjarevisioiden vertailu
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tämä sivu näyttää valitulle alukselle **ohjatun asiakirjan kaksi revisiota rinnakkain, vain luku -tilassa**. Se vastaa kysymykseen: ”Mitä eroja näiden kahden revision välillä on tiedosto- ja metatietotasolla, ja mikä on kunkin julkaisu-, todentamis- ja säilytyshistoria?” Sivu näyttää erot; se ei hyväksy, julkaise eikä arvioi muutoksia. Se ei saata revisiota voimaan eikä tee siitä aiemman korvaajaa. Todisteiden alkuperäiskappaleita ei koskaan muuteta huomaamatta; kun uusi revisio korvaa aiemman, aiemman revision julkaisutilaksi tulee Korvattu ja tapahtuma näkyy sen säilytyshistoriassa (13.03 §1).

## Kuka sitä käyttää
- **Päällikkö / konepäällikkö / miehistö:** vertaa voimassa olevaa revisiota edelliseen ja kirjaa lukukuittauksen (13.03 §2, §5, §6).
- **DPA / Regulatory / Assurance -käyttäjä:** tarkastelee revisiohistoriaa ja todentamiskirjauksia (13.03 §4, §8).

## Ennen kuin aloitat
- Jos alusta ei ole valittu, sivu ilmoittaa **Valitse alus oikeuksiesi piiristä.** ja listaa alukset. Valitse oikea alus.
- Sivun näkeminen vaatii tiedostojen lukuoikeuden. Rajoitettu sisältö ja ISPS-rajoitetut asiakirjat vaativat lisäksi omat oikeutensa.
- Sisällön lataaminen ja kuittauksen kirjaaminen toimivat vain verkossa.

## Vaihe vaiheelta
1. Avaa asiakirja **Ohjatut asiakirjat** -luettelosta. Luokitus- ja **ISPS-rajoitettu**-merkinnät näkyvät rivillä.
2. Valitse kohdassa **Valitse kaksi revisiota** revisiot **Vasen (pohja)** ja **Oikea (tarkasteltava)**. Sivu ehdottaa ensin uusinta revisiota oikealle ja sitä edeltävää vasemmalle.
3. Paina **Vertaa**. Vaihda niiden paikkaa painikkeella **Vaihda puolet**.
4. Lue tavulausunto ja **Metatietojen vertailu** -taulukko: **Tiedostotyyppi**, **Koko (tavua)**, **Sisällön SHA-256**, **Vastaanotto**, **Julkaisu**, **Varmennus** ja aikakentät.
5. Tarkastele kummankin puolen **Hyväksyntä- ja säilytyshistoria** -tietoja.
6. Nähdäksesi sisällön paina **Lataa sisältö (käyttö kirjataan)**. Sisältö näytetään tekijävesileiman kanssa.
7. Kun olet painanut **Load content** ja lukenut voimassa olevan revision, valitse **Olen lukenut tämän revision ja pääsin sen sisältöön.** ja paina **Kirjaa kuittaus**.

## Mitä tilat tarkoittavat
- **Julkaisematon / Voimassa / Korvattu:** revision julkaisutila. Revisio, joka ei ole voimassa, näyttää **Ei operatiivisesti voimassa**.
- **Todentamaton / Varmennettu kelvolliseksi / Varmennettu kelvottomaksi / Varmennus epäselvä:** revision todentamiskirjaus. Todentamaton ei tarkoita ”kelvollinen”.
- **Sama / Muuttunut / Ei verrattavissa:** metatietorivin tulos. Ei verrattavissa ei tarkoita ”Sama”.
- **Byte-identical files:** vain identtiset tavut; tämä ei ole sisällön tarkastus.
- **Files differ at byte level:** se, mikä sääntö muuttui ja mikä sen merkitys on, vaatii henkilön tarkastuksen.
- **Sisältö on rajoitettu roolillesi; vain metatiedot.** sisältöä ei avata tälle roolille.

## Mitä et voi tehdä / varoitus
- Et voi julkaista, hyväksyä, saattaa voimaan tai korvata asiakirjaa tältä sivulta.
- Sivu ei tulkitse sisältöeroja automaattisesti; valtuutettu henkilö päättää niiden merkityksestä.
- Kuittaus kirjataan vain voimassa olevalle revisiolle, kerran henkilöä ja revisiota kohden. Se ei ole hyväksyntä eikä todentaminen.
- Tulostettu kopio on ohjaamaton. Sisältöä ei tallenneta välimuistiin eikä säilytetä offline-tilassa.
- Offline-tilassa vertailu on vanhentunut eikä uutta sisältöä voi ladata.

## Jos jokin menee vikaan
- **Tämä asiakirja ei ole käytettävissäsi:** tarkista aluskontekstisi ja oikeutesi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Sinulla ei ole oikeutta tähän toimintoon:** tarkista roolisi ja aluskontekstisi (13.03 §10).
- **Tietue muuttui sillä välin:** avaa uusin versio painikkeella **Lataa uudelleen**.
- **Revisiota ei voida todentaa:** tarkista todisteet ja alkuperähistoria; keskustele valtuutetun todentajan kanssa (13.03 §10).
- **Pyyntö epäonnistui:** lähetä näkymä, aika ja vaiheesi tukeen (13.03 §10).

## Liittyvät sivut
