---
key: today
title: Dnes
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## Na čo slúži táto stránka
„Dnes“ zhromažďuje pre zvolenú loď alebo remorkér **upozornenia, ktoré teraz potrebujú vašu pozornosť**, v jednom zozname. Odpovedá na otázku: „Ktorý záznam tohto plavidla dnes čaká na rozhodnutie alebo kontrolu a ktorý zdroj to uvádza?“ Stránka nič nerozhoduje; zobrazuje upozornenie, jeho zdroj a ďalší krok.

## Kto ju používa
- **Kapitán** a **kapitán remorkéra:** na zobrazenie výnimiek dňa, ich zodpovednej osoby a ďalšieho kroku (13.03 §2, §3).
- **Hlavný strojník:** na sledovanie upozornení z plánu údržby (13.03 §5).
- **Crew / OOW:** na zobrazenie práce, ktorá im pripadá (13.03 §6).

## Skôr než začnete
- V hornej kontextovej lište skontrolujte, či sú zvolené **správna spoločnosť a plavidlo** (13.03 §1). Zobrazené upozornenie patrí k zvolenému plavidlu.
- Na otvorenie tejto stránky potrebujete prístup na čítanie pracovného priestoru.
- Ak nie je zvolené žiadne plavidlo, stránka najprv zobrazí zoznam **Vybrať plavidlo**.

## Krok za krokom
1. Zvoľte loď alebo remorkér, na ktorom pracujete. Zvolené plavidlo sa zobrazuje ako **Aktuálne plavidlo**; zmeníte ho pomocou **Zmeniť plavidlo**.
2. Pozrite si sekciu **Pokrytie zdrojov** v hornej časti. Ukazuje, ktoré zdroje (regulačné, plánovaná údržba, prevádzka remorkérov) bolo možné prečítať.
3. Prečítajte si karty upozornení zhora nadol. Každá karta pochádza z jedného zdroja a navrhuje ďalší krok (napríklad **Preskúmať koncept**, **Preskúmať plán údržby**, **Zobraziť zákazku remorkéra**).
4. Stlačte **Prečo?**, aby ste videli dôvod karty. Sekcia zobrazuje pravidlo, zdrojový záznam, jeho revíziu, čas načítania a všetky neznáme vstupy.
5. Prejdite cez odkaz karty na vlastnú stránku záznamu a konajte tam v rámci svojich oprávnení.
6. Pomocou **Obnoviť** údaje načítate znova.

## Čo znamenajú stavy
- **Dostupné (neoverené):** zdroj bol prečítaný. Neznamená to, že jeho obsah bol overený.
- **Chýba / Nedostupné v tomto kontexte:** zdroj nebolo možné pre toto plavidlo prečítať. Upozornenie, ktoré nevidíte, neznamená, že je všetko v poriadku.
- **Partial view:** niektoré zdroje nebolo možné prečítať. Počet nula neznamená „žiadny problém“.
- **Every listed source was read and returned no items:** nevrátilo sa žiadne upozornenie. Nie je to bezpečnostné uvoľnenie.
- **Neznáme:** hodnota v zdroji chýba alebo ju nebolo možné určiť. Neznáme neznamená „Nie“, „Pripravené“ ani „Neuplatňuje sa“.
- **Vyžaduje posúdenie / Upozornenie plánovania / Poradné:** priorita karty. Karta s „Vyžaduje posúdenie“ čaká, kým sa na ňu pozrie oprávnená osoba.
- **Dokončené ≠ Overené:** dokončenie záznamu neznamená, že ho oprávnený overovateľ potvrdil (13.03 §1).

## Čo nemôžete robiť / upozornenie
- Táto stránka neprijíma rozhodnutie o odchode, prevádzke ani bezpečnosti. Toto rozhodnutie patrí oprávnenému kapitánovi alebo kapitánovi remorkéra.
- Z tejto stránky sa nespúšťa žiadny príkaz; akcie prebiehajú na vlastnej stránke záznamu.
- Údaje zobrazené offline sú staré a neoverené. Podrobnosti **Prečo?** sa offline neotvárajú.
- Čas načítania alebo uloženia nedokazuje, že zdroj je správny.

## Ak sa niečo pokazí
- **Offline:** pokračujte v kritickej práci a sledujte stav synchronizácie. Ak sa objaví konflikt, prejdite do Centra konfliktov (13.03 §10).
- **Prístup zamietnutý / Plavidlo nie je dostupné vo vašom rozsahu:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby kontaktujte administrátora nájomcu.
- **Unexpected alert:** otvorte **Prečo?**; ak pravidlo alebo zdroj pôsobí nesprávne, odovzdajte to zodpovednej osobe kontroly alebo odborníkovi.
- **Zdroj nedostupný / Too many requests:** chvíľu počkajte a použite **Skúsiť znova**.
- **The server response failed validation:** nič z nej sa nezobrazí; nahláste to podpore so snímkou obrazovky, časom a vašimi krokmi.

## Súvisiace stránky
