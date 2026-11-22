---
key: towage-plan
title: Plán vleku
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Plán vleku zaznamenáva pre odoslanú zákazku remorkéra **úlohy a konfiguráciu remorkérov, výmenu informácií, prevádzkové limity, komunikáciu, núdzové uvoľnenie, pohotovostné opatrenia a opatrenia pre zónu spätného rázu lana (snap-back)**. Odpovedá na otázku: „Ako sa táto zákazka vykoná, v akých limitoch a s akými núdzovými opatreniami?“ Schválenie plánu je ľudský záznam menovaného kapitána alebo kapitána remorkéra; nie je to rozhodnutie GO ani povolenie. Limity sú vstupy plánu uvedené s ich zdrojom; systém nedodáva žiadne predvolené hodnoty.

## Kto ju používa
- **Koordinátor prevádzky remorkérov / plánovač:** na prípravu plánu a jeho odoslanie na schválenie (13.03 §7).
- **Kapitán remorkéra / kapitán:** na potvrdenie konfigurácie, lana, BP/schopnosti, komunikácie, limitov, pohotovostných opatrení a podmienok zastavenia práce a na schválenie alebo vrátenie plánu (13.03 §3).

## Skôr než začnete
- Otvorte remorkér z **Prevádzka remorkérov** a zvoľte zákazku z **Vyberte odoslanú zákazku remorkéra**.
- Úprava plánu vyžaduje prístup na zápis remorkérov a zápis plánu. Remorkéry v pláne sa vyberajú z remorkérov s aktuálnym návrhom pridelenia pre túto zákazku.
- Schválenie alebo vrátenie vyžaduje **Menovanie** zaznamenané na serveri (kapitán remorkéra alebo kapitán) pre koordinujúci remorkér.

## Krok za krokom
1. Prečítajte si **Stav** a **Revízia**, potom zoznam **Chýba pred odoslaním**.
2. V **Úlohy a konfigurácia remorkérov** použite **Pridať** pre každý remorkér; zadajte **Pozícia**, **Spôsob vleku**, **Koordinujúci (vedúci) remorkér**, **Zdroj vlečného lana**, **Bod pripojenia**, **ID vybavenia vlečného lana** a **ID vybavenia núdzového uvoľnenia**.
3. Napíšte text **Výmena informácií kapitán lode / lodivod / kapitán remorkéra**.
4. V **Prevádzkové limity (vstupy plánu)** zadajte pre každý **Parameter** **Hodnota** a **Zdroj limitu**. Nový limit začína prázdny.
5. Vyplňte **Komunikačný plán** (hlavný a záložný kanál, pracovný jazyk, postup pri strate komunikácie), **Núdzové uvoľnenie**, **Pohotovostné opatrenia** a **Opatrenia pre zónu spätného rázu lana (snap-back)**.
6. Uložte pomocou **Vytvoriť koncept** alebo **Uložiť koncept**; keď nič nechýba, stlačte **Odoslať na schválenie**.
7. Schvaľovateľ zvolí **Menovanie**, napíše **Dôvod** s dĺžkou aspoň 10 znakov a stlačí **Schváliť** alebo **Vrátiť**.
8. Ak sa schválený plán musí zmeniť, napíšte dôvod a použite **Upraviť**. Zoznamy **Záznamy o schválení** a **Revízie** zobrazujú históriu.

## Čo znamenajú stavy
- **Koncept:** plán je možné upravovať.
- **V posudzovaní:** plán bol odoslaný na schválenie; čaká sa na rozhodnutie.
- **Vrátené:** schvaľovateľ vrátil plán s dôvodom; opravte ho a odošlite znova.
- **Schválené:** kapitán alebo kapitán remorkéra schválil túto revíziu plánu. Nie je to rozhodnutie GO; kontrola pripravenosti a briefing sú stále potrebné.
- **Neznáme:** pre pole nebola zvolená žiadna hodnota. „Neznáme“ pri zónach spätného rázu lana nikdy nezastupuje „Nie“.
- **Požiadavka na remorkér sa zmenila po uložení tohto plánu.:** plán vychádza zo staršej revízie požiadavky a musí sa posúdiť.

## Čo nemôžete robiť / upozornenie
- Osoba, ktorá plán vytvorila alebo odoslala, ho nemôže schváliť.
- Plán s chýbajúcimi povinnými sekciami nie je možné odoslať; neúplný koncept je možné uložiť.
- Systém nenavrhuje žiadnu hodnotu limitu, kanála ani prahu; každá hodnota pochádza od plánovača spolu s jej zdrojom.
- Schválený plán nie je povolením na začatie; začatie vyžaduje aktuálne potvrdenia briefingu a záznam GO kapitána remorkéra (13.03 §3).
- Offline sú príkazy vypnuté.

## Ak sa niečo pokazí
- **Správa „Chýbajú povinné sekcie.“:** doplňte kódy v **Chýba pred odoslaním**.
- **Správa „The record changed“:** pomocou **Načítať znova** skontrolujte aktuálny plán.
- **Správa „The outcome is unknown“:** **Skúsiť znova** odošle rovnakú požiadavku znova.
- **Bez oprávnenia:** skontrolujte svoju rolu, remorkér a kontext menovania; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).

## Súvisiace stránky
