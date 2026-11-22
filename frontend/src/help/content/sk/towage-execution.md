---
key: towage-execution
title: Vykonanie vlečenia
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Konzola vykonania zaznamenáva **skutočné vykonanie** odoslanej zákazky remorkéra ako časovú os: začatie, pozorovania, úkony **Pozastaviť / Obnoviť / Prerušiť / Dokončiť** a neskoršie overenie. Odpovedá na otázku: „Kedy sa operácia začala, kedy bolo vlečné lano pripojené a uvoľnené, ktoré pozorovania boli zaznamenané, v akom stave je operácia a bol záznam overený?“ Začatie a prechody sú ľudské záznamy kapitána remorkéra alebo kapitána na vykonávajúcom remorkéri; konzola nikdy nevydáva GO.

## Kto ju používa
- **Kapitán remorkéra / kapitán:** na začatie operácie a zaznamenanie časovej osi Začatie/Pozastavenie/Obnovenie/Prerušenie/Dokončenie a jej dôkazov (13.03 §3).
- **Overovateľ (osoba s právomocou overiť vykonanie):** na overenie dokončeného alebo prerušeného záznamu alebo zamietnutie jeho overenia (13.03 §1).
- **Koordinátor prevádzky remorkérov / pobrežná prevádzka:** na sledovanie úplnosti dôkazov a histórie dokončených zákaziek; nemôžu operáciu začať ani prerušiť (13.03 §7).

## Skôr než začnete
- Otvorte remorkér z **Prevádzka remorkérov** a zvoľte zákazku z **Vyberte odoslanú zákazku remorkéra**.
- Na začatie musí byť aktuálne všetko z tohto: aktívne pridelenie remorkéra, schválený **Plán vleku** (remorkér musí byť v pláne), vydaný **Riziko vleku a briefing** potvrdený každým účastníkom a záznam **GO** v **Kontrola prevádzkovej pripravenosti** naviazaný na aktuálne posúdenie.
- Začatie a prechody vyžadujú **Menovanie** zaznamenané na serveri (kapitán remorkéra alebo kapitán) pre daný remorkér.

## Krok za krokom
1. V sekcii **Podmienka začatia** si pre každý remorkér prečítajte **Rozhodnutie o pripravenosti** a **Prekážky**.
2. Ak nie sú žiadne prekážky, prečítajte si potvrdzovací text ako menovaná osoba na palube, zvoľte svoje **Menovanie**, voliteľne zadajte **Počiatočná poloha** a stlačte **Začať operáciu**.
3. Keď je vykonanie otvorené, sledujte **Stav**, **Overenie** a polia záznamníka (vlečné lano pripojené/uvoľnené, maximálne pozorované zaťaženie vleku, komunikácia, navijak, test núdzového uvoľnenia, aktívne minúty).
4. V **Zaznamenať pozorovanie** zvoľte typ pozorovania (napríklad vlečné lano pripojené, zaťaženie vleku, kontrola komunikácie, počasie, stav navijaka, test núdzového uvoľnenia, poloha, dôkaz, poznámka), zadajte hodnoty a stlačte **Zaznamenať pozorovanie**.
5. V **Úkony veliteľa remorkéra** zvoľte **Akcia** (Pozastaviť, Obnoviť, Prerušiť, Dokončiť) a zadajte svoje **Menovanie** a **Dôvod**. Prerušenie a dokončenie vyžadujú dôvod s dĺžkou aspoň 20 znakov a otvárajú pole **Koncová poloha**; dokončenie sa bez koncovej polohy neprijme.
6. Ak sú uvedené **Položky blokujúce dokončenie**, odstráňte ich; napríklad ak je vlečné lano stále pripojené, najprv zaznamenajte jeho uvoľnenie.
7. Ako overovateľ napíšte dôvod a stlačte **Overiť záznam** alebo **Zamietnuť overenie**.
8. Všetky udalosti v poradí uvidíte v **Časová os**.

## Čo znamenajú stavy
- **Aktívne / Pozastavené / Dokončené / Prerušené:** stav operácie. Dokončené neznamená, že záznam je overený.
- **Čaká / Overené / Zamietnuté:** stav overenia. Dokončené ≠ Overené; overenie je záznam inej osoby.
- **Dobrá / Stratená / Vyhovuje / Nevyhovuje / Podmienené / Neznáme:** výsledok pozorovaní komunikácie, navijaka a núdzového uvoľnenia. Neznáme ukazuje, že nebolo nič zaznamenané; nikdy nezastupuje „Dobrá“ ani „Vyhovuje“.
- **Bezpečnostné udalosti — Neznáme:** táto konzola bezpečnostné udalosti nepočíta; nevyvodzuje sa žiadny záver „žiadne udalosti“.
- **GO (ľudský záznam) / NO GO (ľudský záznam):** rozhodnutie zaznamenané kapitánom remorkéra alebo kapitánom v kontrole pripravenosti.

## Čo nemôžete robiť / upozornenie
- Pobrežní používatelia, AI a systémové identity nemôžu operáciu začať ani prerušiť. Pobrežné pridelenie nikdy neprevažuje nad bezpečnostným rozhodnutím kapitána remorkéra na palube (13.03 §7).
- Pozastaviť, obnoviť, prerušiť alebo dokončiť smie iba menovaný kapitán remorkéra alebo kapitán. Prerušenie je vždy dostupné, kým operácia prebieha.
- Osoba, ktorá operáciu začala alebo ukončila, nemôže overiť ten istý záznam; overenie prebieha po skončení operácie.
- Záznamník umožňuje iba pridávanie; zaznamenané udalosti sa nikdy nemažú ani nemenia.
- Offline sa neodosiela žiadny príkaz. Pozorovania je možné zaradiť do frontu v **Offline operácie remorkérov**; začatie, prechody a overenie zostávajú online.

## Ak sa niečo pokazí
- **Správa „Server uviedol chýbajúce alebo blokujúce položky.“:** odstráňte kódy v **Prekážky** alebo **Položky blokujúce dokončenie** (napríklad plán neschválený, potvrdenie briefingu čaká, záznam GO chýba alebo nie je aktuálny).
- **Správa „The record changed or the command conflicts.“:** pomocou **Načítať najnovšie** záznam skontrolujte.
- **Správa „The outcome is unknown.“:** **Skúsiť znova** odošle rovnaký príkaz znova.
- **Bez oprávnenia:** skontrolujte svoju rolu, remorkér a kontext menovania; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).

## Súvisiace stránky
