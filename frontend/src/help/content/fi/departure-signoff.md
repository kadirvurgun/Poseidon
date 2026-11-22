---
key: departure-signoff
title: Lähtövalmius ja päällikön kuittaus
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tämä sivu näyttää valitun laivan ja suunnitellun lähtöjakson **neuvoa-antavan valmiusarvion** ja kirjaa nimitetyn päällikön päätöksen. Se vastaa kysymykseen: ”Mitä miehistö-, tekniset, lakisääteiset, hätävalmius- ja turvallisuustapahtumalähteet kertovat tästä jaksosta, ja miksi?” Päätös lähdön vapauttamisesta, pidättämisestä tai estämisestä kuuluu **päällikölle** (13.03 §2). Järjestelmä ei koskaan vapauta lähtöä; kirjattu kuittaus ei ole lähtöselvitys.

## Kuka sitä käyttää
- **Päällikkö:** tarkastaa arvion ja ”Miksi”-luettelon ja kirjaa sitten Vapauta, Pidätä tai Hylkää perusteluineen (13.03 §2).
- **Maaoperaatiot / tekninen tarkastaja:** seuraavat arviota ja kirjattuja päällikön päätöksiä vain luku -tilassa. Maahenkilöstö ei voi kirjata päätöstä päällikön puolesta.

## Ennen kuin aloitat
- Tarkista ylhäällä olevasta kontekstipalkista, että **oikea yritys** on valittuna. Sivu listaa vain käyttöalueesi laivat (13.03 §1).
- Sivun näkeminen vaatii lähtövalmiuden lukuoikeuden.
- Pelkkä päätösoikeus ei riitä päätöksen kirjaamiseen: tarvitset myös **voimassa olevan päällikön nimityksen** tälle laivalle. Ilman sitä sivu ilmoittaa ”You hold no current Master appointment for this ship. Decisions are read-only.”

## Vaihe vaiheelta
1. Valitse laiva luettelosta ”Valitse alus”.
2. Täytä kohdassa **Suunniteltu lähtöjakso** kentät ”Lähtöpäivä” ja ”Matkan loppu” ja paina **Arvioi valmius**.
3. Lue **Arvio**-riviltä tilamerkki, tila ”Voimassa” tai ”Ennuste”, ”Arvioitu” ja ”Voimassa asti”.
4. Tarkista luokkataulukosta miehistön, teknisten, lakisääteisten, hätävalmius- ja turvallisuustapahtumaluokkien tulokset.
5. Lue **Miksi**-luettelo. Jokainen rivi näyttää havainnon, joka ei läpäissyt, sen syykoodin ja lähteen. Tarkista odottamaton rivi lähteen omalla sivulla.
6. Jos arvio ei ole voimassa ja tilassa Valmis, lue **Miksi Vapauta ei ole käytettävissä**. Se kertoo, milloin jakso on ennuste, listaa luokat, jotka eivät vielä läpäise, tuloksineen ja selittää, mikä tietue sulkisi hätävalmiuden (61.02-harjoituslähdettä ei ole vielä kytketty) ja lakisääteisen (pakollisten todistusten joukkoa ei ole määritelty) lähdeaukot. Se vain selittää palvelimen luokat; se ei muuta porttia.
7. Näet kohdassa **Päällikön päätökset** aiemmin kirjatut päätökset ja niiden voimassaolon.
8. Jos olet päällikkö, valitse kohdassa **Kirjaa päällikön päätös** Vapauta, Pidätä tai Hylkää, valitse nimityksesi kentässä ”Nimitys”, kirjoita ”Peruste” (10–2000 merkkiä) ja paina **Kirjaa**.
9. Lue arvio uudelleen painikkeella **Päivitä**.

## Mitä tilat tarkoittavat
- **Valmis:** missään kirjatussa lähteessä ei ole havaintoa, joka ei läpäissyt. Tämä on vain tietoa; lähtöpäätös on edelleen päällikön.
- **Ehdollinen:** vähintään yksi lähde ilmoittaa ehdon. Vapauta-vaihtoehtoa ei tarjota tälle arviolle.
- **Ei valmis:** vähintään yksi lähde palauttaa ”Hylätty”. Muiden luokkien läpäisy ei kumoa sitä.
- **Tarkastus vaaditaan:** vähintään yksi syöte on ”Tuntematon”. Valtuutetun henkilön on tarkasteltava sitä.
- **Hyväksytty / Ehto / Hylätty / Tuntematon:** luokan tulos luokkataulukossa. **Tuntematon** tarkoittaa, että lähdettä ei voitu lukea tai siinä ei ole tietoja; se ei tarkoita ”Hyväksytty” eikä ”Valmis”. Tässä versiossa hätävalmiusluokkaan ei ole kytketty hätävalmiuslähdettä, joten rivi näyttää Tuntematon. Lakisääteinen voi jäädä tilaan Tuntematon, vaikka jokainen kirjattu todistus olisi pätevä, koska mikään lähde ei määrittele laivan pakollisten todistusten joukkoa. Lakisääteinen sisältää myös tietojen luotettavuussignaalit (DATA_TRUST_*); ilman luotettavuussignaalien lukuoikeutta Lakisääteinen näyttää Tuntematon.
- **Ennuste:** jakso ei sisällä tätä päivää. Se on vain suunnitteluennuste eikä sitä voi vapauttaa.
- **Voimassa / Vanhentunut — syötteet muuttuivat:** kirjatun päätöksen voimassaolo. Kun syötteet muuttuvat, aiempi päätös vanhentuu eikä sitä poisteta.

## Mitä et voi tehdä / varoitus
- Järjestelmä ei koskaan vapauta lähtöä eikä näytä mitään tilaa lähtöselvityksenä. Kirjattu päätös on nimitetyn päällikön oma ihmisen kirjaus.
- Vapauta voidaan kirjata vain voimassa olevalle READY-arviolle; jokaisen luokan on läpäistävä. Koska hätävalmiusluokka pysyy tässä versiossa tilassa Tuntematon, arvio ei voi saavuttaa tilaa Valmis eikä Vapauta-vaihtoehtoa tarjota; **Miksi Vapauta ei ole käytettävissä** näyttää syyn. Valmis-tilassakin vapautuspäätös on päällikön; järjestelmä ei hyväksy mitään. ”Pidätä ja Hylkää voidaan aina kirjata perusteluineen.”
- Jokainen päätös vaatii perustelun. Kirjattuja päätöksiä ei muuteta; uusi päätös on uusi tietue.
- Et voi kirjata päätöstä, kun arvio on vanhentunut tai olet offline-tilassa; päivitä ensin.

## Jos jokin menee vikaan
- **Olet offline-tilassa:** arvio voi olla vanhentunut ja kuittaus on poissa käytöstä. Päivitä, kun olet taas verkossa (13.03 §10).
- **Sinulla ei ole pääsyä tämän aluksen lähtövalmiuteen:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään.
- **Odottamaton valmius:** avaa ”Miksi”-luettelo; jos lähde vaikuttaa väärältä, vie asia kontrollin omistajan tai aihealueen asiantuntijan tietoon.
- **Lopputulos on epävarma:** ”Yritä uudelleen” lähettää saman komennon. Älä kirjoita uutta päätöstä.
- **Päätöshistoria muuttui / Arvio muuttui:** käytä painiketta ”Lataa uudelleen”, tarkista nykyinen arvio ja päätä sitten.
- **Komentoa ei hyväksytty:** ilmoita siitä tukeen kuvakaappauksen, ajan ja vaiheidesi kanssa.

## Liittyvät sivut
