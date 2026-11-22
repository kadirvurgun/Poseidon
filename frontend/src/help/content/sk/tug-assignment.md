---
key: tug-assignment
title: Pridelenie remorkéra
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka zobrazuje **návrhy pridelenia remorkérov** pre odoslanú zákazku remorkéra a ich **konflikty** s inými zákazkami. Odpovedá na otázku: „Ktorý remorkér bol pre túto zákazku navrhnutý, s akou rolou a časovým oknom, je v konflikte s inou prácou a prijal kapitán remorkéra návrh?“ Pobrežie navrhuje, kapitán remorkéra potvrdzuje; stránka neprijíma žiadne rozhodnutie o vyslaní ani uvoľnení. Upozornenie v hornej časti to uvádza: „Systém nikdy nevydáva GO.“

## Kto ju používa
- **Koordinátor prevádzky remorkérov / pobrežná prevádzka:** na vytvorenie návrhu pridelenia po posúdení konfliktov a pripravenosti na tabuli zákaziek (13.03 §7).
- **Kapitán remorkéra:** na potvrdenie návrhu pre vlastný remorkér v kontexte a jeho prijatie alebo zamietnutie (13.03 §3).

## Skôr než začnete
- Otvorte remorkér z **Prevádzka remorkérov**; ak nie je zvolený žiadny remorkér, stránka vás tam nasmeruje.
- Navrhovanie vyžaduje prístup na zápis remorkérov a zápis plánu; zákazka musí byť **Odoslané**.
- Odpoveď vyžaduje **Vymenovanie** zaznamenané na serveri (kapitán remorkéra alebo kapitán) pre daný remorkér. Osoba, ktorá návrh vytvorila, naň nemôže odpovedať.

## Krok za krokom
1. Otvorte číslo zákazky z **Vyberte odoslanú zákazku remorkéra**. Túto stránku otvára aj odkaz **Pridelenia a pripravenosť** v detaile zákazky.
2. Pri existujúcich návrhoch si prečítajte **Stav**, **Potvrdenie kapitána remorkéra**, **Rola**, **Plánovaný začiatok** a **Plánovaný koniec**.
3. V **Konflikty** uvidíte časové a zdrojové konflikty s inými zákazkami, ich odznak výsledku a kód dôvodu.
4. Pre nový návrh zvoľte vo formulári **Navrhnúť pridelenie** položky **Remorkér**, **Plánovaný začiatok**, **Plánovaný koniec** a **Rola** (Vedúci, Podporný, Sprievod) a stlačte **Navrhnúť pridelenie**.
5. Ako kapitán remorkéra zvoľte svoje **Vymenovanie**, v prípade potreby pridajte **Dôvod** a stlačte **Prijať** alebo **Zamietnuť**. Na zamietnutie je dôvod povinný.
6. Ak chcete návrh odstrániť, zadajte dôvod a stlačte **Stiahnuť návrh**.
7. Pomocou odkazov **Kontrola prevádzkovej pripravenosti** a **Posádka a vlečná spôsobilosť** pod každým návrhom prejdete na obrazovky pripravenosti.

## Čo znamenajú stavy
- **Navrhnuté / Stiahnuté:** životný cyklus návrhu. Navrhnuté neznamená, že remorkér bol vyslaný.
- **Čaká / Prijaté / Zamietnuté:** odpoveď kapitána remorkéra na návrh. Prijaté potvrdzuje rozsah návrhu; nie je to rozhodnutie GO.
- **Vyhovuje / Nevyhovuje / Neznáme:** výsledok kontroly konfliktov. Neznáme ukazuje, že pre kontrolu nebolo dosť zdrojových údajov; nikdy neznamená „žiadny konflikt“.
- **Žiadny konflikt nezistený; vstupy môžu byť neúplné.:** nebol pozorovaný žiadny konflikt, ale nie je to úplný výsledok spôsobilosti.
- **Požiadavka sa po tomto návrhu zmenila; je potrebný nový návrh.:** požiadavka zákazky sa zmenila po revízii, z ktorej návrh vychádzal.

## Čo nemôžete robiť / upozornenie
- Pobrežný návrh pridelenia nikdy neprevažuje nad bezpečnostným rozhodnutím kapitána remorkéra na palube (13.03 §3, §7).
- Navrhovateľ nemôže prijať vlastný návrh; odpoveď patrí iba osobe menovanej pre daný remorkér.
- Pre koncept ani zrušenú zákazku nie je možné vytvoriť návrh.
- Offline sú príkazy vypnuté.

## Ak sa niečo pokazí
- **Správa „The outcome is uncertain“:** **Skúsiť znova** odošle ten istý príkaz; nevytvárajte nový návrh.
- **Správa „The record changed“:** pomocou **Znova načítať** skontrolujte aktuálny záznam a potom konajte znova.
- **„Na túto akciu nemáte oprávnenie.“ / „K tomuto záznamu nemáte prístup.“:** skontrolujte svoju rolu, remorkér a kontext menovania; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).
- **Neočakávaný konflikt:** poznačte si kód dôvodu a posúďte ho so zodpovednou osobou druhej zákazky.

## Súvisiace stránky
