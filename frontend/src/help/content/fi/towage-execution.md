---
key: towage-execution
title: Hinauksen suoritus
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Suorituskonsoli kirjaa lähetetyn hinaajatyön **todellisen suorituksen** aikajanaksi: aloituksen, havainnot, toimenpiteet **Keskeytä / Jatka / Hätäkeskeytä / Päätä** sekä myöhemmän varmennuksen. Se vastaa kysymykseen: ”Milloin operaatio alkoi, milloin hinausköysi kiinnitettiin ja irrotettiin, mitkä havainnot kirjattiin, missä tilassa operaatio on ja onko kirjaus varmennettu?” Aloitus ja siirtymät ovat suorittavan hinaajan hinaajan päällikön tai päällikön ihmisen tekemiä kirjauksia; konsoli ei koskaan anna GO-päätöstä.

## Kuka sitä käyttää
- **Hinaajan päällikkö / Päällikkö:** aloittaa operaation ja kirjaa aikajanan Aloita/Keskeytä/Jatka/Hätäkeskeytä/Päätä sekä sen näytön (13.03 §3).
- **Varmentaja (henkilö, jolla on suorituksen varmennusvaltuus):** varmentaa päätetyn tai hätäkeskeytetyn kirjauksen tai hylkää sen varmennuksen (13.03 §1).
- **Hinaajaoperaatioiden koordinaattori / Maaoperaatiot:** seuraavat näytön täydellisyyttä ja päätettyjen töiden historiaa; he eivät voi aloittaa eivätkä hätäkeskeyttää operaatiota (13.03 §7).

## Ennen kuin aloitat
- Avaa hinaaja kohdasta **Hinaajatoiminnot** ja valitse työ kohdasta **Valitse lähetetty hinaustyö**.
- Aloittaminen edellyttää, että kaikki seuraavat ovat ajan tasalla: hinaajan aktiivinen määräys, hyväksytty **Hinaussuunnitelma** (hinaajan on oltava suunnitelmassa), julkaistu **Hinausriski ja tiedotustilaisuus**, jonka jokainen osallistuja on kuitannut, sekä **Operaation valmiustarkastus** -kohdan **GO**-kirjaus, joka on sidottu nykyiseen arviointiin.
- Aloitus ja siirtymät edellyttävät kyseiselle hinaajalle palvelimelle kirjattua **Nimitys**-tietoa (hinaajan päällikkö tai päällikkö).

## Vaihe vaiheelta
1. Lue osiossa **Aloitusehto** kunkin hinaajan **Valmiuspäätös** ja **Esteet**.
2. Jos esteitä ei ole, lue vahvistusteksti aluksella olevana nimitettynä henkilönä, valitse **Nimitys**, syötä halutessasi **Aloitussijainti** ja paina **Aloita operaatio**.
3. Kun suoritus on auki, seuraa kohtia **Tila**, **Varmennus** ja kirjanpidon kenttiä (hinausköysi kiinnitetty/irrotettu, suurin havaittu hinauskuorma, viestintä, vinssi, hätäirrotuksen testi, aktiiviset minuutit).
4. Valitse kohdassa **Kirjaa havainto** havainnon tyyppi (esimerkiksi hinausköysi kiinnitetty, hinauskuorma, viestinnän tarkastus, sää, vinssin tila, hätäirrotuksen testi, sijainti, näyttö, huomautus), syötä arvot ja paina **Kirjaa havainto**.
5. Valitse kohdassa **Hinaajan päällikön toimenpiteet** **Toiminto** (Keskeytä, Jatka, Hätäkeskeytä, Päätä) ja syötä **Nimitys** sekä **Peruste**. Hätäkeskeytys ja päättäminen vaativat vähintään 20 merkin perusteen ja avaavat kentän **Lopetussijainti**; päättämistä ei hyväksytä ilman lopetussijaintia.
6. Jos **Päättämisen esteet** on lueteltu, poista ne; esimerkiksi jos hinausköysi on yhä kiinni, kirjaa ensin sen irrotus.
7. Kirjoita varmentajana peruste ja paina **Varmenna kirjaus** tai **Hylkää varmennus**.
8. Näet jokaisen tapahtuman järjestyksessä kohdassa **Aikajana**.

## Mitä tilat tarkoittavat
- **Aktiivinen / Keskeytetty / Päätetty / Hätäkeskeytetty:** operaation tila. Päätetty ei tarkoita, että kirjaus olisi varmennettu.
- **Odottaa / Varmennettu / Hylätty:** varmennuksen tila. Completed ≠ Verified; varmennus on toisen henkilön kirjaus.
- **Hyvä / Menetetty / Hyväksytty / Ei läpäissyt / Ehdollinen / Tuntematon:** viestinnän, vinssin ja hätäirrotuksen havaintojen tulos. Tuntematon osoittaa, ettei mitään kirjattu; se ei koskaan korvaa arvoa ”Hyvä” tai ”Hyväksytty”.
- **Turvallisuustapahtumat — Tuntematon:** tämä konsoli ei laske turvallisuustapahtumia; johtopäätöstä ”ei tapahtumia” ei tehdä.
- **GO (ihmisen kirjaus) / NO GO (ihmisen kirjaus):** hinaajan päällikön tai päällikön valmiustarkastukseen kirjaama päätös.

## Mitä et voi tehdä / varoitus
- Maan käyttäjät, tekoäly ja järjestelmäidentiteetit eivät voi aloittaa eivätkä hätäkeskeyttää operaatiota. Maalta tehty määräys ei koskaan ohita hinaajan päällikön aluksella tekemää turvallisuuspäätöstä (13.03 §7).
- Vain nimitetty hinaajan päällikkö tai päällikkö voi keskeyttää, jatkaa, hätäkeskeyttää tai päättää. Hätäkeskeytys on aina käytettävissä operaation ollessa käynnissä.
- Operaation aloittanut tai päättänyt henkilö ei voi varmentaa samaa kirjausta; varmennus tehdään operaation päätyttyä.
- Kirjanpitoon voi vain lisätä; kirjattuja tapahtumia ei koskaan poisteta eikä muuteta.
- Offline-tilassa komentoja ei lähetetä. Havaintoja voi asettaa jonoon sivulla **Hinaajan offline-operaatiot**; aloitus, siirtymät ja varmennus vaativat yhteyden.

## Jos jokin menee vikaan
- **Viesti ”Palvelin ilmoitti puuttuvia tai estäviä kohteita.”:** poista koodit kohdasta **Esteet** tai **Päättämisen esteet** (esimerkiksi suunnitelmaa ei ole hyväksytty, tiedotustilaisuuden kuittaus odottaa, GO-kirjaus puuttuu tai ei ole ajan tasalla).
- **Viesti ”The record changed or the command conflicts.”:** tarkastele kirjausta painikkeella **Lataa uusin**.
- **Viesti ”The outcome is unknown.”:** **Yritä uudelleen** lähettää täsmälleen saman komennon uudelleen.
- **Ei valtuutusta:** tarkista roolisi, hinaajasi ja nimityskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
