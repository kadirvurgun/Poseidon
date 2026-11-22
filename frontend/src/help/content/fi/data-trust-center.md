---
key: data-trust-center
title: Tietojen luotettavuuskeskus
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tietojen luotettavuuskeskus näyttää valitun aluksen kontrollien, todisteiden ja alusprofiilin kenttien **tietojen laatusignaalit**. Se vastaa kysymykseen: ”Mitkä tämän aluksen tietueet puuttuvat, ovat vanhentuneita, ristiriitaisia, todentamattomia tai orpoja, ja mikä lähde- ja ajantasaisuustieto sen osoittaa?” Luotettavuussignaali ei ole taustalla olevan tietueen todentaminen. Sivu ei koskaan merkitse mitään valmiiksi.

## Kuka sitä käyttää
- **Regulatory / Assurance -käyttäjä:** välttää jättämästä orpoja tai keskeneräisiä tietueita vaatimus → kontrolli → todiste → todentaminen -ketjuun (13.03 §8).
- **DPA:** seuraa todisteiden ja todentamisen puutteita (13.03 §4).
- **Tekninen tarkastaja / päällikkö:** näkee signaalit, jotka pitävät valmiusarvion tilassa Tarkastus vaaditaan (13.03 §2, §5).

## Ennen kuin aloitat
- Tarkista ylhäällä olevasta kontekstipalkista, että **oikea yritys** on valittuna (13.03 §1).
- Sivu tarvitsee kontrollien, todisteiden, tiedostojen, sovellettavuuden ja vuokralaisen sääntelyluonnosten lukuoikeudet kaikki yhdessä sekä koko vuokralaisen kattavan lukualueen.
- Jos alusta ei ole valittu, sivu näyttää ensin luettelon ”Valitse alus”.

## Vaihe vaiheelta
1. Valitse alus luettelosta ”Valitse alus”.
2. Lue kohdasta **Yhteenveto** kokonaistila (”Tarkastus vaaditaan” tai ”Ei olennaista signaalia”) ja **Kohteet signaaleittain** -määrät.
3. Tarkista **Kontrollit ja näytöt** -taulukosta kunkin kohteen sarakkeet ”Lähde / alkuperä”, ”Ajantasaisuus”, ”Varmennus” ja ”Signaalit”.
4. Huomioi signaalimerkin vieressä oleva syykoodi ja tarkista tietue sen omalla sivulla.
5. Katso profiilikenttien signaalit taulukosta **Aluksen profiilikentät**.
6. Käytä painikkeita **Edellinen** / **Seuraava** muihin kohteisiin.
7. Jos näkyviin tulee ”Nämä signaalit voivat olla vanhentuneita.”, paina **Lataa uudelleen**.

## Mitä tilat tarkoittavat
- **Puuttuu / Vanhentunut / Ristiriitainen / Todentamaton / Orpo:** kohteen signaalin laji. Signaali on laatumerkintä; se ei todista tietuetta oikeaksi tai vääräksi.
- **Ei signaalia:** kohteesta ei löytynyt signaalia. Tämä ei tarkoita, että tietue on todennettu.
- **Tarkastus vaaditaan:** olennainen signaali on olemassa. Valmius pidetään tilassa Tarkastus vaaditaan niin kauan kuin olennaisia signaaleja on.
- **Ei olennaista signaalia:** tämä ei ole valmiuslausunto.
- **Ihmisen todentama:** valtuutettu henkilö todensi todisteen. Tieto tulee todisteen omasta tietueesta; keskus ei todenna mitään.
- **Ei todennettu / Todennus vanhentunut / Todennus hylätty:** todisteen todentamistila.
- **Ei koske kontrolleja:** näytetään kontrolliriveillä; todentamistila koskee todisteriviä.
- **Havaintoaika tuntematon / Ei ajantasaisuussääntöä / Tulevaisuuteen päivätty:** ajantasaisuutta ei voitu todeta tai päivämäärä on tulevaisuudessa. Mikään näistä ei tarkoita ”ajan tasalla”.

## Mitä et voi tehdä / varoitus
- Tietueita ei korjata, todenneta tai suljeta tältä sivulta; sen tekee valtuutettu henkilö tietueen omalla sivulla.
- Jos vain ensimmäiset kohteet arvioitiin, yhteenveto pysyy tilassa Tarkastus vaaditaan.
- Kohteet, joita et voi lukea, lasketaan mutta niitä ei näytetä, ja yhteenveto pysyy silloin tilassa Tarkastus vaaditaan. Tietue, jota et näe, ei tarkoita, ettei ongelmaa olisi.
- Offline-tilassa luotettavuussignaaleja ei voi päivittää.

## Jos jokin menee vikaan
- **Sinulla ei ole oikeutta lukea luotettavuussignaaleja:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Tietuetta ei voida todentaa:** tarkista todiste ja sen alkuperä; ota asia esiin valtuutetun todentajan kanssa.
- **Odottamaton signaali:** vie syykoodi kontrollin omistajan tai aihealueen asiantuntijan tietoon.
- **Luotettavuussignaalit eivät ole käytettävissä:** käytä painiketta ”Yritä uudelleen”; jos tilanne jatkuu, ilmoita siitä tukeen kuvakaappauksen, ajan ja vaiheidesi kanssa.

## Liittyvät sivut
