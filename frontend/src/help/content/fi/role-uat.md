---
key: role-uat
title: Roolikohtaiset UAT-tiedot
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Roolikohtaiset UAT-tiedot -sivulle kirjataan laiva- ja hinaajaroolien kanssa tehdyt käyttäjien hyväksymistestauksen (UAT) ajot, niiden skenaariotulokset ja havainnot **ihmisten näyttönä**, ja niitä verrataan sovittuun kynnykseen. Se vastaa kysymykseen: ”Täyttääkö jokaisen roolin ja skenaarion vahvistettu UAT-näyttö ihmisten sopiman kynnyksen?” Ylhäällä oleva huomautus on voimassa: ”Kirjaa vain ihmisten UAT-näytön. Järjestelmä ei koskaan hyväksy UAT:tä eikä päätä pilotin GO:sta; automaattitestien tulokset ovat liitteitä, eivät PASS.”

## Kuka sitä käyttää
- **UAT-vetäjä:** avaa ajon, kirjaa osallistujan havaitut skenaariotulokset ja havainnot ja lähettää ajon vahvistettavaksi. Laivan osallistujarooleja ovat päällikkö, DPA, konepäällikkö ja miehistö (13.03 §2, §4, §5, §6).
- **Vahvistaja:** muu henkilö kuin vetäjä vahvistaa ajon.
- **UAT-omistajat / Tuoteomistaja:** kirjaavat sovitun kynnyksen ja käyttävät tulosta **Pilotin go/no-go-katselmus** -sivulla.

## Ennen kuin aloitat
- Tarkastelu vaatii koko käyttöalueen lukuoikeuden ja UAT-lukuoikeuden. Ajojen ja tulosten kirjaaminen, kynnyksen kirjaaminen ja vahvistaminen vaativat kukin oman oikeutensa.
- UAT-omistajien on täytynyt sopia kynnys etukäteen; järjestelmä ei koskaan aseta sitä.
- Valitse osallistujalle pseudonyymi. Älä koskaan käytä nimeä, sähköpostiosoitetta tai miehistönumeroa.
- Kirjaaminen vaatii toimivan yhteyden; se on pois käytöstä offline-tilassa.

## Vaihe vaiheelta
1. Valitse **Alus** tai **Hinaaja** **Toimintatila**-painikkeilla.
2. Tarkista kohdassa **UAT-näyttö sovittuun kynnykseen nähden** tila, määrät **Avoimet estävät**, **Avoimet merkittävät havainnot**, **Vahvistetut ajot** ja **Odottaa vahvistusta** sekä skenaariotaulukko.
3. Jos kynnystä ei ole, täytä kohdassa **Kirjaa sovittu kynnys** **Skenaarion vähimmäisläpäisyaste**, **Avoimien merkittävien havaintojen enimmäismäärä**, **Suunnittelun mukaisuus**, **Sopimuksen viite** ja **Perustelu** ja paina **Kirjaa**.
4. Syötä kohdassa **UAT-ajot** lomakkeella **Uusi UAT-ajo** **Rooli**, **Ympäristö**, **Build-commit** ja **Osallistujan pseudonyymi** ja paina **Kirjaa**.
5. Avaa ajo. Valitse painikkeella **Kirjaa tulos** **Skenaario** ja **Tulos**; lisää tarvittaessa **Apua annettiin** ja **Automaattisen näytön viite**. Ongelmalle käytä painiketta **Kirjaa havainto** kenttien **Vakavuus** ja **Havainnon yhteenveto** kanssa.
6. Kun olet kirjannut skenaariot, paina **Lähetä ajo vahvistettavaksi** (vain vetäjä). Skenaariot ilman tulosta lasketaan tilaan **Ei suoritettu**. Muu henkilö kuin vetäjä kirjoittaa kohdassa **Vahvista ajo** **Vahvistuslausuma**-tekstin ja painaa **Kirjaa**.
7. Sulje kohdassa **Avoimet havainnot** havainto kenttien **Ratkaisu** ja **Ratkaisun huomautus** avulla ja paina **Ratkaise**.

## Mitä tilat tarkoittavat
- **Kynnystä ei asetettu:** ”Ihmisten sopimaa kynnystä ei ole kirjattu. UAT ei voi läpäistä ennen kuin se kirjataan.”
- **Kriteerit eivät täyty / Näyttö puutteellinen:** vahvistettu näyttö ei täytä kynnystä, tai kaikkia rooleja ja skenaarioita ei ole katettu.
- **Kriteerit täyttyvät; ihmisen päätös odottaa:** paras mahdollinen tila. Se ei ole UAT-hyväksyntä eikä pilotin GO; päätös on ihmisen.
- **Ei suoritettu:** skenaariolle ei ole tulosta. Suorittamatonta skenaariota ei koskaan lasketa läpäistyksi.
- **Avoin / Lähetetty / Vahvistettu:** ajon tila. Vain vahvistetut ajot lasketaan kynnykseen.
- **Läpäisty / Epäonnistui / Estetty / Ei suoritettu:** osallistujan havaittu skenaariotulos.
- **Estävä / Merkittävä / Vähäinen:** havainnon vakavuus. Avoin estävä havainto hylkää portin.
- **Kuvitteellinen aineisto:** synteettinen esimerkkitietue; se ei ole todellista UAT-näyttöä.

## Mitä et voi tehdä / varoitus
- Et voi vahvistaa ajoa, jota vedit: ”Vahvistuksen on kirjattava eri henkilö kuin vetäjä.”
- Et voi ratkaista havaintoa, jonka itse ilmoitit. Estävä havainto suljetaan vain tilaan **Ratkaistu (korjattu ja testattu uudelleen)**; **Riski hyväksytty** -vaihtoehtoa ei tarjota.
- Automaattinen (Playwright) tulos on vain liitetty näyttö; tulos on osallistujan havaitsema tulos.
- Järjestelmä ei koskaan hyväksy UAT:tä, ei koskaan aseta kynnystä eikä koskaan keksi vahvistuksia tai hyväksyntöjä.

## Jos jokin menee vikaan
- **Sinulla ei ole pääsyä näihin tietueisiin.** Tarkista roolisi ja käyttöalueesi ja ota yhteyttä Tenant Adminiin (13.03 §10).
- **Tietuetta ei löytynyt vuokraajastasi.** Tarkista ajon linkki ja valittu tila.
- **Tietue muuttui tai sitä on jo käytetty. Lataa uudelleen.** Avaa nykyinen tila painikkeella **Lataa uudelleen**.
- **Tulos on epävarma. Uusi yritys lähettää saman komennon.** Lähetä sama komento turvallisesti uudelleen painikkeella **Yritä uudelleen**.
- **Säännöt hylkäsivät.** Esimerkiksi skenaario ei kuulu ajon rooliin, tai estävää havaintoa ei voi sulkea riskin hyväksynnällä.
- **Ei sallittu.** Esimerkiksi vedit tämän ajon etkä voi vahvistaa sitä, tai ilmoitit tämän havainnon etkä voi ratkaista sitä.
- **Olet offline-tilassa. Kirjaus on pois käytöstä, kunnes yhteys palaa.** Kun yhteys palaa, paina **Päivitä**.

## Liittyvät sivut
