---
key: operational-downtime
title: Operatiivinen seisokki
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tämä sivu kirjaa laivan **ilmoitetut operatiiviset keskeytykset** (seisokit) lähdeketjuineen. Se vastaa kysymykseen: ”Mikä keskeytys tälle laivalle ilmoitettiin, milloin, mistä syystä ja mihin vikaan tai työmääräykseen linkitettynä, ja missä ovat sen tarkastus ja sulkeminen?” Sivu ei päätä vuokranmenetyksen (loss of hire) oikeudesta, turvallisesta käytöstä eikä mistään turvallisuusvapautuksesta.

## Kuka sitä käyttää
- **Konepäällikkö / tekninen tarkastaja:** kirjaa teknisen vian tai huollon aiheuttaman seisokin ja linkittää sen vika- tai työmääräyshistoriaan (13.03 §5).
- **Päällikkö:** seuraa seisokin operatiivista vaikutusta ja tarkastaa sen nimitettynä (13.03 §2).
- **Nimitetty tarkastaja (päällikkö, yliperämies, konepäällikkö, laivastopäällikkö, DPA):** todentaa seisokkitietueen, palauttaa sen korjattavaksi tai sulkee sen.

## Ennen kuin aloitat
- Tarkista ylhäällä olevasta kontekstipalkista, että **oikea yritys** on valittuna; sivu listaa vain käyttöalueesi laivat.
- Lukeminen vaatii seisokkien lukuoikeuden; luonnoksen avaaminen vaatii kirjoitusoikeuden. Tarkastustoiminnot vaativat lisäksi tarkastusoikeuden ja voimassa olevan tehtävänimityksen tälle laivalle.
- Linkitetyn seisokin avaamiseen voit käyttää vika- tai työmääräyssivun painiketta **Avaa linkitetty seisokkiluonnos**.

## Vaihe vaiheelta
1. Valitse laiva luettelosta ”Valitse laiva”. **Kirjatut operatiiviset keskeytykset** -luettelo avautuu.
2. Rajaa tämän sivun tietueita kentällä **Hae syitä tältä sivulta**.
3. Uutta tietuetta varten paina **Uusi seisokkiluonnos**.
4. Valitse kullekin kentälle ensin sen arvotila: **Puuttuu**, **Tuntematon** tai **Ilmoitettu arvo**. Syötät arvon vain, kun ”Ilmoitettu arvo” on valittuna.
5. Kirjoita kenttiin ”Keskeytyksen alku” ja ”Keskeytyksen loppu” täydellinen aikaleima sekunteineen ja nimenomaisine UTC-siirtymineen.
6. Paina **Tallenna arvioimaton luonnos**. Keskeneräinen luonnos voidaan tallentaa.
7. Linkitä kohdassa **Seisokin tarkistus ja sulkeminen** tarvittaessa korvausvaatimuksen todiste valinnalla **Valitse todiste** ja paina sitten **Lähetä tarkastettavaksi**.
8. Jos olet nimitetty tarkastaja, valitse ”Nimitys” ja käytä painiketta **Todenna**, **Palauta korjattavaksi** tai **Sulje**.
9. Tarkastele aiempia versioita kohdassa **Muuttumaton luonnoshistoria**.

## Mitä tilat tarkoittavat
- **Luonnos / Tarkastus vaaditaan:** kirjattu sisältö on arvioimaton. Syyt ovat kohdassa ”Miksi tämä luonnos pysyy arvioimattomana”.
- **Ei lähetetty / Avoin / Tarkastuksessa:** tietueen elinkaaren tila.
- **Todennettu (ihminen):** muu nimitetty henkilö kuin kirjaaja todensi sen. Valmis ei ole Todennettu (13.03 §1).
- **Suljettu:** seisokki suljettiin. Todennettu sulkeminen ei ole turvallisuusvapautus.
- **Tuntematon:** arvoa ei ilmoitettu tai sitä ei voitu määrittää. Tuntematon ei tarkoita ”Ei” eikä ”Ei sovellu”. Tästä syystä vuokranmenetys ja sulkeminen pysyvät tilassa ”Tuntematon”.
- **Loss of Hire -ehdokas:** vain ehdokas; se ei ilmoita oikeutta eikä summaa.
- **Kirjattu aikaväli:** arvioimaton tekninen havainto; se ei korvaa ilmoitettuja minuutteja.

## Mitä et voi tehdä / varoitus
- Seisokin kirjannut henkilö ei voi todentaa eikä sulkea sitä; sen on tehtävä toisen nimitetyn henkilön.
- Lähettäminen, todentaminen ja sulkeminen eivät ole mahdollisia, kun pakollisia tietoja puuttuu.
- Korvausvaatimuksen todisteen linkittäminen ei ole todentamista.
- Omistavasta lähteestä (vika, työmääräys) ei kopioida kuvausta, aikaa eikä sulkemispäätöstä; vain täsmällinen versiolinkki säilytetään.
- Offline-tilassa nykyisiä lähteitä ei voi lukea eikä offline-tallennusta ole; syötteesi säilyvät vain avoimella sivulla.

## Jos jokin menee vikaan
- **Tallennuksen lopputulos on tuntematon:** alkuperäinen komentosi on suojattu. Käytä samalla tilillä toimintoa ”Toista täsmällinen seisokkikomento”.
- **Tallennettu luonnos muuttui:** joku muu muutti tietuetta. Syötteesi säilyvät; lataa uudelleen toiminnolla ”Lue nykyinen luonnos” tai hylkää.
- **Seisokkitietueet tai nykyinen käyttöoikeus eivät ole käytettävissä:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Lähde muuttui:** käytä omistavan lähteen nykyistä versiota uudelle luonnokselle.
- **Komentoa ei hyväksytty:** ilmoita siitä tukeen kuvakaappauksen, ajan ja vaiheidesi kanssa.

## Liittyvät sivut
