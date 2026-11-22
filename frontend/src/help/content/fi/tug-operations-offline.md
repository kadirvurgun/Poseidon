---
key: tug-operations-offline
title: Hinaajan offline-operaatiot
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Tämä sivu säilyttää laitteella hinaajatyön **etukäteen haetun tilannekuvan** ja asettaa ilman yhteyttä tehdyt kolmenlaiset tallenteet **pysyvään, salattuun jonoon**: tiedotustilaisuuden kuittaukset, suorituksen havainnot ja päiväkirjamerkinnät. Se vastaa kysymykseen: ”Mitä voin turvallisesti tallentaa ilman yhteyttä, mitä jonossa odottaa ja miten palvelin otti sen vastaan?” Päällikön vapautus, valmiuden GO, suorituksen aloitus/siirtymät, varmennus ja tarkastukset pysyvät verkossa tehtävinä ihmisen kirjauksina.

## Kuka sitä käyttää
- **Hinaajan päällikkö:** jatkaa kriittistä työtä heikon yhteyden alueella ja seuraa synkronoinnin tilaa (13.03 §10).
- **Miehistö:** tekee tiedotustilaisuuden kuittaukset ja vahtimerkinnät offline-tilassa ja ratkaisee ristiriidat olettamatta, että oma kirjaus voittaa automaattisesti (13.03 §6).

## Ennen kuin aloitat
- Avaa sivu hinaajatyöstä; ilman työtä sivu näyttää viestin ”Avaa tämä näkymä hinaajan toimeksiannosta hakeaksesi sen tilannekuvan etukäteen.”
- Rivin **Offline-käytäntö** on näytettävä määritetty tallennuskäytäntö (`CONFIGURED_UNVERIFIED`); ilman käytäntöä tilannekuvan ennakkohaku ja offline-tallennus eivät ole käytettävissä.
- Hae tilannekuva etukäteen **yhteyden ollessa päällä**. Tarkista ennen tallentamista, ettei tilannekuvan vuokra ole päättynyt.

## Vaihe vaiheelta
1. Paina yhteyden ollessa päällä **Hae toimeksiannon tilannekuva etukäteen**. Näkyviin tulee viesti ”Tilannekuva sinetöity tälle laitteelle.”
2. Tarkista kohdasta **Toimeksiannon tilannekuva** työn numero, sen tila ja **Vuokra voimassa asti**.
3. Jos olet julkaistun tiedotustilaisuuden osallistuja, valitse kohdassa **Kuittaa briefing** **Kuittaa** tai **Kiistä** (vähintään 10 merkin perusteella) ja paina **Sinetöi ja lisää jonoon**.
4. Valitse käynnissä olevalle suoritukselle kohdassa **Kirjaa suorituksen havainto** suoritus ja havainnon tyyppi (Huomautus, Hinausköysi kiinnitetty, Hinausköysi irrotettu), kirjoita huomautus ja paina **Sinetöi ja lisää jonoon**.
5. Valitse luonnos- tai palautetulle päiväkirjalle kohdassa **Päiväloki-merkintä** päiväkirja, kirjoita **Päivän yhteenveto** ja paina **Sinetöi ja lisää jonoon**.
6. Seuraa kunkin tallenteen tilaa kohdassa **Jonotetut tallenteet**. Tallenteet lähetetään järjestyksessä, kun yhteys palaa; lähetä käsin painikkeella **Lähetä jonotetut tallenteet**.
7. Jos tallenteessa näkyy **Olennainen ristiriita**, lue **Muuttunut palvelimella**, valitse nimenomaisesti **Säilytä palvelimen tietue** tai **Käytä tallentamaani arvoa nykyiseen tietueeseen** ja paina **Ratkaise ristiriita**.

## Mitä tilat tarkoittavat
- **Jonossa laitteella (ei lähetetty):** tallenne on sinetöity tälle laitteelle; jonossa oleva ei ole lähetetty.
- **Palvelin hyväksyi:** palvelin vastaanotti tallenteen. Tämä ei ole hyväksyntä eikä varmennus.
- **Olennainen ristiriita:** palvelimen tietue muuttui tallentamisen jälkeen; sitä ei koskaan yhdistetä automaattisesti, ja se odottaa ihmisen päätöstä.
- **Palvelimen arvo säilytetty / Palvelin hylkäsi:** palvelimen ratkaisu tallenteelle.
- **UNKNOWN (offline):** käytännön tilaa ei voitu lukea offline-tilassa; mitään käytäntöä ei oleteta.

## Mitä et voi tehdä / varoitus
- Vain verkossa: päällikön vapautus, valmiuden GO, suorituksen aloitus ja siirtymät, tiedotustilaisuuden julkaisu, varmennus sekä päiväkirjan lähettäminen ja tarkastus.
- Olennaisia ristiriitoja ei koskaan yhdistetä automaattisesti; ratkaisu on aina ihmisen nimenomainen valinta (13.03 §1).
- Uutta tallennetta ei voi tehdä, jos tilannekuvan vuokra on päättynyt; hae uusi tilannekuva etukäteen.
- Uudelleentoistossa jokainen tallenne käsitellään kerran alkuperäisellä tunnuksellaan; älä luo samaa tallennetta uudelleen.

## Jos jokin menee vikaan
- **Viesti ”The device key for this queue is unavailable.”:** tallennetut kirjaukset säilytetään, mutta niitä ei voi lukea; älä poista niitä ja ota yhteyttä tukeen.
- **Viesti ”Outcome unknown”:** samaa tallennetta yritetään uudelleen alkuperäisellä tunnuksella; älä luo uutta.
- **Viesti ”Offline-käytäntö ei salli tilannekuvaa.”:** ota yhteyttä vuokralaisen ylläpitäjään käytännön määrityksestä (13.03 §10).
- **Olennainen ristiriita:** käytä tämän sivun **Ratkaise ristiriita** -lomaketta; ristiriidat näkyvät myös kohdassa **Olennaisten ristiriitojen keskus** (13.03 §10).

## Liittyvät sivut
