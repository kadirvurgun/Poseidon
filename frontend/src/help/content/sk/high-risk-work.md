---
key: high-risk-work
title: Vysoko rizikové práce
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Vysoko rizikové práce zobrazuje výnimky pre práce vo flotile, ako sú práce s otvoreným ohňom, uzavreté priestory, práce vo výškach, elektrické odpojenie, kritická údržba, vlečenie a bunkrovanie, v jednom zozname **read-only** (iba na čítanie). Odpovedá na otázku: „Ktorá vysoko riziková práca má otvorený problém v oblasti rizika, povolenia, kritickej bariéry, posádky alebo vybavenia a kto má ďalší krok?“ Každá dimenzia sa zobrazuje samostatne; neexistuje žiadne súhrnné skóre. Akcie otvárajú vlastný záznam; táto stránka nemení žiadny záznam a nerozhoduje o začatí práce (13.03 §4).

## Kto ju používa
- **DPA / HSEQ:** v rámci riadenia rizík sleduje výnimky vysoko rizikových prác v celej flotile (13.03 §4).
- **Kapitán a kapitán remorkéra:** vidia blokátory vysoko rizikových prác na vlastnom plavidle a prechádzajú do vlastného záznamu. Rozhodnutie o začatí alebo zastavení práce zostáva na nich (13.03 §2, §3).

## Skôr než začnete
- Zobrazenie vyžaduje oprávnenia na čítanie rizík a čítanie vysoko rizikových prác. Bez nich uvidíte „You do not have access to the high-risk work view.“
- Zoznam pokrýva iba flotily a plavidlá vo vašom rozsahu.
- V režime offline môže byť zobrazený zoznam zastaraný.

## Krok za krokom
1. Vyberte filtre **Flotila**, **Plavidlo** a **Druh práce**. **Všetko** filter odstráni.
2. V tabuľke si pri každej práci prečítajte samostatne stavy **Riziko**, **Povolenie**, **Kritická bariéra**, **Posádka** a **Vybavenie**. Odznaky **Kritické blokátory** a **Položky na posúdenie** zobrazujú počty.
3. Pomocou stĺpcov **Ďalšia akcia** a **Zodpovedná osoba / termín** zistíte, kto má čo urobiť.
4. Ak chcete vidieť, prečo je riadok v danom stave, stlačte **Prečo**. Otvorí sa tabuľka **Dimenzia**, **Stav** a **Dôvod**; zatvoríte ju pomocou **Skryť**.
5. Ak chcete niečo zmeniť, kliknite na odkaz práce alebo **Otvoriť záznam**; akcia sa vykoná na vlastnej stránke príslušného záznamu.
6. Strany prechádzajte pomocou **Predchádzajúce** a **Ďalšie**. **Vyhodnotené** a **Pokrytie zdrojov** ukazujú, kedy a z ktorých zdrojov bol zoznam vypočítaný.

## Čo znamenajú stavy
- **Pripravené / Podmienené / Vyžaduje pozornosť / Nepripravené / Blokované:** stav dimenzie v jej zdroji. Dimenzia v stave Pripravené neznamená, že prácu možno začať; rozhodnutie zostáva na oprávnenej osobe.
- **Vyžaduje posúdenie:** osoba to musí posúdiť.
- **Neznáme:** zdroj nie je dostupný; nikdy to nie je pripravené. POSEIDON zatiaľ nemá žiadny zdroj povolení na prácu, preto stĺpec **Povolenie** vždy zobrazuje **Neznáme**.
- **V poriadku / Obmedzené / Zlyhalo:** stav kritickej bariéry.
- **Neklasifikované:** druh práce nie je známy alebo jeho kritickosť nie je určená; zo zoznamu sa nevynecháva.
- **Čaká sa na zdroj / Vyriešiť zastavenie alebo spor / Obnoviť prepadnutú položku / Dokončiť povinnú položku / Prehodnotiť / Otvoriť záznam vlastníka:** ďalšia akcia, zobrazená v pevnom poradí priority.
- **Po termíne nevyriešené / Neplánované:** termín alebo plánovaný začiatok nie je známy.

## Čo nemôžete robiť / upozornenie
- Táto stránka je iba na čítanie; nič nemení, neschvaľuje ani nepovoľuje.
- Neexistuje žiadne súhrnné skóre; práca s kritickým blokátorom sa vždy zoradí navrch a to, že ostatné dimenzie sú v poriadku, ju nikdy neskryje.
- **Neznáme** nečítajte ako pripravené. Historické aktívne povolenie nikdy nie je aktuálne platným povolením.
- Rozhodnutie o začatí alebo zastavení práce zostáva na kapitánovi alebo kapitánovi remorkéra.

## Ak sa niečo pokazí
- **Služba nie je dostupná. Skúste to znova.** Stlačte **Skúsiť znova**.
- **Ste offline. Zobrazený zoznam môže byť zastaraný.** Po pripojení stlačte **Znova načítať**.
- Ak stav vyzerá neočakávane, otvorte tabuľku **Prečo** a opýtajte sa vlastníka záznamu alebo vlastníka kontroly (13.03 §10).
- Ak nemáte prístup, skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).

## Súvisiace stránky
