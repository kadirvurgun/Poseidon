---
key: tug-operations-offline
title: Offline operácie remorkérov
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka uchováva na zariadení **vopred stiahnutý snímok** zákazky remorkéra a tri druhy záznamov zachytených bez pripojenia ukladá do **trvalého, šifrovaného frontu**: potvrdenia briefingu, pozorovania pri vykonávaní a záznamy denného denníka. Odpovedá na otázku: „Čo môžem bezpečne zachytiť bez pripojenia, čo čaká vo fronte a ako to server prevzal?“ Uvoľnenie kapitánom, GO pripravenosti, začatie/prechody vykonania, overenie a kontroly zostávajú online ľudskými záznamami.

## Kto ju používa
- **Kapitán remorkéra:** na pokračovanie v kritickej práci tam, kde je pripojenie slabé, a sledovanie stavu synchronizácie (13.03 §10).
- **Posádka:** na vykonávanie potvrdení briefingu a záznamov hliadky offline a na riešenie konfliktov bez predpokladu, že ich vlastný záznam automaticky vyhrá (13.03 §6).

## Skôr než začnete
- Otvorte stránku zo zákazky remorkéra; bez zákazky stránka zobrazí „Otvorte túto obrazovku zo zákazky remorkéra, aby sa vopred stiahol jej snímok.“
- Riadok **Offline politika** musí zobrazovať nastavenú politiku ukladania (`CONFIGURED_UNVERIFIED`); bez politiky nie je vopred stiahnutie snímku ani offline zachytávanie dostupné.
- Snímok stiahnite vopred **kým ste online**. Pred zachytávaním skontrolujte, či zámok snímku nevypršal.

## Krok za krokom
1. Kým ste online, stlačte **Vopred stiahnuť snímok zákazky**. Zobrazí sa správa „Snímok zapečatený na tomto zariadení.“
2. V **Snímok zákazky** skontrolujte číslo zákazky, jej stav a **Požičané do**.
3. Ak ste účastníkom vydaného briefingu, zvoľte v **Potvrdiť briefing** **Potvrdiť** alebo **Spochybniť** (s dôvodom aspoň 10 znakov) a stlačte **Zapečatiť a zaradiť do frontu**.
4. Pre prebiehajúce vykonanie zvoľte v **Zaznamenať pozorovanie pri vykonávaní** vykonanie a typ pozorovania (Poznámka, Vlečné lano pripojené, Vlečné lano uvoľnené), napíšte poznámku a stlačte **Zapečatiť a zaradiť do frontu**.
5. Pre koncept alebo vrátený denný denník zvoľte denník v **Záznam denného denníka**, napíšte **Súhrn dňa** a stlačte **Zapečatiť a zaradiť do frontu**.
6. Stav každého záznamu sledujte v **Zachytené záznamy vo fronte**. Záznamy sa po obnovení pripojenia odosielajú v poradí; na ručné odoslanie použite **Odoslať zachytené záznamy z fronty**.
7. Ak záznam zobrazuje **Podstatný konflikt**, prečítajte si **Zmenené na serveri**, výslovne zvoľte **Ponechať záznam servera** alebo **Použiť mnou zachytenú hodnotu na aktuálny záznam** a stlačte **Vyriešiť konflikt**.

## Čo znamenajú stavy
- **Vo fronte na zariadení (neodoslané):** záznam je zapečatený na tomto zariadení; zaradené do frontu neznamená odoslané.
- **Prijaté serverom:** server záznam prijal. Nie je to schválenie ani overenie.
- **Podstatný konflikt:** záznam na serveri sa po zachytení zmenil; nikdy sa nezlučuje automaticky a čaká na rozhodnutie človeka.
- **Hodnota servera ponechaná / Odmietnuté serverom:** výsledok servera pre daný záznam.
- **UNKNOWN (offline):** stav politiky nebolo možné offline prečítať; nepredpokladá sa žiadna politika.

## Čo nemôžete robiť / upozornenie
- Iba online: uvoľnenie kapitánom, GO pripravenosti, začatie a prechody vykonania, vydanie briefingu, overenie, odoslanie a kontrola denného denníka.
- Podstatné konflikty sa nikdy nezlučujú automaticky; riešenie je vždy výslovná ľudská voľba (13.03 §1).
- So zámkom snímku, ktorý vypršal, nie je možné vytvoriť nový záznam; stiahnite vopred nový snímok.
- Pri opätovnom prehratí sa každý záznam spracuje raz s pôvodným ID; nevytvárajte ten istý záznam znova.

## Ak sa niečo pokazí
- **Správa „The device key for this queue is unavailable.“:** zachytené záznamy sa uchovávajú, ale nie je možné ich prečítať; nemažte ich a kontaktujte podporu.
- **Správa „Outcome unknown“:** ten istý záznam sa skúsi znova s pôvodným ID; nevytvárajte nový.
- **Správa „The offline policy does not allow a snapshot.“:** kontaktujte administrátora nájomcu ohľadom nastavenia politiky (13.03 §10).
- **Podstatný konflikt:** použite formulár **Vyriešiť konflikt** na tejto stránke; konflikty sú uvedené aj v **Centrum podstatných konfliktov** (13.03 §10).

## Súvisiace stránky
