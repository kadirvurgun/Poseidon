---
key: towage-plan
title: Hinaussuunnitelma
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Hinaussuunnitelma kirjaa lähetetylle hinaajatyölle **hinaajien roolit ja kokoonpanon, tiedonvaihdon, toimintarajat, viestinnän, hätävapautuksen, varautumisen ja takaisiniskun vaara-alueen hallintatoimet**. Se vastaa kysymykseen: ”Miten tämä työ tehdään, missä rajoissa ja millaisin hätäjärjestelyin?” Suunnitelman hyväksyntä on nimitetyn päällikön tai hinaajan päällikön ihmisen tekemä kirjaus; se ei ole GO-päätös eikä lupa. Rajat ovat suunnitelman lähtötietoja, jotka ilmoitetaan lähteineen; järjestelmä ei anna oletusarvoja.

## Kuka sitä käyttää
- **Hinaajaoperaatioiden koordinaattori / suunnittelija:** laatii suunnitelman ja lähettää sen hyväksyttäväksi (13.03 §7).
- **Hinaajan päällikkö / Päällikkö:** vahvistaa kokoonpanon, köyden, BP:n/suorituskyvyn, viestinnän, rajat, varautumisen ja työn keskeytyksen ehdot sekä hyväksyy tai palauttaa suunnitelman (13.03 §3).

## Ennen kuin aloitat
- Avaa hinaaja kohdasta **Hinaajatoiminnot** ja valitse työ kohdasta **Valitse lähetetty hinaustyö**.
- Suunnitelman muokkaaminen vaatii hinaajan kirjoitusoikeuden ja suunnitelman kirjoitusoikeuden. Suunnitelman hinaajat valitaan hinaajista, joilla on tälle työlle voimassa oleva määräysehdotus.
- Hyväksyminen tai palauttaminen edellyttää koordinoivalle hinaajalle palvelimelle kirjattua **Nimitys**-tietoa (hinaajan päällikkö tai päällikkö).

## Vaihe vaiheelta
1. Lue **Tila** ja **Versio** ja sen jälkeen luettelo **Puuttuu ennen lähettämistä**.
2. Käytä kohdassa **Hinaajien roolit ja kokoonpano** painiketta **Lisää** jokaiselle hinaajalle; syötä **Sijainti**, **Hinausmenetelmä**, **Koordinoiva (johtava) hinaaja**, **Hinausköyden lähde**, **Kiinnityspiste**, **Hinausköyden varusteen tunnus** ja **Hätävapautuksen varusteen tunnus**.
3. Kirjoita **Päällikön / luotsin / hinaajan päällikön tiedonvaihto** -teksti.
4. Syötä kohdassa **Toimintarajat (suunnitelman lähtötiedot)** kullekin **Parametri**-kohdalle **Arvo** ja **Rajan lähde**. Uusi raja alkaa tyhjänä.
5. Täytä **Viestintäsuunnitelma** (ensisijainen ja varakanava, työkieli, viestintäyhteyden katkeamisen menettely), **Hätävapautus**, **Varautuminen** ja **Takaisiniskun vaara-alueen (snap-back) hallintatoimet**.
6. Tallenna painikkeella **Luo luonnos** tai **Tallenna luonnos**; kun mitään ei puutu, paina **Lähetä hyväksyttäväksi**.
7. Hyväksyjä valitsee **Nimitys**-tiedon, kirjoittaa vähintään 10 merkin **Peruste**-tekstin ja painaa **Hyväksy** tai **Palauta**.
8. Jos hyväksyttyä suunnitelmaa on muutettava, kirjoita peruste ja käytä painiketta **Muuta**. Luettelot **Hyväksyntätietueet** ja **Versiot** näyttävät historian.

## Mitä tilat tarkoittavat
- **Luonnos:** suunnitelmaa voi muokata.
- **Tarkastuksessa:** suunnitelma lähetettiin hyväksyttäväksi; päätöstä odotetaan.
- **Palautettu:** hyväksyjä palautti suunnitelman perusteineen; korjaa ja lähetä se uudelleen.
- **Hyväksytty:** päällikkö tai hinaajan päällikkö hyväksyi tämän suunnitelman version. Tämä ei ole GO-päätös; valmiustarkastus ja tiedotustilaisuus vaaditaan edelleen.
- **Tuntematon:** kentälle ei valittu arvoa. Takaisiniskun vaara-alueiden ”Tuntematon” ei koskaan korvaa arvoa ”Ei”.
- **Hinaajapyyntö muuttui tämän suunnitelman tallentamisen jälkeen.:** suunnitelma perustuu pyynnön vanhempaan versioon, ja se on tarkastettava.

## Mitä et voi tehdä / varoitus
- Suunnitelman laatinut tai lähettänyt henkilö ei voi hyväksyä sitä.
- Suunnitelmaa, josta puuttuu pakollisia osioita, ei voi lähettää; keskeneräisen luonnoksen voi tallentaa.
- Järjestelmä ei ehdota raja-, kanava- eikä kynnysarvoja; jokainen arvo tulee suunnittelijalta lähteineen.
- Hyväksytty suunnitelma ei ole lupa aloittaa; aloittaminen edellyttää ajantasaisia tiedotustilaisuuden kuittauksia ja hinaajan päällikön GO-kirjausta (13.03 §3).
- Komennot ovat poissa käytöstä offline-tilassa.

## Jos jokin menee vikaan
- **Viesti ”Pakollisia osioita puuttuu.”:** täydennä kohdan **Puuttuu ennen lähettämistä** koodit.
- **Viesti ”The record changed”:** tarkastele nykyistä suunnitelmaa painikkeella **Lataa uudelleen**.
- **Viesti ”The outcome is unknown”:** **Yritä uudelleen** lähettää saman pyynnön uudelleen.
- **Ei valtuutusta:** tarkista roolisi, hinaajasi ja nimityskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
