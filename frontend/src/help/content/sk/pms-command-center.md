---
key: pms-command-center
title: Riadiace centrum PMS
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Riadiace centrum PMS zhromažďuje na jednej obrazovke **výnimky** údržby vybraného plavidla. Odpovedá na otázku: „Má toto plavidlo kritické úlohy po termíne, otvorené pracovné príkazy, upozornenia na poruchy alebo chýbajúce kritické náhradné diely a prečo?“ Stránka je poradné zobrazenie výnimiek; nie je rozhodnutím o spôsobilosti, pripravenosti ani bezpečnosti. Položky v poriadku sa neuvádzajú, iba sa spočítajú.

## Kto ju používa
- **Hlavný strojník / Technický inšpektor:** sledujú úlohy po termíne, otvorené pracovné príkazy, upozornenia na poruchy a chýbajúce kritické náhradné diely (13.03 §5).
- **Kapitán:** vidí vplyv záznamov PMS a porúch na prevádzkové obmedzenia (13.03 §2).

## Skôr než začnete
- Na zobrazenie stránky potrebujete oprávnenie na čítanie vybavenia (`equipment.read`).
- Každá časť závisí od oprávnenia na čítanie svojho modulu. Napríklad bez oprávnenia na čítanie porúch zostáva časť **Upozornenia na stav** v stave „Vyžaduje sa prístup: nezobrazené“.
- Najprv vyberte plavidlo. Bez plavidla stránka zobrazuje iba zoznam **Vyberte plavidlo.**

## Krok za krokom
1. Vyberte plavidlo zo zoznamu. Ak vo vašom rozsahu žiadne nie je, stránka uvádza „No vessel is in your scope.“
2. Hore si prečítajte odznak **Súhrn výnimiek** a čas **Posledné obnovenie**.
3. Skontrolujte časti: **Kritické úlohy po termíne**, **Splatné, oneskorené a na kontrolu**, **Otvorené pracovné príkazy**, **Upozornenia na stav** a **Chýbajúce kritické náhradné diely**.
4. Pri položke otvorte **Prečo**, aby ste videli zaznamenané kódy dôvodov a, ak je k dispozícii, čas **Zistené**.
5. Pomocou **Otvoriť zdrojový záznam** prejdete na zdroj. Položka poruchy otvorí [Poruchy a dočasné opatrenia](/app/defects), položka pracovného príkazu otvorí [Centrum pracovných príkazov](/app/work-order-center).
6. Stlačením **Obnoviť** znova načítate údaje.

## Čo znamenajú stavy
- **Blokované / Vyžaduje posúdenie / Vyžaduje pozornosť:** stav súhrnu výnimiek. Podrobnosti sú v položkách jednotlivých častí.
- **Žiadne otvorené výnimky (poradné):** prečítané zdroje nevrátili žiadnu otvorenú výnimku. Nie je to výsledok pripravenosti ani spôsobilosti.
- **Kritické / Vyžaduje pozornosť / Vyžaduje posúdenie:** závažnosť položky.
- **Zdroj nedostupný: neznáme, nie nula:** zdroj časti nebolo možné prečítať. Neznáme neznamená „nula“, „žiadny problém“ ani „Pripravené“.
- **Vyžaduje sa prístup: nezobrazené:** na túto časť nemáte oprávnenie; nezobrazuje sa obsah ani počet.
- **Z tohto zdroja žiadne výnimky:** časť bola prečítaná a nevrátila žiadnu výnimku. Počet položiek v poriadku sa zobrazuje samostatne.
- **Zastarané údaje / Aktuálnosť neznáma:** načítanie za položkou nie je aktuálne alebo jeho aktuálnosť nie je známa.

## Čo nemôžete robiť / upozornenie
- Stránka nemení žiadny záznam; iba číta. Pracovné príkazy, poruchy a náhradné diely sa spracúvajú na ich vlastných stránkach.
- Stránka nikdy nevyhlási plavidlo za „pripravené“ ani „bezpečné“. Rozhodnutie o vyplávaní patrí kapitánovi (13.03 §2).
- Porucha sa považuje za uzavretú iba na základe ľudského záznamu o uzavretí a iba pre revíziu vyhlásenia, ktorá bola uzavretá. Ak sa vyhlásenie neskôr zmení, položka sa vráti na posúdenie.
- Údaje zobrazené v režime offline môžu byť staré a **Obnoviť** nie je dostupné.

## Ak sa niečo pokazí
- **Záznam nebolo možné načítať:** skontrolujte pripojenie a použite **Znova načítať**.
- **Nemáte serverové oprávnenie na túto akciu alebo záznam:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Neočakávaná výnimka:** otvorte **Prečo** a posúďte zdrojový záznam so zodpovedným vlastníkom alebo odborným expertom (SME) (13.03 §10).
- **Chyba aplikácie:** nahláste podpore ID korelácie (Correlation ID), obrazovku, čas a kroky (13.03 §10).

## Súvisiace stránky
