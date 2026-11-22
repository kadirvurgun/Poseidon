---
key: pilot-program
title: Pilottiohjelman viikot ja arvoraportti
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Pilottiohjelma-sivu kokoaa yhteen paikkaan aluksen (ja valinnaisesti hinaajan) pilottiviikot, kunkin viikon tarkistuslistan kohdat, pilotin ongelmat, päivittäisen palautteen ja arvoraportin. Se vastaa kysymykseen: ”Onko jokaisen pilottiviikon kohdat täytetty todellisilla tietueilla, ja miten mitatut arvot muuttuivat lähtötasoon verrattuna?” Ylhäällä oleva huomautus on voimassa: ”Software cannot complete a pilot. A checklist item is satisfied only by a link you make to a real record that the server verifies; a missing baseline stays Unknown; only the appointed Product Owner closes a week or records the program decision.”

## Kuka sitä käyttää
- **Tuoteomistaja:** sulkee viikot ja kirjaa ohjelmapäätöksen (Continue, Fix, Stop, Expand). Tämä edellyttää voimassa olevaa tuoteomistajan nimitystä.
- **Pilottitiimi:** linkittää tietueita tarkistuslistan kohtiin, ilmoittaa ongelmista sekä kirjaa palautetta ja mittauksia.
- **Luokittelun omistaja:** luokittelee ja ratkaisee ongelmat.
- 13.03 ei määritä tälle sivulle erillistä roolia; vuokralaisen hallinnan periaatteet ovat voimassa (13.03 §9).

## Ennen kuin aloitat
- Katselu edellyttää koko vuokralaisen lukuoikeutta ja pilottiohjelman lukuoikeutta. Ohjelmien luominen, kirjaaminen, luokittelu ja päättäminen edellyttävät kukin omaa oikeuttaan; viikkojen sulkeminen ja päätökset edellyttävät lisäksi tuoteomistajan nimitystä.
- Linkittämiesi tietueiden (esimerkiksi todistus-, miehistö- tai työmääräystietueen) on oltava jo olemassa omilla sivuillaan.
- Älä koskaan kirjoita palautteeseen nimiä, sähköpostiosoitteita, puhelin- tai henkilötunnusnumeroita; käytä vain pseudonyymiä ja roolia.
- Kirjaaminen edellyttää toimivaa yhteyttä.

## Vaihe vaiheelta
1. Avaa ohjelma kohdasta **Pilottiohjelmat**. Uutta ohjelmaa varten täytä kohdassa **Luo pilottiohjelma** kentät **Ohjelman nimi**, **Alus**, **Hinaaja (valinnainen)**, **Viikon 1 alkamispäivä** ja **Kalenterin aikavyöhyke (IANA)** ja paina **Kirjaa**.
2. Tarkista kunkin **Viikko**-osion sarakkeet **Tarkistuslistan kohta**, **Kriteeri (lähde)**, **Alue**, **Tila** ja **Linkitetyt tietueet**.
3. Täyttääksesi kohdan anna kohdassa **Linkitä tietue** **Tarkistuslistan kohta**, **Tietuetyyppi** ja **Tietueen tunniste** ja paina **Linkitä tietue**. Poista väärä linkki painikkeella **Peru linkki** kirjoitettuasi **Perumisen syy**.
4. Kirjaa kohdassa **Pilotin ongelmat** painikkeella **Ilmoita ongelma** alue, **Luokka**, vakavuus ja **Ongelman yhteenveto**. Luokittelun omistaja käyttää toimintoa **Alustava luokittelu** ja kirjoittaa **Luokittelun huomautus**, minkä jälkeen hän ratkaisee ongelman.
5. Kirjaa kohdassa **Päivittäinen palaute** painikkeella **Kirjaa palaute** **Yhteenveto** ja **Tunnelma**.
6. Kirjaa kohdassa **Arvoraportti** painikkeella **Kirjaa mittaus** **Mittari**, **Vaihe** (lähtötaso tai pilottijakso), **Arvo**, **Otoksen koko**, **Jakso alkaen**, **Jakso päättyen**, **Näyttöviite** ja **Menetelmä ja otos**.
7. Jos olet tuoteomistaja, kirjaa tulos ja perustelu kohdassa **Sulje viikko**. Ohjelman lopussa täytä päätös, **Nimitys** ja **Perustelu** kohdassa **Kirjaa ohjelmapäätös** ja paina **Kirjaa**.

