---
key: tug-job-intake
title: Príjem práce
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Príjem práce štruktúruje novú **požiadavku na zákazku remorkéra ako koncept** vo zvolenom kontexte remorkéra. Odpovedá na otázku: „Aké je pre túto zákazku asistované plavidlo, typ práce, časové okno, miesto a požadované zdroje (počet remorkérov, ťažná sila na väzáku)?“ Uložená požiadavka je koncept vyhlásenia; nie je to prevádzkové pridelenie, spôsobilosť, povolenie ani bezpečnostné schválenie.

## Kto ju používa
- **Koordinátor prevádzky remorkérov / pobrežná prevádzka:** na štruktúrovanie požiadavky a zadanie asistovaného plavidla, typu práce, časového okna a požadovanej schopnosti (13.03 §7).

## Skôr než začnete
- V hornej kontextovej lište zvoľte **správnu spoločnosť a remorkér**.
- Potrebujete prístup na zápis remorkérov. Bez neho sa formulár neotvorí a zobrazí sa „Prístup nie je dostupný.“
- Neúplné koncepty je možné uložiť. Povinné prevádzkové údaje a posúdenie oprávnenou osobou musia byť vyriešené pred odoslaním.

## Krok za krokom
1. V **Požiadavka a asistované plavidlo** vyplňte **Zdroj požiadavky**, **Referencia zákazníka**, **Asistované plavidlo**, **IMO asistovaného plavidla**, **Typ práce**, **Priorita** a **Vlastník požiadavky**. Zoznamy bez voľby zostávajú „Neznáme“.
2. V **Požadovaný čas a poloha** zadajte **Požadovaný začiatok (UTC)**, **Požadovaný koniec (UTC, podmienený)**, **Prevádzková oblasť**, **Miesto vyzdvihnutia** a **Cieľový bod**. Časy sú v UTC.
3. V **Potreby zdrojov a poznámky** zadajte **Požadovaná BP (tony, presné desatinné číslo)**, **Požadovaný počet remorkérov**, **Osobitné požiadavky** a **Poznámky**.
4. Stlačte **Uložiť koncept**. Pri úspechu sa otvorí detail zákazky so správou „Koncept bol uložený. Prevádzkové schválenie zostáva nedostupné.“
5. Ak chcete svoje úpravy zahodiť, použite **Zahodiť zmeny**.
6. V detaile zákazky použite **Odoslať požiadavku** na zaznamenanie požiadavky na plánovanie. Ak chýbajú povinné údaje, odoslanie je blokované a chýbajúce polia sú uvedené.

## Čo znamenajú stavy
- **Koncept:** požiadavka je uložená a upraviteľná; zatiaľ nie je odoslaná.
- **Odoslané:** požiadavka je zaznamenaná na plánovanie. Nie je to schválenie, pridelenie ani rozhodnutie o pripravenosti.
- **Zrušené:** požiadavka je uzavretá; nie je možné ju znova spustiť.
- **Neznáme:** pre pole nebola zvolená žiadna hodnota. Neznáme nie je predvolená hodnota a nikdy nezastupuje „Nie“ ani „Neuplatňuje sa“.
- **Skontrolujte túto hodnotu.:** server toto pole odmietol; opravte ho a uložte znova.

## Čo nemôžete robiť / upozornenie
- Táto stránka neprideľuje žiadny remorkér a nikdy nevyhlasuje zákazku za pripravenú. Návrh pridelenia je samostatný krok a nikdy neprevažuje nad bezpečnostným rozhodnutím kapitána remorkéra na palube (13.03 §7).
- Požiadavku nie je možné odoslať, kým chýbajú povinné údaje; neúplný koncept je možné stále uložiť.
- Polia odoslanej požiadavky nie je možné z tohto formulára zmeniť; zrušenú požiadavku nie je možné znova spustiť, namiesto toho vytvorte novú.
- Offline sa koncept uchováva iba na tejto stránke a nie je možné ho uložiť.

## Ak sa niečo pokazí
- **Správa „Save outcome uncertain“:** presný pokus sa uchováva; po opätovnom pripojení použite **Skúsiť znova** na odoslanie toho istého príkazu. Zahodenie lokálnych vstupov nezruší príkaz, ktorý už bol odoslaný.
- **Správa „Konflikt“:** váš koncept sa uchováva; pred novým pokusom ho zahoďte a znova načítajte.
- **Skontrolovať polia:** opravte označené polia.
- **Prístup nie je dostupný:** skontrolujte svoju rolu a kontext remorkéra; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).

## Súvisiace stránky
