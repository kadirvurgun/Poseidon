---
key: fuel-bunker-log
title: Palivo, bunkrovanie a zostatky
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka paliva, bunkrovania a zostatkov (ROB) uchováva **vyhlásenia o palive a bunkrovaní** vybranej lode ako neoverené koncepty s ich históriou zdrojov. Odpovedá na otázku: „Aké palivo bolo prijaté alebo spotrebované, aký ROB sme deklarovali pred a po, ktoré referencie BDN, dodávateľa, vzorky a nádrží k nemu patria a nadväzuje deklarovaný reťazec ROB z jedného záznamu na ďalší?“ Stránka neopravuje množstvá, nepočíta emisie a nepreukazuje súlad ani overenie.

## Kto ju používa
- **Hlavný strojník / strojní dôstojníci:** zaznamenávajú vyhlásenia o bunkrovaní, spotrebe a ROB s referenciami na dôkazy (13.03 §5).
- **Kapitán:** sleduje vyhlásenia o palive popri poludňajších súhrnoch posúdených kapitánom (13.03 §2).

## Skôr než začnete
- V hornom kontextovom paneli vyberte **správnu spoločnosť a loď**. Ak nie je vybraná žiadna loď, stránka zobrazí „Vybrať loď“.
- Čítanie vyžaduje `operations.fuel.read`. Vytváranie a ukladanie vyžaduje `operations.fuel.write` a práva na úpravy na tejto lodi.
- Časy musia obsahovať dátum, sekundy a Z alebo výslovný posun UTC; miestny čas bez časového pásma sa neprijíma.

## Krok za krokom
1. Vyberte loď. **Záznamy paliva a zostatkov** uvádza vrátené záznamy. Pomocou **Filtrovať vrátenú stranu** zúžite iba túto stranu.
2. Pod zoznamom si prečítajte **Kontrola kontinuity ROB**. Zobrazuje bilanciu (pred + prijaté − spotrebované − po), **Kontinuita s predchádzajúcim záznamom** a **Príznaky na kontrolu** pre každý deklarovaný druh paliva.
3. Ako kontext si prečítajte **Súhrny paliva a ROB z Noon skontrolované kapitánom**. Zobrazujú sa ako deklarovaný text a nekopírujú sa do knihy záznamov.
4. Stlačte **Nový koncept záznamu**. Vyplňte **Kontext záznamu a dodávky**, **Vyhlásenia množstiev a zostatkov** a **Vlastnosti paliva a záznam vzorky**. Pri každom poli ponechajte deklarovanú hodnotu, **Chýba** alebo **Neznáme**.
5. Stlačte **Uložiť neoverený koncept**. Každé uloženie pridá revíziu do **Zaznamenaná história revízií**.
6. Ak chcete začať z uloženej poludňajšej správy alebo strojného denníka, použite na tej stránke **Otvoriť palivový záznam z uloženého zdroja**. **Prepojené denné vyhlásenie** tu zostáva iba na čítanie.

## Čo znamenajú stavy
- **Vyhlásenie konceptu / Neoverený koncept záznamu:** hodnoty deklarovala osoba; nič nie je overené.
- **Vyžaduje posúdenie:** stav posúdenia súladu každého záznamu; súlad nebol posúdený.
- **Čakajúce; overenie nepreukázané:** stav posúdenia overenia; žiadne overenie neexistuje.
- **Nezistený rozdiel:** deklarované čísla sedia. Nie je to overenie.
- **Neznáme:** kontinuitu nemožno posúdiť, napríklad keď hodnota chýba alebo staršia história je mimo posudzovaného obdobia. Neznáme neznamená „Nezistený rozdiel“ ani „Pripravené“.
- **Chýba / Neznáme:** odlišné od nuly.

## Čo nemôžete robiť / upozornenie
- Ľudské posúdenie, prevádzkové alebo právne schválenie, finalizácia a offline zaznamenávanie nie sú na tejto stránke dostupné.
- Systém označuje rozdiely na ľudské posúdenie; nikdy neopravuje množstvo ani nerozhoduje o súlade paliva.
- Posudzuje sa iba najnovších 100 záznamov. Bez referencie nádrže je medzera na plavidle s viacerými nádržami otázkou chýbajúcich údajov o nádrži, nie chybou údajov.
- Množstvá z poludňajšej správy alebo strojného denníka s nevyriešenou jednotkou sa nikdy neprevádzajú na prijaté, spotrebované ani ROB hodnoty.

## Ak sa niečo pokazí
- **„The saved source changed.“:** vaše vstupy zostanú zachované; pred ďalším uložením výslovne znova načítajte alebo zahoďte zmeny.
- **„Outcome unknown.“:** použite **Zopakovať presný prevádzkový príkaz**; nevypĺňajte znova.
- **„Check the declared values, explicit time offsets and current source permission.“:** opravte označené pole alebo ho ponechajte ako **Chýba** alebo **Neznáme**.
- **Prevádzkový zdroj alebo prístup nie je dostupný:** použite **Zopakovať načítanie zdroja**; ak prístup zostáva odmietnutý, skontrolujte svoju rolu a kontext lode so správcom nájomcu (13.03 §10).

## Súvisiace stránky
