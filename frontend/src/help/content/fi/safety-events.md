---
key: safety-events
title: Turvallisuustapahtumat ja läheltä piti -tilanteet
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: [1]
reviewed: false
---
## Mihin tätä sivua käytetään
Turvallisuustapahtumat ja läheltä piti -tilanteet -sivulla voit ilmoittaa aluksen turvallisuustapahtumasta nopeasti ja seurata sen jälkeisiä ihmisten tekemiä vaiheita: alustava luokittelu, tutkinta, riippumaton katselmointi, korjaavat ja ehkäisevät toimenpiteet, todentaminen, vaikuttavuus ja sulkeminen. Se vastaa kysymykseen: ”Mitä tapahtui, kuka käsittelee asiaa, ja toimiko toimenpide todella?” Mikään täällä ei päätä vakavuutta, juurisyytä tai sulkemista automaattisesti; kukin on erillinen ihmisen kirjaus. Sivu noudattaa ketjua Havainto → Toimenpide → Todentaminen, ja **suljettu ei ole sama kuin vaikuttava** (13.03 §4).

## Kuka sitä käyttää
- **Crew / OOW / Engineer ja päällikkö:** ilmoittavat läheltä piti -tilanteista, vaaratilanteista, onnettomuuksista, väsymyksestä sekä ympäristö- tai kybertapahtumista (13.03 §2, §6).
- **DPA / HSEQ:** turvallisuussiltana seuraavat kriittisiä tapahtumia ja myöhässä olevia toimenpiteitä ja hallinnoivat tutkintaa ja toimenpiteiden vaikuttavuutta (13.03 §4).
- Alustava luokittelu, tutkinta, katselmointi, henkilöllisyyden lukeminen, toimenpiteiden kirjoittaminen, todentaminen ja vaikuttavuuden arviointi ovat erillisiä oikeuksia; kukin käyttäjä näkee vain ne osat, joihin hänellä on oikeus.

## Ennen kuin aloitat
- Valitse oikea alus (**Valitse alus**). Ilman pääsyä näet ilmoituksen ”Sinulla ei ole pääsyä tämän aluksen turvallisuustapahtumiin.”
- Ilmoittaminen vaatii ilmoitusoikeuden; luettelon lukeminen vaatii lukuoikeuden.
- **Luottamuksellinen**-ilmoitus ja **Lähetä ilmoitus** vaativat yhteyden. Offline-tilassa voit tallentaa vain nimellisen luonnoksen, ja vain etukäteen valmistellulla laitteella.
- Aika syötetään UTC-aikana.

## Vaihe vaiheelta
1. Näet aluksen tapahtumat **Tapahtumat**-luettelossa. Käytä valintaa **Vain omat ilmoitukseni** nähdäksesi vain omasi.
2. Paina uudelle tietueelle **Ilmoita tapahtumasta**. Valitse **Ilmoitustapa**-kohdassa **Nimellä** tai **Luottamuksellinen**. Jos luottamuksellinen ilmoitus tarvitsee seurantaa, valitse **Anna minulle yksityinen seurantakoodi**.
3. Täytä **Tapahtuman tyyppi**, **Milloin se tapahtui?**, **Toiminto**, **Sijainti aluksella**, **Yhteenveto**, **Mitä tapahtui**, **Toteutettiinko välittömiä toimenpiteitä?** ja tarvittaessa **Oma vakavuusarviosi**. Jos aikaa ei tiedetä, valitse **Tuntematon** ja täytä **Miksi aika on tuntematon?**.
4. Voit käyttää painiketta **Tallenna luonnos**, vaikka tiedot ovat puutteelliset. Kun **Vielä tarvitaan ennen lähettämistä** on tyhjä, paina **Lähetä ilmoitus**. Lähettämättömän luonnoksen voi perua painikkeella **Peru luonnos** perusteen kanssa.
5. Jos seurantakoodi annettiin, tallenna kohdassa **Luottamuksellinen seurantakoodisi** näkyvä koodi heti yksityisesti ja paina **Olen tallentanut sen**. Se näytetään vain kerran. Syötä se myöhemmin kohdan **Luottamuksellinen seuranta** kenttään **Seurantakoodi** ja paina **Avaa** lukeaksesi tiimin vastaukset.
6. Luokittelija kirjaa kohdassa **Alustava luokittelu** vakavuuden ja **Tutkinta vaaditaan** -tiedon perusteen kanssa ja painaa **Kirjaa luokittelu**, tai palauttaa sen painikkeella **Palauta ilmoittajalle**.
7. Tutkija painaa **Avaa tutkinta**, täyttää **Myötävaikuttaneet tekijät**, **Juurisyyt**, **Suojaukset** ja **Epävarmuudet** ja painaa sitten **Tallenna tutkinta** ja **Lähetä tarkastettavaksi**. Kohdassa **Riippumaton katselmointi** toinen henkilö valitsee **Hyväksy**, **Palauta** tai **Hylkää**.
8. Käytä kohdassa **Korjaavat ja ehkäisevät toimenpiteet** painiketta **Lisää toimenpide** ja anna vastuuhenkilö, määräpäivä ja havainto, jota toimenpide koskee. Vastuuhenkilö kirjaa **Ilmoita suoritetuksi**, toinen henkilö kirjaa **Todenna** ja kolmas kirjaa **Arvioi vaikuttavuus**.
9. Kun **Sulkemisen esteet** on tyhjä, katselmoija (katselmointioikeus), joka ei ole ilmoittaja eikä tutkinnan laatija, painaa **Sulje tapahtuma**. Kohdassa **Viestit** voit lähettää viestejä näkyvyydellä **Vain tiimi** tai **Jaettu ilmoittajan kanssa**.