## Mitä tilat tarkoittavat
- **Linkitetty ja varmennettu:** kohtaan on linkitetty palvelimen varmentama todellinen tietue. Kohdat, joiden tila on **Linkki ei ole enää voimassa** tai **Puuttuu**, eivät ole täytettyjä.
- **Completion required:** linkitetyn tietueen on oltava valmiissa tilassa. ”Audit trace proves that a person wrote the record; it does not prove the record or flow is completed.”
- **Valmis ihmisen sulkemaksi / Ei valmis:** viikkoa koskeva suositus; vain tuoteomistaja sulkee viikon tilaan **Hyväksytty** tai **Ei hyväksytty**.
- **Luokiteltu samana päivänä / Luokiteltu myöhässä / Luokittelu erääntyy tänään / Myöhässä:** P0-ongelmien luokittelun ajoitus suhteessa ohjelman kalenteripäivään. Ongelmille, joiden kohdalla näkyy **Lähteessä ei ole saman päivän tavoitetta**, järjestelmä ei keksi määräaikaa.
- **Mitattu / Tuntematon:** kun lähtötason tai pilottijakson arvo puuttuu, raportti pysyy tilassa **Tuntematon**; sitä ei koskaan lasketa nollaksi eikä parannukseksi. Arvoja, joiden tila on **Ei verrattavissa**, ei verrata.
- **Telemetria (ilmoitettu, varmentamaton):** ilmoitettu telemetria-arvo.
- **Laajennus mahdollinen; ihmisen päätös odottaa / Korjausta suositellaan:** ohjelmaa koskeva suositus, ei päätös.

## Mitä et voi tehdä / varoitus
- ”Accepted is possible only when every item is verified and no P0 triage is open, pending or late.”
- ”Expand is possible only when every week is accepted and values, UAT, defects and P0 are clear.”
- Jos olet itse laatinut näytön, johon viikko tai päätös perustuu (linkit, palaute, mittaukset), et voi sulkea kyseistä viikkoa etkä kirjata kyseistä päätöstä.
- Kuvitteelliselle testiaineisto-ohjelmalle ei voi kirjata viikon sulkemista eikä päätöstä.
- Järjestelmä ei koskaan vie pilottia päätökseen, ei koskaan sulje viikkoa eikä koskaan keksi päätöksiä tai hyväksyntöjä.

## Jos jokin menee vikaan
- **Sinulla ei ole voimassa olevaa tuoteomistajan nimitystä; sulkemiset ja päätökset ovat vain luku -tilassa.** Pyydä nimitystä Tenant Adminilta (13.03 §10).
- **Palautteen keruu ei ole käytettävissä ennen kuin operaattori määrittää säilytyskäytännön.** Pyydä järjestelmänvalvojaa määrittämään säilytyskäytäntö.
- **Tietue muuttui tai sitä on jo käytetty. Lataa uudelleen.** Avaa nykyinen tila painikkeella **Lataa uudelleen**.
- **Tulos on epävarma. Uusi yritys lähettää saman komennon.** Lähetä sama komento turvallisesti uudelleen painikkeella **Yritä uudelleen**.
- **Säännöt hylkäsivät.** Esimerkiksi linkitettyä tietuetta ei ehkä ole olemassa, se voi olla käyttöalueen ulkopuolella tai se ei ole valmis.
- **Ei sallittu.** Olet laatinut linkit, palautteen tai mittaukset, joihin tämä viikon sulkeminen tai päätös perustuu; toisen henkilön on kirjattava se.
- Viikkoa, joka ei ole alkanut, ei voi sulkea (näytetään viestinä ”Tietue muuttui tai sitä on jo käytetty. Lataa uudelleen.”).
- **Sinulla ei ole pääsyä näihin tietueisiin.** Tarkista roolisi ja vuokralaisen käyttöalue (13.03 §10).

## Liittyvät sivut
