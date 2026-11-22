---
key: high-risk-work
title: Vysoce rizikové práce
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Vysoce rizikové práce zobrazuje výjimky pro práce ve flotile, jako jsou práce s otevřeným ohněm, práce v uzavřených prostorech, práce ve výškách, elektrické odpojení, kritická údržba, vlečení a bunkrování, v jednom seznamu **pouze pro čtení**. Odpovídá na otázku: „Která vysoce riziková práce má otevřený problém v oblasti rizika, povolení, kritické bariéry, posádky nebo vybavení a kdo má další akci?“ Každá dimenze se zobrazuje zvlášť; neexistuje žádné souhrnné skóre. Akce otevírají vlastní záznam; tato stránka nemění žádný záznam a nerozhoduje o zahájení práce (13.03 §4).

## Kdo ji používá
- **DPA / HSEQ:** v rámci řízení rizik sleduje výjimky vysoce rizikových prací v celé flotile (13.03 §4).
- **Kapitán a kapitán remorkéru:** vidí překážky vysoce rizikových prací na svém vlastním plavidle a přecházejí k vlastnímu záznamu. Rozhodnutí práci zahájit nebo zastavit zůstává na nich (13.03 §2, §3).

## Než začnete
- Zobrazení vyžaduje oprávnění ke čtení rizik a ke čtení vysoce rizikových prací. Bez nich uvidíte „K přehledu vysoce rizikových prací nemáte přístup.“
- Seznam pokrývá pouze flotily a plavidla ve vašem rozsahu.
- V režimu offline může být zobrazený seznam zastaralý.

## Krok za krokem
1. Zvolte filtry **Flotila**, **Plavidlo** a **Druh práce**. **Vše** filtr odstraní.
2. V tabulce si u každé práce přečtěte zvlášť stavy **Riziko**, **Povolení**, **Kritická bariéra**, **Posádka** a **Vybavení**. Odznaky **Kritické blokátory** a **Položky k posouzení** ukazují počty.
3. Pomocí sloupců **Další akce** a **Odpovědná osoba / termín** zjistíte, kdo má co udělat.
4. Chcete-li vidět, proč je řádek v daném stavu, stiskněte **Proč**. Otevře se tabulka **Dimenze**, **Stav** a **Důvod**; zavřete ji pomocí **Skrýt**.
5. Chcete-li cokoli změnit, použijte odkaz na práci nebo **Otevřít záznam**; akce se provádí na vlastní stránce příslušného záznamu.
6. Stránkujte pomocí **Předchozí** a **Další**. **Vyhodnoceno** a **Pokrytí zdrojů** ukazují, kdy a z jakých zdrojů byl seznam vypočten.

## Co znamenají stavy
- **Připraveno / Podmíněné / Pozor / Nepřipraveno / Blokováno:** stav dimenze v jejím zdroji. Dimenze ve stavu Připraveno neznamená, že práce smí začít; rozhodnutí zůstává na oprávněné osobě.
- **Vyžaduje posouzení:** musí to posoudit osoba.
- **Neznámé:** zdroj není dostupný; nikdy to není připraveno. POSEIDON zatím nemá zdroj povolení k práci, proto sloupec **Povolení** vždy ukazuje **Neznámé**.
- **V pořádku / Oslabená / Selhala:** stav kritické bariéry.
- **Neklasifikováno:** druh práce není znám nebo jeho kritičnost není určena; ze seznamu se nevynechává.
- **Čeká se na zdroj / Vyřešit zastavení nebo spor / Obnovit prošlou položku / Dokončit povinnou položku / Přehodnotit / Otevřít záznam vlastníka:** další akce, zobrazená v pevném pořadí priority.
- **Po termínu nevyřešeno / Neplánováno:** termín nebo plánovaný začátek není znám.

## Co nemůžete dělat / upozornění
- Tato stránka je pouze pro čtení; nic nemění, neschvaluje ani nepovoluje.
- Neexistuje žádné souhrnné skóre; práce s kritickým blokátorem se vždy řadí nahoru a to, že ostatní dimenze jsou v pořádku, ji nikdy neskryje.
- Nečtěte **Neznámé** jako připraveno. Historické aktivní povolení nikdy není aktuálním platným povolením.
- Rozhodnutí práci zahájit nebo zastavit zůstává na kapitánovi nebo kapitánovi remorkéru.

## Když se něco pokazí
- **Služba není dostupná. Zkuste to znovu.** Stiskněte **Zkusit znovu**.
- **Jste offline. Zobrazený seznam může být zastaralý.** Po připojení stiskněte **Znovu načíst**.
- Pokud stav vypadá neočekávaně, otevřete tabulku **Proč** a obraťte se na vlastníka záznamu nebo vlastníka kontroly (13.03 §10).
- Pokud nemáte přístup, zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce tenanta (13.03 §10).

## Související stránky
