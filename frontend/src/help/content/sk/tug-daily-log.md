---
key: tug-daily-log
title: Denný denník remorkéra
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## Na čo slúži táto stránka
Denný denník remorkéra zaznamenáva pre jeden dátum a hliadku **zákazky, počítadlá prevádzkových hodín motorov, palivo a poruchy** a porovnáva ich s vlečeniami daného dňa na **odsúhlasenie**. Odpovedá na otázku: „Koľko zákaziek bolo v tento deň/hliadke dokončených, zodpovedajú hodiny počítadiel časom vykonania, koľko paliva sa spotrebovalo a ktoré poruchy boli nahlásené?“ Odsúhlasenie je odporúčacie; kontrola kapitánom je samostatný ľudský záznam.

## Kto ju používa
- **Posádka / strážna služba:** na vedenie záznamu hliadky v reálnom čase a v poradí (13.03 §6).
- **Kapitán remorkéra / kapitán:** na kontrolu odoslaného denníka a jeho overenie alebo vrátenie (13.03 §3).

## Skôr než začnete
- Otvorte remorkér z **Prevádzka remorkérov**.
- Vytváranie a úprava denníka vyžadujú prístup na zápis remorkérov.
- Kontrola vyžaduje **Vymenovanie** zaznamenané na serveri (kapitán remorkéra alebo kapitán) pre daný remorkér. Kto denník napísal alebo odoslal, ho nemôže kontrolovať.

## Krok za krokom
1. Otvorte existujúci denník zo zoznamu, alebo zvoľte **Dátum denníka** a **Hliadka** a stlačte **Vytvoriť denný denník**.
2. Zadajte **Časové pásmo (IANA)**.
3. V **Počítadlá prevádzkových hodín motorov** zadajte pre každé **Počítadlo** **Počiatočný stav**, **Konečný stav** a v prípade potreby **Doklad o vynulovaní alebo pretečení**.
4. V **Spotrebované palivo** zadajte hodnotu, jednotku a **Odkaz na zdroj údajov o palive**.
5. V **Vlečenia počas dňa** zaškrtnite vykonania, ktoré patria k tomuto denníku; vyplňte **Linked defect identifiers** a **Denný súhrn** (súhrn aspoň 20 znakov).
6. Stlačte **Uložiť**. V riadku **Odsúhlasenie** skontrolujte dokončené zákazky, hodiny vykonania, hodiny počítadiel a nahlásené poruchy.
7. Keď je zoznam **Prekážky** prázdny, stlačte **Odoslať na kontrolu kapitánovi**.
8. Ako kapitán zvoľte svoje **Vymenovanie**, napíšte dôvod a stlačte **Kapitán: overiť denník** alebo **Kapitán: vrátiť denník**.

## Čo znamenajú stavy
- **Koncept / Odoslané / Vrátené / Overené:** stav denníka.
- **Kontrola kapitánom — Čaká / Pozor / Skontrolované:** Čaká = čaká sa na kontrolu; Pozor = vrátené, alebo odoslané, kým odsúhlasenie nie je Odsúhlasené; Skontrolované = overené kapitánom. Odoslaný denník nie je overený denník.
- **Odsúhlasené / Vyžaduje posúdenie / Neúplné:** odporúčací výsledok porovnania. Kódy dôvodov sú uvedené pod ním.
- **otvorený interval:** vykonanie stále prebieha; hodiny vykonania ešte nie sú konečné.
- **Bezpečnostné udalosti — Neznáme:** táto stránka bezpečnostné udalosti nepočíta; nevyvodzuje sa žiadny záver „žiadne udalosti“.
- **Synchronizované / Čaká na synchronizáciu / Konflikt / Zlyhalo:** stav synchronizácie. Čaká na synchronizáciu ukazuje, že zmena sa ešte nedostala na server.

## Čo nemôžete robiť / upozornenie
- Kto denník napísal alebo ho odoslal na kontrolu kapitánovi, ho nemôže overiť.
- Výsledok odsúhlasenia nie je schválenie; kontrolu vykonáva kapitán.
- Dokončenie záznamu a jeho overenie nie sú tá istá akcia (13.03 §1).
- Offline sa jedna neuložená zmena uchováva v pamäti tejto stránky a po obnovení pripojenia sa odošle s pôvodným kľúčom; nechajte stránku otvorenú. Odoslanie a kontrola prebiehajú online.
- Ak sa objaví konflikt, nepredpokladajte, že váš vlastný záznam automaticky vyhrá (13.03 §6).

## Ak sa niečo pokazí
- **Správa „Server uviedol chýbajúce alebo blokujúce položky.“:** odstráňte kódy v **Prekážky**.
- **Správa „The record changed or the command conflicts.“:** pomocou **Načítať najnovšie** skontrolujte aktuálny denník; uchovaná lokálna zmena sa zahodí.
- **Správa „The outcome is unknown.“:** **Skúsiť znova** odošle rovnaký príkaz znova.
- **Konflikt:** zmena uchovaná na tejto stránke sa nikdy nezlučuje; použite **Načítať najnovšie**. Denné záznamy zachytené v **Offline operácie remorkérov**, ktoré sú v konflikte, sa riešia tam; sú uvedené aj v **Centrum podstatných konfliktov** (13.03 §10).

## Súvisiace stránky
