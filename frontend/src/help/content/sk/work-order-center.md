---
key: work-order-center
title: Pracovné príkazy
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## Na čo slúži táto stránka
Centrum pracovných príkazov uchováva **koncepty pracovných príkazov a deklarované záznamy o vykonaní** zvolenej lode. Odpovedá na otázku: „Ktoré pracovné príkazy na tejto lodi existujú a ktoré boli vydané, vykonané, mali odoslané dôkazy a boli overené osobou?“ Každá uložená revízia sa uchováva v nemennej histórii. Overenie a uzavretie nie sú návratom do prevádzky, povolením ani schválením rizika.

## Kto ju používa
- **Hlavný strojník / technický inšpektor:** na otváranie, vydávanie, overovanie a uzatváranie plánovaných a neplánovaných pracovných príkazov (13.03 §5).
- **Strojník / posádka:** na začatie pridelenej práce a zaznamenanie a odoslanie odpočtov, náhradných dielov, fotografií a dokumentov ako dôkazov (13.03 §6).

## Skôr než začnete
- Čítanie vyžaduje `workorder.read`, `equipment.read` a `maintenance.read`. Koncepty vyžadujú `workorder.write`, vydanie `workorder.release`, vykonanie `workorder.execute`, overenie `workorder.verify` a uzavretie `workorder.close`.
- Vydanie, overenie a uzavretie vyžadujú prihláseného ľudského používateľa.
- Zvoľte správnu loď z **Vybrať plavidlo alebo remorkér**.

## Krok za krokom
1. Zvoľte loď. V **Zaznamenané pracovné príkazy** filtrujte túto stranu pomocou **Vyhľadať vrátené čísla pracovných príkazov** a otvorte záznam.
2. Pre novú prácu stlačte **Nový koncept pracovného príkazu**, vyplňte polia a stlačte **Uložiť neposúdený koncept**. Neúplné koncepty je možné uložiť.
3. Ak máte oprávnenie na vydanie, v **Vykonanie a dôkazy** zvoľte **Určiť povinné dôkazy** v časti **Politika povinných dôkazov**, zaškrtnite druhy dôkazov a stlačte **Vydať pracovný príkaz**. Ak vydáte bez určenia, overenie zostáva blokované, kým vydávajúci nepoužije **Zmeniť povinné dôkazy** → **Uložiť povinné dôkazy**.
4. Vykonávateľ stlačí **Začať prácu**, vyplní **Odpočty**, **Spotrebované náhradné diely**, **Fotografie a dokumenty ako dôkazy**, **Vykonaná práca** a **Zistenia**, stlačí **Uložiť dôkazy** a potom **Odoslať dôkazy**.
5. Oprávnený overovateľ, ktorý prácu nevykonal, posúdi dôkazy. Stlačí **Zaznamenať overenie**, alebo zvolí **Dôvod vrátenia** a stlačí **Vrátiť vykonávateľovi**.
6. Pri overenom pracovnom príkaze stlačí oprávnená osoba **Zaznamenať uzavretie**.
7. Skoršie revízie uvidíte v **Nemenná história pracovného príkazu**. Ak je potrebný záznam o odstávke, použite **Otvoriť prepojený koncept odstávky**.

## Čo znamenajú stavy
- **Nevydané na vykonanie. / Vydané / Prebieha:** práca ešte nie je vydaná, je vydaná alebo sa vykonáva.
- **Dôkazy odoslané:** vykonávateľ deklaroval prácu a dôkazy. Neznamená to overené.
- **Overené (záznam človeka):** zaznamenané oprávneným overovateľom, ktorý prácu nevykonal. **Uzavreté (záznam človeka)** je samostatný krok.
- **Povinné dôkazy chýbajú / Prítomné, čaká na overenie človekom / Overené človekom:** stav brány povinných dôkazov.
- **Nutná kontrola: politika nie je nastavená:** neboli určené žiadne povinné dôkazy. Nenastavené nikdy neznamená, že nič nie je povinné; overenie zostáva blokované.
- **Vrátené overovateľom:** práca sa vrátila vykonávateľovi so zobrazeným dôvodom.

## Čo nemôžete robiť / upozornenie
- Ak ste prácu vykonali, nemôžete ju overiť ani uzavrieť. Stránka uvádza „Túto prácu ste vykonali, preto ju nemôžete overiť ani uzavrieť.“
- Dôkazy odoslané, Overené a Uzavreté sú samostatné ľudské záznamy. Stránka nikdy nevyhlasuje zariadenie za pripravené do prevádzky.
- Súhrn „Saved work order source“ opisuje uložené polia konceptu a uvádza: „Vydanie, zaznamenanie dôkazov, nezávislé overenie a uzavretie sa zaznamenávajú v paneli vykonania. Odoslané dôkazy prijíma iba nezávislé ľudské overenie, nikdy osoba, ktorá prácu vykonala; neexistuje samostatný krok prijatia dôkazov a overenie nie je rozhodnutím o návrate do prevádzky.“ Tieto akcie sa vykonávajú v **Vykonanie a dôkazy**.
- Odpočty sa uchovávajú tak, ako boli deklarované; nepoužíva sa žiadny prevod jednotiek ani prah.
- Offline front dôkazov funguje iba vtedy, keď existuje schválená offline politika a kým práca prebieha. Konflikty sa nikdy nezlučujú automaticky; použite **Otvoriť centrum konfliktov** a rozhodne človek.

## Ak sa niečo pokazí
- **Záznam sa zmenil alebo táto akcia nie je v jeho aktuálnom stave povolená:** pomocou **Znova načítať** načítajte aktuálny záznam a skúste to znova.
- **Výsledok neznámy:** **Zopakovať ten istý príkaz** odošle ten istý príkaz; nevytvára nový záznam.
- **Chýba oprávnenie alebo ľudská právomoc:** skontrolujte svoju rolu a kontext lode; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).
- **Brána povinných dôkazov túto akciu odmietla:** doplňte chýbajúce druhy dôkazov. Ak záznam nie je možné overiť, skontrolujte dôkazy a ich pôvod s overovateľom (13.03 §10).

## Súvisiace stránky
