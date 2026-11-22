---
key: port-activity-sof
title: Satamatoiminta ja tapahtumaluettelo
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Satamatoiminta ja SOF -sivu säilyttää valitun laivan **satamatapahtumat** operatiivisena tietueena näyttöviitteineen ja antaa nimitetyn henkilön viimeistellä **tapahtumaluettelon (Statement of Facts, SOF)** sisäisesti. Se vastaa kysymykseen: ”Mitkä satamatapahtumat ilmoitimme, missä UTC-järjestyksessä ja millä aikavälillä, millä lasti-, syväys-, luotsi-, hinaaja-, viivästys- ja vastalauseviitteillä, ja onko nimitetty henkilö viimeistellyt tämän SOF-version?” Sivu ei tuota allekirjoitusta, seisonta-aikaa (laytime) eikä seisontamaksua (demurrage), eivätkä kirjatut tapahtumat ole ohjeita tai hyväksyttyjä suunniteltuja aikaikkunoita.

## Kuka sitä käyttää
- **Päällikkö:** säilyttää saapumisen ja satamatoiminnan operatiivisena tietueena näyttöineen ja viimeistelee SOF:n (13.03 §2).
- **Yliperämies / konepäällikkö:** kirjaa satamatapahtumia ja voi voimassa olevalla nimityksellä viimeistellä SOF:n.

## Ennen kuin aloitat
- Valitse ylhäällä olevasta kontekstipalkista **oikea yritys ja laiva**. Jos laivaa ei ole valittu, sivu näyttää ”Valitse laiva”.
- Lukeminen edellyttää oikeutta `operations.port.read`; luonnosten tallentaminen edellyttää oikeutta `operations.port.write` ja muokkausoikeutta tähän laivaan.
- Viimeistely edellyttää oikeutta `operations.port.write`, muokkausoikeutta tähän laivaan ja oikeutta `operations.port.finalize` sekä voimassa olevaa viimeistelynimitystä (päällikkö, yliperämies tai konepäällikkö) tälle laivalle.

## Vaihe vaiheelta
1. Valitse laiva. **Kirjattu satamatapahtumien aikajana** listaa palautetut tapahtumat palvelimen UTC-järjestyksessä. **Suodata palautettua sivua** rajaa vain tätä sivua.
2. Lue luettelon alla oleva **Statement of Facts -aikajana**: jokainen tapahtuma tietoineen **Kirjattu aikaväli**, **Viivästyksen syy** ja **Huomautukset**.
3. Paina **Uusi luonnostietue**. Täytä osiot **Satamakäynnin konteksti**, **Kirjattu toiminta ja ajoitus**, **Lasti- ja syväysilmoitukset** sekä **Tuki, poikkeukset ja todisteet**. Tapahtuma-ajat edellyttävät Z-merkintää tai nimenomaista UTC-poikkeamaa.
4. Paina **Tallenna todentamaton luonnos**. Jokainen tallennus lisää version kohtaan **Kirjattu versiohistoria**.
5. Avaa tallennetussa tietueessa **Statement of Factsin viimeistely**. Jos näkyy **Required facts missing**, täydennä ensin nämä kentät.
6. Kuittaa jokainen listattu ratkaisematon viite valitsemalla se, valitse **Nimitys** ja paina **Viimeistele SOF**. Järjestelmä kirjaa, kuka viimeisteli, milloin, missä tehtävässä ja millä perusteella.

## Mitä tilat tarkoittavat
- **LUONNOS:** SOF:ää ei ole viimeistelty.
- **LOPULLINEN (sisäinen):** nimitetty henkilö viimeisteli tämän version. Se on sisäinen: ei allekirjoitus eikä ulkoinen sopimus.
- **Allekirjoitusta ei kirjattu:** näytetään aina; tällä sivulla ei ole allekirjoitusta.
- **Muutettu LOPULLISEN jälkeen: uusi tarkistus ja viimeistely tarvitaan.:** tietue tallennettiin uudelleen viimeistelyn jälkeen ja on palannut tilaan LUONNOS.
- **UNKNOWN (unresolved references):** näille pakollisille viitteille ei ole omistavaa hakua. Niiden kuittaaminen ei tee niistä tunnettuja.
- **Tarkastus vaaditaan / Todentamaton luonnostietue:** satamatapahtumat ovat ilmoituksia; mitään ei ole todennettu.
- **Luonnos (ei viimeistelty) / Lopullinen (sisäinen; ei allekirjoitus):** palvelimen tälle versiolle kirjaama SOF-tila, joka näytetään tietueen yhteenvedossa. Lopullinen tila syntyy vain kirjatusta ihmisen tekemästä viimeistelystä.

## Mitä et voi tehdä / varoitus
- Järjestelmä ei koskaan viimeistele SOF:ää. Viimeistely on nimitetyn päällikön, yliperämiehen tai konepäällikön tekemä ihmisen toimi (13.03 §2).
- Viimeistele SOF pysyy poissa käytöstä niin kauan kuin pakollisia tietoja puuttuu, nimitystä ei ole tai ratkaisemattomia viitteitä ei ole kuitattu.
- Viimeistely ei ole allekirjoitus, ei ulkoinen sopimus eikä seisonta-aika- tai seisontamaksulaskelma.
- Sivun alatunnisteessa lukee: ”SOF finalization is an internal human act recorded in the finalization panel by an appointed Master, Chief Officer or Chief Engineer. Signatures, operational/legal approval and offline capture remain unavailable.”
- Luonnosten tallentaminen ja viimeistely edellyttävät voimassa olevaa online-istuntoa; tällä sivulla ei ole offline-kirjausta.

## Jos jokin menee vikaan
- **”Tälle alukselle ei ole voimassa olevaa viimeistelynimitystä.”:** tarkista roolisi ja laivakontekstisi vuokralaisen ylläpitäjän kanssa (13.03 §10).
- **”Tulos tuntematon: uusi yritys lähettää saman komennon kerran.”:** käytä painiketta **Yritä uudelleen**; älä aloita uutta viimeistelyä.
- **”Command not accepted”:** lue näytetty koodi, lataa tietue uudelleen painikkeella **Lue nykyinen tallennettu tietue** ja yritä uudelleen.
- **”The saved source changed.”:** lataa nimenomaisesti uudelleen tai hylkää muutokset ennen kuin tallennat uudelleen.

## Liittyvät sivut
