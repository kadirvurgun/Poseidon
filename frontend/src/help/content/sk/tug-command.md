---
key: tug-command
title: Riadenie remorkéra
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka, zobrazená v menu ako **Riadenie remorkéra**, zobrazuje **zdrojový kontext** zvoleného remorkéra. Odpovedá na otázku: „Aké vyhlásenia o role, type, ťažnej sile na väzáku (BP) a vybavení sú pre tento remorkér zaznamenané, odkiaľ pochádzajú a nakoľko sú aktuálne?“ Stránka zobrazuje iba vyhlásenia a ich zdroje; neposkytuje žiadnu prevádzkovú pripravenosť, spôsobilosť, povolenie ani bezpečnostné schválenie. Odznak v hornej časti to uvádza: „Zdrojové vyhlásenia · prevádzková pripravenosť neznáma“.

## Kto ju používa
- **Kapitán remorkéra:** na potvrdenie kontextu roly, BP a vybavenia vlastného remorkéra pred začatím práce (13.03 §3).
- **Koordinátor prevádzky remorkérov / pobrežná prevádzka:** na overenie, že sú v správnom kontexte remorkéra pred prechodom na prehľad prác a príjem práce (13.03 §7).
- **Posádka:** na zobrazenie svojho pohľadu roly a obrazoviek režimu lode, ktoré má otvorené (13.03 §6).

## Skôr než začnete
- Potrebujete režim remorkéra a prístup na čítanie remorkérov. Bez neho stránka zobrazí „Prístup nie je dostupný.“
- Ak nie je zvolený žiadny kontext, stránka uvedie oprávnené remorkéry v časti **Vybrať remorkér**; jeden otvorte.
- Zoznam vybavenia vyžaduje aj prístup na čítanie vybavenia; bez neho sa zdroj vybavenia zobrazí ako „nedostupné alebo neudelené“.

## Krok za krokom
1. Otvorte remorkér zo zoznamu **Vybrať remorkér**. Riadok **Kontext remorkéra** pod nadpisom zobrazuje kód, názov a režim remorkéra.
2. V **Rola (pridelenie serverom)** a **Pohľad menu režimu remorkéra** si prečítajte rolu a pohľad menu, ktoré vám pridelil server.
3. V **Viditeľnosť režimu** uvidíte, či sú obrazovky režimu lode otvorené. Sekcia **Obrazovky režimu lode** odkazuje iba na obrazovky, ktoré vám boli udelené.
4. Z **Typ remorkéra**, **Zdroj / revízia typu** a **Pozorovanie zdroja typu / pravidlá** skontrolujte, z ktorého zdroja a revízie typ pochádza.
5. Prečítajte si **BP profilu (neoverená)**, **Zdroj / verzia / pôvod BP** a **Aktuálnosť / pozorovanie BP**. Je to deklarovaná hodnota, nie nameraná.
6. V **Kontext vybavenia** uvidíte vyhlásenia o vybavení, ich úrovne, revízie a pôvod.
7. Pomocou kariet stránky prejdite na **Prehľad prác** alebo (s prístupom na zápis) **Príjem práce**, alebo (s prístupom na zápis) stlačte **Nová požiadavka**.

## Čo znamenajú stavy
- **Neznáme — žiadne pridelenie roly:** server pre vás na tomto remorkéri nemá žiadne pridelenie roly. Neznamená to „žiadna rola, žiadny problém“; je potrebné pridelenie administrátorom.
- **Popisy rolí nie sú k dispozícii / Pohľad menu nie je k dispozícii:** zdroj teraz nie je možné prečítať. Chýbajúca informácia sa nepovažuje za prázdnu.
- **Pre režim remorkéra nie je nastavený pohľad:** pre vás v tomto režime nie je definovaný žiadny pohľad menu.
- **V režime remorkéra skryté. Vyžaduje sa medzirežimová rola.:** obrazovky režimu lode vyžadujú výslovne oprávnenú medzirežimovú rolu.
- **BP profilu (neoverená):** deklarovaná ťažná sila na väzáku. Nie je to overený výsledok skúšky.
- **Nevrátili sa vyhlásenia vybavenia. Overená prevádzková spôsobilosť zostáva neznáma.:** prázdny zoznam nikdy neznamená, že vybavenie je spôsobilé.
- **Neznáme / Nedostupné:** zdrojová hodnota chýba alebo ju nebolo možné prečítať. Neznáme nikdy nezastupuje „Pripravené“ ani „Nie“.

## Čo nemôžete robiť / upozornenie
- Na tejto stránke sa neupravuje žiadne pole a nevytvára sa žiadny výsledok pripravenosti. Prevádzkové rozhodnutie a bezpečnostná právomoc zostávajú u kapitána remorkéra na palube (13.03 §3).
- Pobrežný návrh pridelenia nikdy neprevažuje nad bezpečnostným rozhodnutím kapitána remorkéra na palube (13.03 §7).
- Aj s deklarovaným medzirežimovým oprávnením vyžaduje každá obrazovka režimu lode vlastné serverové oprávnenie.
- Offline sa aktuálne zdroje profilu nezobrazujú; znova sa pripojte, aby ste opäť skontrolovali prístup a verzie zdrojov.

## Ak sa niečo pokazí
- **Prístup nie je dostupný:** skontrolujte svoju rolu, remorkér a kontext objektu; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).
- **Upozornenie „Current identity and vessel access must be checked“:** stlačte **Znova skontrolovať prístup**.
- **Údaje nedostupné:** skontrolujte pripojenie a stlačte **Skúsiť znova**.
- **Neočakávané hodnoty BP alebo vybavenia:** poznačte si polia zdroja a verzie a posúďte ich so zodpovednou osobou zdroja; ak záznam nie je možné overiť, skontrolujte jeho dôkazy a pôvod (13.03 §10).

## Súvisiace stránky
