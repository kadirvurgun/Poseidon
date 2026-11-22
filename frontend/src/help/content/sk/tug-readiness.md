---
key: tug-readiness
title: Kontrola pripravenosti remorkéra
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Kontrola pripravenosti posudzuje prevádzkovú pripravenosť pridelenia remorkéra zo zdrojových kontrol **iba ako odporúčanie** a umožňuje menovanému kapitánovi remorkéra alebo kapitánovi zaznamenať odôvodnené **rozhodnutie GO/NO-GO**. Odpovedá na otázku: „Čo hovoria kontroly posádky, vybavenia, PMS, certifikátov, komunikácie, počasia, rezervy ťahu na háku, pridelenia a briefingu a aké prekážky existujú?“ Posúdenie nie je rozhodnutím: „Systém nikdy nevydáva GO; rozhodnutie GO/NO-GO s dôvodom zaznamenáva iba menovaný kapitán alebo kapitán remorkéra.“

## Kto ju používa
- **Kapitán remorkéra / kapitán:** na posúdenie hodnotenia a prekážok a zaznamenanie rozhodnutia GO alebo NO-GO (13.03 §3).
- **Koordinátor prevádzky remorkérov / pobrežná prevádzka:** na sledovanie pripravenosti a prekážok; nerozhodujú (13.03 §7).

## Skôr než začnete
- Otvorte stránku z pridelenia: na **Pridelenie a konflikty** použite odkaz **Kontrola prevádzkovej pripravenosti** pod návrhom. Bez zvoleného pridelenia stránka zobrazí „Vyberte pridelenie zo zoznamu pridelení zákazky.“
- Zaznamenanie rozhodnutia vyžaduje prístup na zápis remorkérov a **Vymenovanie** zaznamenané na serveri (kapitán remorkéra alebo kapitán) pre daný remorkér a pridelenie musí byť stále v stave Navrhnuté.
- Bez briefingu nie je žiadne READY: **Riziko vleku a briefing** musí byť vydaný a potvrdený každým účastníkom (13.03 §3).

## Krok za krokom
1. Prečítajte si odznak stavu v riadku **Hodnotenie**. Poznámka vedľa neho uvádza „Odporúčacie, nejde o rozhodnutie GO.“
2. Skontrolujte **Požadovaný ťah na háku (t)**, **Overený dostupný ťah na háku (t)** a **Rezerva ťahu na háku (t)**. Chýbajúca hodnota sa zobrazí ako „Neznáme“.
3. Posúďte kódy v časti **Kritické blokátory**, **Dôvody posúdenia** a **Podmienky**.
4. V tabuľke kategórií uvidíte výsledok každej kategórie (posádka, vlečná spôsobilosť, vybavenie, vlečné vybavenie, PMS a poruchy, certifikáty, komunikácia, počasie, rezerva ťahu na háku, pridelenie, riziko vleku a briefing). Podrobnosti nájdete v kódoch dôvodov v tabuľke **Kontroly zdrojov**.
5. Ak máte rozhodovaciu právomoc, zvoľte vo formulári **Zaznamenať rozhodnutie kapitána** **NO-GO** alebo **GO**, zadajte svoje **Vymenovanie** a **Dôvod** a stlačte **Zaznamenať**.
6. Zaznamenané rozhodnutia uvidíte v zozname **Rozhodnutia kapitána**.

## Čo znamenajú stavy
- **Pripravené (READY):** každá zdrojová kontrola vyhovuje (žiadna prekážka, neznáma položka ani podmienka). Je to informácia, nie rozhodnutie; rozhodnutie GO stále zaznamenáva kapitán remorkéra alebo kapitán.
- **Podmienené (CONDITIONAL):** platia podmienky uvedené v „Podmienky“. GO je vypnuté a stránka vysvetľuje: „Toto posúdenie je CONDITIONAL. GO vyžaduje posúdenie READY: žiadny zdroj nedefinuje, ktoré podmienky by GO povoľovali, preto ho server odmieta. Vyriešte podmienky a posúďte znova, alebo zaznamenajte NO-GO.“
- **Nepripravené (NOT_READY):** existuje aspoň jedna kritická prekážka.
- **Vyžaduje posúdenie (REVIEW_REQUIRED):** niektoré zdroje sú neznáme alebo čakajú na posúdenie. Neznáma kontrola sa nikdy nepočíta ako „Pripravené“.
- **Vyhovuje / Nevyhovuje / Neznáme:** výsledok jednej kategórie alebo kontroly.
- **GO / NO-GO:** ľudské rozhodnutie a dôvod zaznamenané kapitánom remorkéra alebo kapitánom.

## Čo nemôžete robiť / upozornenie
- GO je možné zaznamenať iba voči posúdeniu READY; server odmieta GO pri CONDITIONAL, NOT_READY alebo REVIEW_REQUIRED. Stránka povoľuje GO iba pri posúdení READY; inak uvádza „GO je možné zaznamenať iba voči posúdeniu READY.“ NO-GO je možné s dôvodom zaznamenať vždy.
- Kým sú riziko a briefing neúplné alebo chýbajú potvrdenia účastníkov, nie je žiadne READY; voľba GO je vtedy vypnutá (13.03 §3).
- Návrh pridelenia od pobrežného používateľa nikdy neprevažuje nad bezpečnostným rozhodnutím kapitána remorkéra na palube (13.03 §7).
- Ak sa posúdenie zmení pred vaším rozhodnutím, rozhodnutie sa odmietne; aktuálne posúdenie posúďte znova.
- Offline sú príkazy vypnuté.

## Ak sa niečo pokazí
- **Správa „The assessment changed“:** použite **Načítať znova**, posúďte aktuálne hodnotenie a rozhodnite znova.
- **Správa „The outcome is uncertain“:** **Skúsiť znova** odošle ten istý príkaz.
- **Neočakávaná pripravenosť:** otvorte tabuľku kategórií a dôvody zdrojových kontrol; posúďte ich so zodpovednou osobou kontroly alebo odborníkom (13.03 §10).
- **Bez oprávnenia:** skontrolujte svoju rolu, remorkér a kontext menovania; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).

## Súvisiace stránky
