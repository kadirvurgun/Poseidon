---
key: system-health
title: Järjestelmän kunto
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
”Järjestelmän kunto” näyttää ylläpitäjille **palveluhavainnot** yhdellä näytöllä: API, auditointikirjoittaja, synkronointi, ilmoitustyöt, tiedostopalvelu ja syötteiden ajantasaisuus. Se vastaa kysymykseen: ”Mitkä palvelusignaalit havaittiin valitulle alukselle ja tälle käyttöalueelle, ja mitkä eivät?” Määritykset ja puuttuva telemetria eivät osoita palvelun olevan kunnossa eivätkä aluksen valmiutta.

## Kuka sitä käyttää
- **System / Tenant Admin:** huomaa palvelu- ja synkronointiongelmat ja käsittelee ne hallitusti (13.03 §9).
- **Support / Engineering:** tarkastelee havaintoja, kun sovellusvirheestä on ilmoitettu (13.03 §10).

## Ennen kuin aloitat
- Sivu näkyy vain käyttäjille, joilla on koko käyttöalueen lukuoikeus, toimintahistorian lukuoikeus ja järjestelmän kunnon lukuoikeus.
- Tarkista ylhäällä olevasta kontekstipalkista, että **oikea yritys** on valittuna.
- Havainnot haetaan aluskohtaisesti; sinun on ensin valittava alus.

## Vaihe vaiheelta
1. Valitse alus kohdassa **Alushavaintojen käyttöoikeusalue**. Pitkässä luettelossa käytä painikkeita **Edelliset alukset** / **Seuraavat alukset**.
2. Tarkista ”Luettu”-aika.
3. Lue kohdasta **Palvelun saatavuus** prosessin käynnistysaika ja käyntiaika.
4. Lue **API**- ja **Auditointikirjoittaja**-korteista pyyntö-, virhe-, tallennus- ja viivelukemat. Ne kattavat tämän käyttöalueen prosessin käynnistymisestä lähtien.
5. Katso kohdasta **Synkronointi** vahvistetut tulokset, ratkaisemattomat ristiriidat ja viimeisin vahvistettu synkronointi.
6. Katso kohdasta **Ilmoitustyöt** avoimet ilmoitukset, määräajan ylittäneet eskalaatiot ja arviointityön tila.
7. Lue kortit **Tiedostopalvelu** ja **Syötteen ajantasaisuus**.
8. Tarkastele kohdassa **Pilotin mittarit** pilottiin rekisteröityjen asiakkaiden havaintoja.
9. Paina **Päivitä havainnot** saadaksesi ajantasaiset tiedot.

## Mitä tilat tarkoittavat
- **Ei havaittu / Ei käytettävissä:** tälle signaalille ei ole havaintoa. Tämä ei tarkoita ”kunnossa” eikä ”ei ongelmaa”.
- **Määritetty · kokeilematon:** tiedostopalvelu näyttää olevan määritetty, mutta tallennustilaa tai skanneria ei ole testattu reaaliaikaisesti.
- **Aikataulussa / Käynnissä / Myöhässä / Ajastin ei ole käytössä / Ajo keskeytyi (lukitus vanhentui):** ilmoitusten arviointityön tila.
- **Ratkaisemattomat ristiriidat:** offline-ristiriidat, jotka odottavat ihmisen ratkaisua. Niitä ei koskaan yhdistetä automaattisesti.
- **Nykyinen / Vanhentunut / Tuntematon / Ei käytettävissä:** syötteiden ajantasaisuusarvioiden määrät. Tuntematon ei tarkoita ”Nykyinen”.
- **This snapshot is older than two minutes:** päivitä ennen kuin luotat siihen.

## Mitä et voi tehdä / varoitus
- Tämä sivu on vain luku -tilassa; palveluille, aluksille tai tietueille ei tehdä mitään toimenpiteitä.
- Ilmoitustietueet ovat vain sovelluksen sisäisiä; ne eivät todista toimitusta eivätkä mitään turvallisuuspäätöstä.
- Palvelun saatavuus osoittaa vain, että tämä API-prosessi vastasi; se ei todista muiden palvelujen olevan käynnissä.
- Pilotin mittarit ovat ilmoitettuja ja todentamattomia; ne eivät ole hyväksymisväite.
- Älä korjaa tietoja suoraan tuotannossa; julkaise määritysmuutokset hallitusti (13.03 §9).

## Jos jokin menee vikaan
- **Kuntotietojen käyttöoikeus ei ole käytettävissä tässä kontekstissa:** tarkista roolisi ja käyttöalueesi.
- **Palveluhavainnot eivät ole käytettävissä:** kunnossa olevaa tilaa ei päätellä; käytä painiketta ”Päivitä havainnot” hetken kuluttua.
- **On ratkaisematon ristiriita:** ohjaa asianomainen käyttäjä ristiriitakeskukseen (13.03 §10).
- **Sovellusvirhe:** ilmoita siitä tukeen/kehitykseen ja kerro korrelaatiotunnus, näkymä, aika ja vaiheet.

## Liittyvät sivut
