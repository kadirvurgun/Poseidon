---
key: requirements
title: Vaatimusluonnokset
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Vaatimusluonnokset-sivu säilyttää lähderevisiosta johdetut **vaatimukset** versioituina luonnoksina ja sitoo jokaisen vaatimuksen tarkkaan lähderevisioon. Se vastaa kysymykseen: ”Minkä lähteen mihin revisioon tämä vaatimus perustuu, ja mikä on sen kirjattu elinkaari?” Vaatimus on ketjun Vaatimus → Kontrolli → Näyttö → Todentaminen ensimmäinen lenkki. Mitään ketjun tietuetta ei pidä jättää linkittämättä (irralliseksi) (13.03 §8).

## Kuka sitä käyttää
- **Regulatory / Assurance -käyttäjä:** sitoo vaatimuksen lähteeseensä, arvioi soveltuvuuden kontekstisyötteillä ja pitää ketjun täydellisenä (13.03 §8).
- **DPA / HSEQ:** lukee vaatimuksen perustan havainto- ja auditointityössä (13.03 §4).

## Ennen kuin aloitat
- Tarkastelu vaatii koko käyttöalueen lukuoikeuden ja sääntelyluonnosten lukuoikeuden; muokkaus vaatii lisäksi sääntelyluonnosten kirjoitusoikeuden.
- Alinäkymät vaativat omat oikeutensa, aina koko käyttöalueen lukulaajuudella. **Elinkaari ja soveltuvuus** vaatii soveltuvuuden lukuoikeuden; sääntöjen muokkaus vaatii sääntöjen kirjoitusoikeuden, tarkastuksen pyytäminen sääntöjen tarkastusoikeuden ja arviointi soveltuvuuden arviointioikeuden; pätevöinti ja tosiseikkojen vahvistus vaativat sääntelyn pätevöinti- tai tosiseikkojen vahvistusoikeuden sekä palvelimelle kirjatun nimityksen. **Kontrollit ja näytöt** vaatii kontrollien, näyttöjen, tiedostojen ja soveltuvuuden lukuoikeuden; linkittäminen vaatii kontrollien tai näyttöjen kirjoitusoikeuden ja aluksen muokkausoikeuden, ja hyväksyntä vaatii kontrollien hyväksymisoikeuden.
- Kirjaa lähde ensin **Lähdeluonnokset**-sivulle. Vaatimuksen voi sitoa vain olemassa olevaan lähderevisioon.
- Tallennus vaatii toimivan palvelinyhteyden; offline-tilassa muutokset säilyvät välilehdellä.

## Vaihe vaiheelta
1. Näet vaatimusluonnokset luettelossa; hae kentällä **Search drafts** ja painikkeella **Hae**.
2. Paina uudelle vaatimukselle **Uusi luonnos**.
3. Valitse kohdassa **Draft fields** jokaiselle kentälle **Tila** (**Puuttuu**, **Tuntematon**, **Known (declared)**) ja syötä tunnetut arvot: **Säädös**, **Versio- / muutosilmoitus**, **Ilmoitettu voimaantulopäivä**, **Ilmoitus / alkuperä**, **Lähteen havaintoaika (UTC-siirtymällä)**.
4. Kirjoita kohdassa **Immutable source citation** kenttään **Find a source** ja paina **Search sources**. Valitse lähde kohdasta **Source draft** ja sitten tarkka revisio kohdasta **Exact revision**. Tarkista valitun revision sisältö kohdasta **Selected immutable source content**.
5. Paina **Tallenna luonnos**. Kun tietue on auki, osiot **Kirjattu elinkaari** ja **Muuttumaton historia** tulevat näkyviin.
6. Käytä näkymää **Elinkaari ja soveltuvuus** soveltuvuudelle ja näkymää **Kontrollit ja näytöt** kontrolli- ja näyttölinkeille. **Kontrollit ja näytöt** -näkymän **Kriittisten irrallisten tietueiden raportti** luettelee kriittiset ilmoitukset, joilla ei ole vaatimuslinkkiä; linkitä nämä tietueet vaatimukseen.

## Mitä tilat tarkoittavat
- **Kirjattu elinkaari:** tila, jonka palvelin johtaa sidotuista säännöistä: **Luonnos**, **Otettu käyttöön**, **Tuleva voimaantulo**, **Voimassa**, **Korvattu**. Jos sitä ei voida lukea, näytetään **Ei käytettävissä**; järjestelmä ei oleta luonnosta eikä voimassaoloa.
- ”Sidotut säännöt kirjaavat eri elinkaaritiloja. Näytetty arvo on ohjelmiston etusijajärjestys; asiantuntijan tarkistus tarvitaan.” Näytetty arvo ei tällöin ole päätös; asiantuntijan tarkastus tarvitaan.
- **Tuntematon:** tietoa ei tiedetä. Kun kriittinen tieto on tuntematon, soveltuvuuden tulos pysyy tilassa **Tarkastus vaaditaan** (REVIEW_REQUIRED); sitä ei koskaan lasketa tilaksi ”ei sovellu” tai ”ei” (13.03 §8).
- **Sovellettavuuden tarkastus tarvitaan / Vaatimuslinkki puuttuu / Todentamista ei tehty:** avoimia kohtia kontrolli–näyttö-ketjussa.
- **Valmistuminen tuntematon · Todentamaton · Vaikuttavuus tuntematon:** valmistuminen, todentaminen ja vaikuttavuus ovat erillisiä tiloja. Valmistunut tietue ei ole todennettu, eikä todennettu tietue ole vaikuttava.

## Mitä et voi tehdä / varoitus
- Et voi sitoa vaatimusta ”uusimpaan” lähteeseen; **Exact revision** valitaan aina. Kun lähde saa uuden revision, vanha sidos säilyy historiassa.
- Soveltuvuuden esikatselu ei osoita vaatimustenmukaisuutta, valmiutta, lupaa eikä oikeudellista hyväksyntää: ”PREVIEW / DRAFT. Predictions do not establish compliance, readiness, permits or legal approval.”
- Elinkaaren muutos kulkee painikkeen **Pyydä henkilön tarkastus** kautta; päätöksen tekevät lähteen hyväksyntä ja pätevä riippumaton tarkastaja. Järjestelmä ei koskaan sulje sitä itse.
- Älä oleta, että luokituslaitoksen, lippuvaltion, yhtiön ja paikalliset sääntölähteet korvaavat toisiaan (13.03 §8).

## Jos jokin menee vikaan
- **Source selection unavailable.** Paina **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Avaa nykyinen revisio painikkeella **Lataa tallennettu luonnos uudelleen**.
- **Check field states, bounds, dates and source revision.** Jos valitsit lähteen, valitse myös sen tarkka revisio.
- **History unavailable. Editor retained.** Paina **Retry history**.
- Jos elinkaari on odottamaton, tarkista sidotut säännöt ja lähderevisiot; ota tarvittaessa yhteyttä kontrollin omistajaan tai aihealueen asiantuntijaan (SME) (13.03 §10).
- Jos oikeus puuttuu, tarkista roolisi ja käyttöalueesi ja pyydä apua Tenant Adminilta (13.03 §10).

## Liittyvät sivut
