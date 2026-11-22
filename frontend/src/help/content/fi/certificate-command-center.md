---
key: certificate-command-center
title: Todistuskeskus
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Todistuskeskus listaa valitun aluksen todistus**ilmoitukset** niiden ilmoitettuine kalenteritiloineen. Se vastaa kysymykseen: ”Mitkä tämän aluksen todistukset ovat lähellä vanhenemista tai katsastusikkunaa, ja mitkä tietueet odottavat vielä ihmisen tarkastusta?” Näytetyt päivämäärät ovat ilmoituksia. Sivu ei koskaan päätä, että todistus on pätevä tai todennettu (13.03 §1, §8).

## Kuka sitä käyttää
- **Päällikkö, konepäällikkö / tekninen tarkastaja:** seuraavat aluksen todistuskalenteria, syöttävät uusia todistusluonnoksia ja aloittavat uusimisen valmistelun.
- **Regulatory / Assurance -käyttäjä:** tarkistaa, että ilmoitukset perustuvat viralliseen, versioituun lähteeseen, ja kirjaa nimitetyn auditoijan (Auditor) väitteen (13.03 §8).
- Pelkän lukuoikeuden käyttäjät näkevät vain luettelon eivätkä voi muuttaa sitä.

## Ennen kuin aloitat
- Katselu vaatii todistusten lukuoikeuden. Luonnosten syöttäminen vaatii todistusten kirjoitusoikeuden ja muokkausalueen kyseiselle alukselle; asiakirjaehdotusten poimiminen vaatii tiedostojen lukuoikeuden ja poimintaoikeuden; ihmisen väitteen kirjaaminen vaatii hakemiston kirjoitusoikeuden, todentamiskirjauksen oikeuden ja voimassa olevan **Auditor**-nimityksen tälle alukselle ja todistukselle. Suojatut miehistöilmoitukset vaativat lisäksi miehistön, miehistön todistusten, miehistön henkilöllisyyden ja tiedostojen lukuoikeuden.
- Valitse ensin oikea alus: paina sitä **Valitse alus** -luettelossa (13.03 §1).
- Sivu tarvitsee reaaliaikaisen verkkoluvun. Offline-tilassa näet ilmoituksen ”Current certificate sources unavailable offline or hidden.” eikä offline-oikeutta päätellä.

## Vaihe vaiheelta
1. Avaa alus **Valitse alus** -luettelosta.
2. Valitse **Todistuksen käyttöoikeusalue** -valinnalla **Aluksen todistukset** tai, jos sinulla on oikeus, **Suojatut miehistöilmoitukset**. Rajaa luetteloa **Ilmoitetun kalenterin suodatin** -valinnalla.
3. Avaa tietue painamalla sen riviä. Riveillä on merkintä **Todentamaton todistusilmoitus**.
4. Uutta tietuetta varten paina **Uusi todistusluonnos**. Täytä lähdeasiakirjasta **Asiakirjan nimi**, **Ilmoitettu sääntelyperusta**, **Ilmoitettu lippu**, katsastusikkuna ja **Ilmoitettu saatavuus**, ja paina sitten **Tallenna todentamaton luonnos**.
5. Saat ehdotuksia linkitetystä asiakirjasta painamalla **Poimi linkitetystä asiakirjasta** kohdassa **Tarkasta asiakirjan tiedot**. Vertaa jokaista ehdotusta lähteeseen, valitse **Tarkistin tämän tiedon lähteestä** ja **Tarkistin, että tämä asiakirja kuuluu tälle alukselle ja todistukselle**, ja paina sitten **Vahvista valitut tiedot ja tallenna luonnos**.
6. Nimitettynä auditoijana täytä kohdassa **Henkilöväitteen tietue** kentät **Valitse todellinen sijoitus** ja **Valitse kirjattu väite**, kirjoita perustelu ja paina **Kirjaa arvioimaton väite**.
7. Aloita uusiminen tietueen kohdan **Todistukseen liittyvä työ** painikkeella **Valmistele uusiminen**. Tämä vie sinut [Todistuksen uusiminen](/app/renewal-workflow) -kulkuun.

## Mitä tilat tarkoittavat
- **Ilmoitettu vanheneminen <90 päivää / <60 päivää / <30 päivää / Ilmoitettu vanheneminen ohitettu:** kalenterihavainto ilmoitetusta vanhenemispäivästä. Se ei ole päätös pätevyydestä.
- **Ilmoitettu katsastusikkuna avoinna / Määräaikaikkunan yli:** sijainti suhteessa ilmoitettuun katsastusikkunaan.
- **Kalenteritarkastus vaaditaan:** päivämäärä puuttuu, on monitulkintainen tai ristiriitainen. Tietue ei ole valmis ennen kuin henkilö tarkistaa sen lähdeasiakirjasta.
- **Tuntematon:** arvoa ei ole ilmoitettu. Tuntematon arvo ei koskaan tarkoita ”ei puutu” tai ”vaatimustenmukainen”; se vaatii tarkastuksen.
- **Pieni luottamusarvo · tarkasta / Monitulkintainen · tarkista / Tarkastus vaaditaan:** asiakirjaehdotus, jota henkilö ei ole vielä vahvistanut. Luottamusarvo ei ole todentaminen.
- ”Vain tämä sivu. Kalenterihavainnot ovat erillään pätevyydestä; nämä eivät ole koko laivaston valmiussummia.”

## Mitä et voi tehdä / varoitus
- Luonnoksen tallentaminen ei todenna todistusta. Tietueen kirjoittaminen, asiakirjaehdotusten vahvistaminen ja auditoijan väite ovat erillisiä vaiheita; Valmis ≠ Todennettu.
- Automaattinen poiminta vain ehdottaa. Puuttuvat arvot pysyvät tuntemattomina, ja vain valitsemasi ja korjaamasi tiedot päivittävät luonnosta.
- Pelkkä ylläpitäjän rooli ei oikeuta kirjaamaan ihmisen väitettä; tarvitaan voimassa oleva nimitys ja lähdetiedosto.
- Tallennettua todistusta ei koskaan muuteta huomaamatta. Jokainen tallennus luo uuden version, ja vanhemmat versiot säilyvät kohdassa **Kirjattu versiohistoria** (13.03 §1).
- Sivu ei myönnä luokituslaitoksen, lippuvaltion tai viranomaisen hyväksyntää eikä päätä matkakelpoisuudesta. Tämä päätös kuuluu valtuutetuille henkilöille.

## Jos jokin menee vikaan
- **Todistuslähde tai käyttöoikeus ei ole käytettävissä. Pätevää, tyhjää tai todennettua inventaaria ei päätellä.** Paina **Toista lähteen luku**. Jos tilanne jatkuu, tarkista roolisi, aluksesi ja kohdealueesi; ota tarvittaessa yhteyttä Tenant Admin -käyttäjään (13.03 §10).
- **Lopputulos tuntematon.** Säilytä alkuperäinen komento ja lähetä sama pyyntö uudelleen painikkeella **Toista täsmällinen todistuskomento**.
- **Nykyinen lähde muuttui.** Paikalliset kenttäsi säilyvät; käytä painiketta **Takaisin tietueisiin**, lue nykyinen tietue uudelleen ja tee muutoksesi uudelleen.
- **Automaattinen luku ei ole käytettävissä tälle asiakirjalle:** syötä arvot käsin lähdeasiakirjasta.
- Jos tietuetta ei voida todentaa, tarkista sen todisteet ja lähdeketju ja ohjaa se todentajalle (13.03 §10).

## Liittyvät sivut
