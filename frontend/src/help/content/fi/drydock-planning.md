---
key: drydock-planning
title: Telakoinnin suunnittelu
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Telakoinnin suunnittelu sisältää valitun aluksen **telakointipaketit** (projekti, telakka, suunnitellut päivämäärät ja työlaajuuden kohteet). Se vastaa kysymykseen: ”Mihin työmääräykseen, vikaan, todistukseen tai katsastuksen tarkistuslistaan kukin telakoinnin työlaajuuden kohde on linkitetty, ja ovatko linkit ajan tasalla?” Työlaajuus linkitetään näihin tietueisiin (13.03 §5). **Linkitysten valmius** tarkoittaa vain linkitysten kattavuutta; se ei ole luokituslaitoksen tai lippuvaltion katsastustulos eikä merikelpoisuuslausunto.

## Kuka sitä käyttää
- **Tekninen tarkastaja:** valmistelee telakointipaketin, linkittää työlaajuuden tietueisiin, suunnittelee ja päättää sen (13.03 §5).
- **Konepäällikkö:** seuraa, miten työlaajuus linkittyy vika- ja työmääräystietueisiin, ja oikeuksin todentaa toisen henkilön päättämän paketin (13.03 §5).

## Ennen kuin aloitat
- Valitse oikea alus. Jos alusta ei ole valittu, sivu ilmoittaa **Valitse alus työtilasta jatkaaksesi.**
- Pakettien näkeminen vaatii telakoinnin lukuoikeuden. Luominen, muokkaaminen ja tilan muuttaminen vaativat kirjoitusoikeuden; todentaminen vaatii todentamisoikeuden.
- Pidä valmiina linkitettävien työmääräys-, vika-, todistus- ja katsastustarkistuslistatietueiden tunnisteet ja versiot.

## Vaihe vaiheelta
1. Avaa paketti luettelosta tai luo tyhjä luonnos painikkeella **Luo telakointipaketti**.
2. Täytä **Telakointiprojekti**, **Telakka**, **Suunniteltu alku** ja **Suunniteltu valmistuminen**.
3. Lisää kohteita painikkeella **Lisää työlaajuuden kohde**; syötä **Työlaajuuden kohde** -tyyppi, **Otsikko**, **Työpaketti**, **Luokituslaitoksen vaatimus**, **Lakisääteinen vaatimus** ja **Prioriteetti**.
4. Käytä kussakin kohteessa painiketta **Lisää linkki** ja syötä **Tietuetyyppi**, **Tietueen tunniste** ja **Kiinnitetty versio**.
5. Päättämistä varten lisää todisteviite kohtaan **Valmistumisen näyttö** painikkeella **Lisää**. Paina sitten **Tallenna**.
6. Tarkista **Suunnittelun esteet**- tai **Päättämisen esteet** -luettelo. Valitse kohdassa **Muuta tilaa** **Toiminto** (**Suunnittele**, **Takaisin luonnokseksi**, **Aloita telakointi**, **Päätä telakointi**, **Peruuta paketti**) ja vahvista se. Peruuttaminen vaatii huomautuksen. Vain nykyiselle tilalle sallitut toiminnot tarjotaan; paketin voi peruuttaa vain tilassa Luonnos tai Suunniteltu.
7. Kirjoita päätetylle paketille, jota et itse päättänyt, vähintään 10 merkin **Peruste** ja käytä painiketta **Varmenna kirjaus** tai **Hylkää varmennus**.
8. Seuraa suunniteltuja ja toteutuneita päivämääriä sekä jokaista versiota kohdassa **Aikajana**.

## Mitä tilat tarkoittavat
- **Luonnos / Suunniteltu / Käynnissä / Päätetty / Varmennettu / Peruttu:** paketin elinkaari. Päätetty ja Varmennettu eivät ole sama asia.
- **Linkit ajan tasalla / Huomio / Tarkastus vaaditaan / Keskeneräinen:** linkitysten valmius. Vaikka linkit olisivat ajan tasalla, tämä ei ole katsastustulos.
- **Linkitetty / Linkittämätön / Myöhässä / Tarkastus vaaditaan:** työlaajuuden kohteen linkkitila (Tarkastus vaaditaan, kun jokin linkki on Vanhentunut tai Ei käytettävissä).
- **Nykyinen / Vanhentunut / Ei käytettävissä:** linkitetty tietue verrattuna sen kiinnitettyyn versioon. Tarkista vanhentunut linkki ja kiinnitä se uuteen versioon toiminnolla **Kiinnitä uudelleen versioon**.
- **Tuntematon / Ei asetettu:** prioriteettia ei syötetty. Tuntematon ei tarkoita ”Normaali” eikä ”Matala”.

## Mitä et voi tehdä / varoitus
- Sivu ei päätä, että telakointi on turvallinen tai että alus on merikelpoinen.
- Et voi todentaa pakettia, jonka itse päätit. Varmennuksen hylkääminen palauttaa paketin tilaan **Käynnissä**.
- Kustannus, valuutta, toimittaja ja budjetti ovat vain ulkoisia viitteitä; täällä ei lasketa yhteen, hyväksytä eikä tilata mitään.
- Päätettyä, varmennettua tai peruttua pakettia ei voi muokata.
- Komentoja ei lähetetä offline-tilassa.

## Jos jokin menee vikaan
- **Palvelin listasi puuttuvia tai estäviä kohteita:** ratkaise esteluettelon kohdat ja yritä uudelleen.
- **Tietue muuttui tai komento on ristiriidassa:** käytä painiketta **Lataa uusin** ja tarkista.
- **Lopputulos on tuntematon:** **Yritä uudelleen** lähettää täsmälleen saman komennon uudelleen.
- **Sinulla ei ole pääsyä tähän tietueeseen:** tarkista roolisi ja aluskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Et voi lukea näitä linkitettyjä tietuetyyppejä:** tarkista oikeus tietueen omistajalta tai vuokralaisen ylläpitäjältä.

## Liittyvät sivut
