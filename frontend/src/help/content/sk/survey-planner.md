---
key: survey-planner
title: Plánovač prehliadok
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Plánovač prehliadok zobrazuje deklarované okná prehliadok v kalendári flotily spolu s vhodnosťou plavby a prístavu. Odpovedá na otázku: „Ktoré certifikáty na ktorých lodiach majú v zvolenom rozsahu otvárajúce sa alebo uplynuté okno prehliadky a existuje kontrolný zoznam prípravy?“ Je to iba pomôcka pri plánovaní: nie harmonogram orgánu, rezervácia ani výsledok prehliadky (13.03 §8).

## Kto ju používa
- **Kapitán, hlavný strojník / technický inšpektor:** vidia nadchádzajúce prehliadky a včas začnú kontrolný zoznam prípravy (13.03 §5).
- **Používateľ Regulatory / Assurance:** sleduje deklarované okná a chýbajúce deklarácie v celej flotile (13.03 §8).

## Skôr než začnete
- Zobrazenie vyžaduje čítanie certifikátov. Uvádzajú sa iba lode, ku ktorým máte prístup.
- Okná pochádzajú z deklarácií v zázname certifikátu. Ak nie je deklarované žiadne okno, riadok sa zobrazí v časti **Okno nedeklarované**; dátumy zo zdrojového dokumentu zadajte v [Certificate command center](/app/certificate-command-center).
- Stránka vyžaduje živé čítanie. Offline uvidíte „Offline: vyžaduje sa aktuálne online čítanie.“

## Krok za krokom
1. V **Rozsah kalendára** zvoľte **Od** a **Do** a stlačte **Zobraziť**.
2. V súhrnnom riadku si prečítajte počty otvorených, nadchádzajúcich, po termíne a neznámych.
3. Na každom riadku skontrolujte **Certifikát**, **Vydavateľ**, **Deklarovaná vlajka**, **Právny základ**, **Deklarovaná platnosť do**, **Okno prehliadky** a **Vhodnosť plavby/prístavu**.
4. Stlačte **Otvoriť certifikát**, aby ste videli certifikát.
5. Ak zoznam prípravy existuje, použite **Otvoriť kontrolný zoznam**, inak **Začať kontrolný zoznam**, a prejdite na [Pre-survey checklist](/app/pre-survey-checklist).

## Čo znamenajú stavy
- **Nadchádzajúce / Okno otvorené:** poloha vzhľadom na deklarované okno.
- **Okno uplynulo, splnenie nezaznamenané:** okno skončilo a žiadny záznam neukazuje, že prehliadka bola vykonaná. Nedokazuje to však ani to, že prehliadka bola zmeškaná; skontrolujte záznamy.
- **Okno neznáme:** dátum chýba alebo je nečitateľný. Neznáme okno nikdy neznamená „žiadny problém“; vyžaduje posúdenie.
- Vhodnosť plavby/prístavu: **Podmienené**, **Nevhodné**, **Neznáme**. Stránka nikdy nedáva schválenie „vhodné“.
- Pripravenosť kontrolného zoznamu: **Blokované**, **Neúplné**, **Pripravené, čaká na ľudské potvrdenie**.

## Čo nemôžete robiť / upozornenie
- Plánovač nič nerezervuje a nič nepodáva na orgán. Koordinácia s klasifikačnou spoločnosťou, vlajkou a orgánmi je externá a nie je tu simulovaná.
- To, čo zobrazuje kalendár, nie je výsledok prehliadky ani platnosť certifikátu. Rozhodnutia o plavbe a prehliadke patria oprávneným osobám.
- Stránka je iba na čítanie; zmeny sa vykonávajú na stránkach certifikátov a kontrolných zoznamov.

## Ak sa niečo pokazí
- **Zobrazená je len časť flotily; zúžte rozsah.** Zvoľte kratší rozsah kalendára.
- **V tomto rozsahu nie sú certifikáty s oknami prehliadok.** Rozšírte rozsah alebo skontrolujte deklarácie certifikátov.
- **Načítanie zlyhalo. Skúste to znova.** Stlačte **Skúsiť znova**. Ak problém pretrváva, nahláste ho podpore s obrazovkou, časom a krokmi (13.03 §10).
- **K tomuto zobrazeniu nemáte prístup.** Skontrolujte svoju rolu a rozsah lodí; v prípade potreby kontaktujte Tenant Admin (13.03 §10).

## Súvisiace stránky
