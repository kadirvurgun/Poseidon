---
key: notification-escalation
title: Ilmoitukset
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Ilmoitukset-sivu listaa valitun aluksen tai hinaajan **sovelluksen sisäiset määritetyt lähdepoikkeamat** (huolto erääntyy, huolto vaatii tarkastuksen, huoltolähde muuttui tai ei ole käytettävissä). Se vastaa kysymykseen: ”Mitkä huoltoilmoitukset saapuivat roolilleni, kuinka vakavia ne ovat, mitkä eskaloitiin, koska kukaan ei kuitannut niitä ajoissa, ja mihin lähdetietueeseen ne perustuvat?” Kuittaus ei todenna todisteita eikä hyväksy operaatioita.

## Kuka sitä käyttää
- **Konepäällikkö / tekninen tarkastaja:** seuraa huoltojen määräpäiviä ja tarkastuspyyntöjä ja siirtyy lähdesuunnitelmaan (13.03 §5).
- **Hinaajan päällikkö / päällikkö / miehistö:** näkevät rooleilleen ohjatut ilmoitukset ja kuittaavat ne (13.03 §6).

## Ennen kuin aloitat
- Tarvitset ilmoitusten, huollon ja kaluston lukuoikeudet. Kuittaaminen ja asetusten tallentaminen vaativat erilliset oikeudet; ilman niitä vastaavia painikkeita ei näytetä.
- Valitse valtuutettu alus tai hinaaja kohdasta **Ilmoituksen alus**.
- Kuittaus- tai asetuskomentoa ei voi lähettää offline-tilassa.

## Vaihe vaiheelta
1. Valitse alus kohdasta **Ilmoituksen alus**.
2. Lue käytäntörivi: ”Määritetty · todentamaton lähdekäytäntö · vain sovelluksen sisäinen toimitus”.
3. Lue kunkin ilmoituksen otsikko, vakavuusmerkki (**Tieto**, **Varoitus**, **Kriittinen**) ja tilamerkki; tarkista **Lähde / revisio**, **Määräpäivä** ja **Luontiaika**. **Reitityssääntö** näyttää säännön koodin.
4. Siirry liittyvään huoltosuunnitelmaan painikkeella **Avaa lähdesuunnitelma**.
5. Kirjataksesi, että olet nähnyt ilmoituksen, paina **Kuittaa** (vain tiloissa **Avoin** tai **Eskaloitu**).
6. Jos sinulla on asetusoikeus, muokkaa asetuksia **Vaimenna ei-pakolliset ohjeelliset ilmoitukset** ja **Hiljaiset tunnit** (alku, loppu, IANA-aikavyöhyke) ja paina **Tallenna ohjeellinen asetus**.
7. Käytä painikkeita **Edelliset ilmoitukset** / **Lisää ilmoituksia** muille sivuille ja **Lataa ilmoituslaatikko uudelleen** lukeaksesi uudelleen.

## Mitä tilat tarkoittavat
- **Avoin:** ilmoitus odottaa kuittausta.
- **Kuitattu:** henkilö kirjasi nähneensä sen. Tämä ei tarkoita, että huolto olisi tehty tai todisteet todennettu.
- **Eskaloitu:** ilmoitusta ei kuitattu ajoissa, ja se ohjattiin sääntönsä eskalaatioroolille.
- **Lähde muuttui / Lähde ei saatavilla:** perustetietue muuttui tai sitä ei voida vahvistaa. Kirjattu peruste säilytetään; tämä ei poista eikä aktivoi ilmoitusta uudelleen.
- **Pakollinen reitti · ei voi vaimentaa:** asetukset eivät voi vaimentaa tätä ilmoitusta.
- **Tuntematon (Määräpäivä):** lähteessä ei ole määräpäivää. Tämä ei tarkoita, ettei mitään erääntyisi.

## Mitä et voi tehdä / varoitus
- Kuittaus ei todenna todisteita eikä hyväksy operaatioita.
- Ohjeelliset asetukset eivät voi vaimentaa pakollisia ja kriittisiä reittejä. Hiljaiset tunnit vain viivästyttävät valinnaisia ilmoituksia.
- Tyhjä sivu ei tarkoita, että kaikki olisi kunnossa. Jos käytäntö tai lähteet eivät ole käytettävissä, sivu ilmoittaa siitä erikseen.
- Automaattista uudelleenyritystä ei ole; epävarma komento säilytetään tässä välilehdessä ja kontekstin muutokset pysyvät lukittuina.

## Jos jokin menee vikaan
- **”Outcome unknown” -ilmoitus:** lähetä säilytetty komento uudelleen painikkeella **Toista täsmällinen ilmoituskomento**.
- **”Notification version changed” -ilmoitus:** lataa nykyinen luettelo painikkeella **Lataa ilmoituslaatikko uudelleen**.
- **”Notification sources unavailable or access denied.” -ilmoitus:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **”The saved quiet hours could not be applied” -ilmoitus:** kaikki ilmoitukset näytetään; tarkista hiljaisten tuntien asetukset.

## Liittyvät sivut
