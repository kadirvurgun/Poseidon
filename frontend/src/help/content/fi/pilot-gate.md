---
key: pilot-gate
title: Pilotin go/no-go-katselmus
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Pilotin go/no-go-katselmus -sivu näyttää yhdessä paikassa pilotin edellyttämien porttien tilan, avointen P0-asioiden määrän ja tuen valmiuden, ja siihen kirjataan nimitetyn tuoteomistajan go/no-go-päätös. Se vastaa kysymykseen: ”Onko jokainen pilotin portti täytetty näytöllä, ja onko ihmisen päätös kirjattu?” Ylhäällä oleva huomautus on voimassa: ”Advisory review only. Unknown or deferred gates and an unknown or open P0 recommend NO-GO. Only the appointed Product Owner records a decision.”

## Kuka sitä käyttää
- **Tuoteomistaja:** lukee katselmuksen tuloksen ja kirjaa go/no-go-päätöksen perusteluineen. Päättäminen edellyttää voimassa olevaa tuoteomistajan nimitystä.
- **Tuki / Tenant Admin:** merkitsee tuki- ja päivystysvalmiuden kohdat näyttöviitteen kanssa.
- 13.03 ei määritä tälle sivulle erillistä roolia; vuokralaisen hallinnan periaatteet (vähimmät oikeudet, hallittu julkaisu) ovat voimassa (13.03 §9).

## Ennen kuin aloitat
- Katselu edellyttää koko vuokralaisen lukuoikeutta ja pilotin porttien lukuoikeutta. Tukikohdan merkitseminen edellyttää tuen kirjausoikeutta; päättäminen edellyttää päätösoikeutta sekä palvelimella määritettyä voimassa olevaa tuoteomistajan nimitystä. Pelkkä oikeus ei riitä.
- Vain ihmiskäyttäjän tili voi merkitä tukikohtia tai päättää; nimityksen on vastattava aktiivista rooliasi ja oltava voimassaoloaikansa sisällä.
- Porttien syötteet tulevat muilta sivuilta; esimerkiksi alus-UAT- ja hinaaja-UAT-portit lasketaan **Roolikohtaiset UAT-tiedot** -sivun vahvistetuista ajoista.
- Kirjaaminen edellyttää toimivaa yhteyttä; se on poissa käytöstä offline-tilassa.

## Vaihe vaiheelta
1. Lue kohdasta **Pilotin porttikatselmus** suositus (**Neuvoa-antava suositus**), **Avoimet P0** -määrä ja porttitaulukko: **Portti**, **Tila**, **Lähde**, **Syyt**.
2. Valitse kohdassa **Tuki- ja päivystysvalmius** kullekin kohdalle **Tila**, anna tilalle **Valmis** **Näyttöviite**, kirjoita **Huomautus** ja paina **Merkitse**.
3. Jos olet tuoteomistaja, valitse päätös kohdassa **Kirjaa go/no-go-päätös** (**NO-GO**, **Ehdollinen GO**, **GO**), valitse tuoteomistajan nimityksesi kohdassa **Nimitys** ja kirjoita **Perustelu**. Täytä tilassa **Ehdollinen GO** kohta **Ehdot (yksi riviä kohden)**.
4. Paina **Kirjaa**. Päätös näkyy järjestysnumeroineen kohdassa **Tuoteomistajan päätökset**.
5. Lataa tiedot uudelleen painamalla **Päivitä**.

## Mitä tilat tarkoittavat
- **Evidence threshold met (PASS):** portin lähde täyttää näyttökynnyksen. Se ei ole hyväksyntä; ihminen päättää.
- **Fail (FAIL)**, **Unknown (UNKNOWN)**, **Deferred (DEFERRED):** porttia ei ole täytetty. Tuntematonta tai lykättyä porttia ei koskaan lasketa läpäistyksi, ja se johtaa NO-GO-suositukseen.
- **NO-GO suositellaan:** vähintään yksi portti ei ole PASS, tai avointen P0-asioiden määrä on tuntematon tai suurempi kuin nolla.
- **GO-kelpoinen; ihmisen päätös odottaa:** jokainen portti on PASS ja avoimia P0-asioita on nolla. Järjestelmä ei koskaan päätä GO:ta; vain tuoteomistaja kirjaa sen.
- **Ei merkitty / Valmis / Ei valmis:** tukikohdan viimeisin merkitty tila.
- **Voimassa / Vanhentunut: katselmus muuttui:** päätös on sidottu katselmukseen sellaisena kuin se oli päätöshetkellä. Jos katselmus muuttui myöhemmin, päätös näkyy vanhentuneena ja se on harkittava uudelleen.

## Mitä et voi tehdä / varoitus
- ”GO is possible only when every gate is PASS and open P0 is zero.” Muuten GO-vaihtoehto on poissa käytöstä.
- Ehdollinen GO edellyttää samaa tilaa kuin GO (jokainen portti PASS, avointen P0-asioiden tiedetään olevan nolla) ja vähintään yhtä ehtoa. Sitä ei voi kirjata, kun jokin portti on epäonnistunut, tuntematon tai lykätty, eikä kun P0 on avoin tai tuntematon. NO-GO voidaan aina kirjata, ilman ehtoja.
- Järjestelmä, tekoäly tai automaatio ei koskaan tee pilottipäätöstä eikä koskaan keksi portteja tai hyväksyntöjä. Pilotin portit ja päätökset ovat ihmisten kirjauksia.
- Et voi muuttaa kirjattua päätöstä; uusi päätös lisätään uudella järjestysnumerolla.

## Jos jokin menee vikaan
- **Sinulla ei ole voimassa olevaa tuoteomistajan nimitystä. Päätökset ovat vain luku -tilassa.** Nimitykset määritetään palvelimella; ota yhteyttä Tenant Adminiin (13.03 §10).
- **Sinulla ei ole pääsyä näihin tietueisiin.** Tarkista roolisi ja vuokralaisen käyttöalue.
- **Tietue muuttui tai sitä on jo käytetty. Lataa uudelleen.** Katselmus muuttui päättäessäsi; avaa nykyinen katselmus painikkeella **Lataa uudelleen**. Tämä näytetään myös, kun porttien tilat muuttuivat niin, ettei päätös ole enää sallittu.
- **Tulos on epävarma. Uusi yritys lähettää saman komennon.** Lähetä sama komento turvallisesti uudelleen painikkeella **Yritä uudelleen**.
- **Säännöt hylkäsivät.** Jokin portti on lykätty.
- **Ei sallittu.** Nimityksesi ei ole voimassa tai tilisi ei ole ihmiskäyttäjän tili.
- **Palvelu ei ole käytettävissä. Yritä uudelleen.** Jos tilanne jatkuu, ilmoita tukeen korrelaatiotunnus, aika ja vaiheet (13.03 §10).

## Liittyvät sivut
