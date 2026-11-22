---
key: tug-readiness
title: Hinaajan valmiustarkastus
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Mihin tätä sivua käytetään
Valmiustarkastus arvioi hinaajan osoituksen operatiivisen valmiuden lähdetarkistusten perusteella **vain neuvoa-antavasti** ja antaa nimitetyn hinaajan päällikön tai päällikön kirjata perustellun **GO/NO-GO-päätöksen**. Se vastaa kysymykseen: ”Mitä miehistön, kaluston, PMS:n, todistusten, viestinnän, sään, vetovoimamarginaalin, osoituksen ja tiedotustilaisuuden tarkastukset sanovat, ja mitä esteitä on olemassa?” Arvio ei ole päätös: ”The system never issues GO; only the appointed Master or Tug Master records a GO/NO-GO decision with a reason.”

## Kuka sitä käyttää
- **Hinaajan päällikkö / Päällikkö:** tarkastelee arviota ja esteitä ja kirjaa GO- tai NO-GO-päätöksen (13.03 §3).
- **Hinaajaoperaatioiden koordinaattori / Maaoperaatiot:** seuraavat valmiutta ja esteitä; he eivät päätä (13.03 §7).

## Ennen kuin aloitat
- Avaa sivu osoituksesta: käytä näkymässä **Osoitus ja ristiriidat** ehdotuksen alla olevaa linkkiä **Operaation valmiustarkastus**. Ilman valittua osoitusta sivu näyttää viestin ”Valitse osoitus työn osoitusluettelosta.”
- Päätöksen kirjaaminen vaatii hinaajan kirjoitusoikeuden ja kyseiselle hinaajalle palvelimelle kirjatun **Nimitys**-tiedon (hinaajan päällikkö tai päällikkö), ja osoituksen on oltava yhä tilassa Ehdotettu.
- READY-tilaa ei ole ilman tiedotustilaisuutta: **Hinausriski ja tiedotustilaisuus** on julkaistava ja jokaisen osallistujan on kuitattava se (13.03 §3).

## Vaihe vaiheelta
1. Lue tilamerkki riviltä **Arvio**. Sen vieressä oleva huomautus sanoo ”Neuvoa-antava, ei GO-päätös.”
2. Tarkista **Vaadittu vetokyky (t)**, **Varmennettu käytettävissä oleva vetokyky (t)** ja **Vetokyvyn marginaali (t)**. Puuttuva arvo näkyy merkinnällä ”Tuntematon”.
3. Tarkastele kohtien **Kriittiset esteet**, **Tarkastuksen syyt** ja **Ehdot** koodeja.
4. Näet luokkataulukosta kunkin luokan tuloksen (Miehistö, Hinauspätevyys, Kalusto, Hinausvarusteet, PMS ja viat, Todistukset, Viestintä, Sää, Vetovoimamarginaali, Osoitus, Hinausriski ja tiedotustilaisuus). Lue yksityiskohdat taulukon **Lähdetarkistukset** syykoodeista.
5. Jos sinulla on päätösvalta, valitse lomakkeessa **Kirjaa päällikön päätös** **NO-GO** tai **GO**, syötä **Nimitys** ja **Perustelu** ja paina **Kirjaa**.
6. Näet kirjatut päätökset luettelosta **Päällikön päätökset**.

## Mitä tilat tarkoittavat
- **Valmis (READY):** jokainen lähdetarkistus läpäistään (ei esteitä, tuntemattomia eikä ehtoja). Tämä on tietoa, ei päätös; GO-päätöksen kirjaa edelleen hinaajan päällikkö tai päällikkö.
- **Ehdollinen (CONDITIONAL):** kohdan ”Ehdot” ehdot ovat voimassa. GO on poissa käytöstä, ja sivu selittää: ”Tämä arvio on EHDOLLINEN. GO edellyttää arviota VALMIS: mikään lähde ei määritä, mitkä ehdot sallisivat GO:n, joten palvelin hylkää sen. Poista ehdot ja arvioi uudelleen tai kirjaa NO-GO.”
- **Ei valmis (NOT_READY):** vähintään yksi kriittinen este on olemassa.
- **Tarkastus vaaditaan (REVIEW_REQUIRED):** jotkin lähteet ovat tuntemattomia tai odottavat tarkastusta. Tuntematonta tarkastusta ei koskaan lasketa tilaksi ”Valmis”.
- **Hyväksytty / Ei läpäissyt / Tuntematon:** yksittäisen luokan tai tarkastuksen tulos.
- **GO / NO-GO:** hinaajan päällikön tai päällikön kirjaama ihmisen päätös ja peruste.

## Mitä et voi tehdä / varoitus
- GO voidaan kirjata vain READY-arviolle; palvelin hylkää GO:n tiloissa CONDITIONAL, NOT_READY tai REVIEW_REQUIRED. Sivu ottaa GO:n käyttöön vain READY-arviolla; muuten se sanoo ”GO voidaan kirjata vain arviolle VALMIS.” NO-GO voidaan aina kirjata perusteineen.
- READY-tilaa ei ole, kun riski ja tiedotustilaisuus ovat keskeneräisiä tai osallistujien kuittauksia puuttuu; GO-vaihtoehto on silloin poissa käytöstä (13.03 §3).
- Maan käyttäjän osoitusehdotus ei koskaan ohita hinaajan päällikön aluksella tekemää turvallisuuspäätöstä (13.03 §7).
- Jos arvio muuttuu ennen päätöstäsi, päätös hylätään; tarkastele nykyistä arviota uudelleen.
- Komennot ovat poissa käytöstä offline-tilassa.

## Jos jokin menee vikaan
- **Viesti ”The assessment changed”:** käytä painiketta **Lataa uudelleen**, tarkastele nykyistä arviota ja päätä uudelleen.
- **Viesti ”The outcome is uncertain”:** **Yritä uudelleen** lähettää saman komennon.
- **Odottamaton valmius:** avaa luokkataulukko ja lähdetarkistusten syyt; tarkastele niitä kontrollin omistajan tai asiantuntijan kanssa (13.03 §10).
- **Ei valtuutusta:** tarkista roolisi, hinaajasi ja nimityskontekstisi; ota tarvittaessa yhteyttä vuokralaisen ylläpitäjään (13.03 §10).

## Liittyvät sivut