## Mitä tilat tarkoittavat
- **Luonnos / Lähetetty / Palautettu / Luokiteltu / Tutkinnassa / Toimenpiteiden seuranta / Suljettu / Peruttu:** tapahtuman elinkaari.
- **Vakavuus — Ei luokiteltu / Arvioimatta:** ihmisen luokitusta ei vielä ole. **Ilmoittajan arvio** on vain ehdotus. **Severity policy: not configured – human classification only** osoittaa, ettei järjestelmä johda vakavuutta.
- **Tutkinnan tarve on UNKNOWN:** päätöstä ei ole tehty; se ei tarkoita ”ei”, ja se estää sulkemisen.
- **Toimenpide — Avoin / Päätetty / Varmennettu:** päätetty ei ole varmennettu, eikä varmennettu ole vaikuttava.
- **Vaikuttavuus — Odottaa / Vaikuttava / Ei vaikuttava / Epäselvä / Ei toimenpiteitä / Ei näkyvissä:** vaikuttavuus on ihmisen toimenpidekohtainen arvio ja erillinen sulkemisesta. **Ei toimenpiteitä** ei tarkoita vaikuttavaa.
- **Avoimet turvallisuustapahtumat** -yhteenveto: **Avoimia vakavia tapahtumia**, **Tarkastus vaaditaan** tai **Ei avoimia vakavia tapahtumia**. Niin kauan kuin vakavuuskäytäntöä ei ole määritetty, tämän kaikki kunnossa -tilan sijaan näytetään **Vakavuuskäytäntöä ei ole määritetty – katselmointi vaaditaan**, koska vakavia tapahtumia ei voida sulkea pois. Se on vain varoitus; se ei anna valmiuspäätöstä.
- **Luottamuksellinen (suojattu):** ilmoittajan henkilöllisyys on rajatussa holvissa eikä sitä näytetä lukijoille.
- **Kirjattu offline-tilassa / Odottaa synkronointia / Tallennettu tälle laitteelle. Ei vielä lähetetty.** Jonossa oleva ei ole lähetetty.

## Mitä et voi tehdä / varoitus
- Ilmoittaja ei voi luokitella omaa ilmoitustaan. Tutkinnan laatija, lähettäjä tai ilmoittaja ei voi antaa riippumatonta katselmointia.
- Vain toimenpiteen vastuuhenkilö ilmoittaa suorituksesta. Vastuuhenkilö tai suorittaja ei voi todentaa; vastuuhenkilö, suorittaja tai todentaja ei voi arvioida vaikuttavuutta. Nämä ovat eri henkilöiden erillisiä kirjauksia.
- Juurisyyt ovat ihmisten lausumia; järjestelmä ei koskaan ehdota juurisyytä.
- Laatija ei voi koskaan perua tai poistaa lähetettyä ilmoitusta.
- Luottamuksellisessa ilmoituksessa henkilöllisyys avataan vain tiedontarpeen perusteella henkilölle, jolla on henkilöllisyyden lukuoikeus ja joka valitsee **Tarkoitus**-kohdan; jokainen lukukerta kirjataan tarkoituksineen. Nimetön ilmoittaminen vaatii julkisen vastaanottokanavan, jota ei ole määritetty.
- Epäselvä-arvion voi toistaa; Vaikuttava ja Ei vaikuttava ovat lopullisia; toimenpide, joka ei toiminut, vaatii uuden jatkotoimenpiteen.

## Jos jokin menee vikaan
- **Palvelin hylkäsi komennon:** täydennä sen alla luetellut puuttuvat tai estävät kohdat.
- **Tietue muuttui sillä välin. Lataa uudelleen ja tarkista ennen uutta yritystä.** Paina **Lataa uudelleen**.
- **Lopputulos on tuntematon (yhteys katkesi).** Käytä painiketta **Yritä samaa komentoa uudelleen**; sitä ei sovelleta kahdesti.
- **Tämä toiminto ei ole sinulle sallittu.** Tarkista roolisi, aluksesi ja kohteen konteksti; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).
- **Laiteavain ei ole käytettävissä** tai **Tarvitaan uusi valtuutus verkossa:** kirjatut tiedot säilyvät; valtuuta uudelleen, kun olet verkossa. Sovellusvirheessä ota yhteyttä tukeen ja kerro korrelaatiotunnus, näkymä, aika ja vaiheet (13.03 §10).

## Liittyvät sivut
