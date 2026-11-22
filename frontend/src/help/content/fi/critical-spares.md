---
key: critical-spares
title: Kriittiset varaosat ja hankintapyynnöt
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Kriittiset varaosat ja hankintapyynnöt -sivu näyttää valitun aluksen **ilmoitetun varaston, minimin ja maksimin, alhaisen varaston hälytykset ja tekniset hankintapyyntöehdotukset**. Se vastaa kysymykseen: ”Mikä kriittinen varaosa saavutti miniminsä, moniko avoin työmääräys viittaa siihen, ja onko tekninen hankintapyyntö ehdotettu ja päätetty?” Määrät ovat ihmisten ilmoituksia, eikä niitä ole todennettu varastoa vasten.

## Kuka sitä käyttää
- **Konepäällikkö / tekninen tarkastaja:** seuraa kriittisten varaosien minimirajoja ja aloittaa teknisen hankintapyynnön (13.03 §5). Hankintapyynnön ehdottaja ja sen hyväksyjä ovat eri henkilöitä.

## Ennen kuin aloitat
- Katselu vaatii oikeuden `spare.read`. Varaosien määrittely ja laskentojen kirjaaminen vaatii oikeuden `spare.write`, hankintapyynnön ehdottaminen oikeuden `requisition.propose` ja päättäminen oikeuden `requisition.approve`.
- Ehdotukset ja päätökset vaativat sisäänkirjautuneen ihmiskäyttäjän.
- Valitse oikea alus **Alus**-luettelosta.

## Vaihe vaiheelta
1. Valitse alus. **Kriittiset varaosat** -näkymä avautuu. Lue ylhäältä **Alhainen varasto**- ja **Tarkastus vaaditaan** -määrät.
2. Lue taulukon kultakin riviltä **Varaosan kriittisyys**, **Laitteen kriittisyys**, **Varaston tila**, **Ennuste**, **Min / max** ja **Avoimet työmääräykset**.
3. Paina **Uusi kriittinen varaosa** uutta varaosaa varten tai **Muokkaa** olemassa olevaa varten. Syötä **Osanumero**, **Nimi**, **Yksikkö**, **Minimivarasto** ja **Maksimivarasto**. Jätä rajat tyhjiksi, jos niitä ei ole päätetty.
4. Syötä laskenta valitsemalla **Kirjaa uusi varastolaskenta**, täytä **Laskettu määrä** ja paina **Tallenna**.
5. Paina **Alhainen varasto** -rivillä, jolla ei ole avointa hankintapyyntöä, **Ehdota hankintapyyntöä**. Palvelin laskee ehdotetun määrän (maksimi miinus ennustettu varasto). Jos maksimia ei ole asetettu, määrää ei ehdoteta ja hyväksyjän on syötettävä **Hyväksytty määrä**.
6. **Tekniset hankintapyynnöt** -näkymässä valtuutettu henkilö, joka ei tehnyt ehdotusta, tarkistaa **Hyväksytty määrä** -arvon ja painaa **Hyväksy ERP-vientiin** tai valitsee **Hylkäyksen syy** -arvon ja painaa **Hylkää**.
7. Avaa hyväksytyn hankintapyynnön **ERP-vientiviite** nähdäksesi viitteen.

## Mitä tilat tarkoittavat
- **Alhainen varasto:** ennustettu varasto (viimeisin ilmoitettu laskenta miinus laskennan jälkeen lähetetyillä työmääräyksillä kirjatut varaosat) on minimissä tai sen alle. Hankintapyyntöehdotus tarjotaan tällä rivillä.
- **Minimin yläpuolella (ilmoitettu):** ennustettu varasto on minimin yläpuolella; laskentaa ei ole todennettu.
- **Minimiä ei ole määritetty:** rajaa ei syötetty. Tyhjä raja raportoidaan tarkastusta vaativana, ei koskaan kunnossa olevana.
- **Varasto tuntematon / Kulutus selvittämättä:** varastoa ei voitu laskea. Tuntematon ei tarkoita ”riittävästi varastoa”.
- **Ehdotettu / Hyväksytty ERP-vientiin / Hylätty:** teknisen hankintapyynnön tila.
- **Kriittinen (ilmoitettu) / Ei-kriittinen (ilmoitettu) / Tuntematon:** varaosan kriittisyys on ilmoitus.

## Mitä et voi tehdä / varoitus
- Et voi päättää hankintapyynnöstä, jonka itse ehdotit. Sivu ilmoittaa: ”Ehdotit tätä itse; toisen henkilön on päätettävä.”
- Ehdotus ja hyväksyntä ovat erillisiä ihmisen tekoja. Hyväksyntä luo vain ERP-vientiviitteen; ostotilauksia, toimittajia tai hintoja ei hallita täällä.
- Sivulla ei ole erillistä uudelleentilausrajan kenttää; alhainen varasto arvioidaan minimivarastoa vasten.
- Offline-tilassa tietoja ei voi päivittää eikä mitään toimintoa voi tehdä.

## Jos jokin menee vikaan
- **Tietue muuttui:** päivitä sivu painikkeella **Yritä uudelleen** ja yritä uudelleen.
- **Lopputulos tuntematon:** **Toista sama komento** lähettää saman komennon.
- **Sääntö hylkäsi pyynnön:** tarkista varaston tila ja avoimen hankintapyynnön tiedot.
- **Sinulla ei ole oikeutta:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
