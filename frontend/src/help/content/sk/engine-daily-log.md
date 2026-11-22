---
key: engine-daily-log
title: Strojný denník
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Strojný denník uchováva **záznamy strojnej hliadky** vybranej lode ako koncepty so zaznamenanou verziou zdroja. Odpovedá na otázku: „Čo strojná hliadka deklarovala o stave a hodinách motorov, spotrebe paliva a mazacieho oleja, sladkej vode, kritických meraniach, alarmoch, poruchách, bunkrovaní, podpalubnej vode a environmentálnych kontrolách, a zaznamenal hlavný strojník posúdenie presne tejto revízie?“ Uložený zápis je neoverené vyhlásenie. Údaje z konceptu sa nepremieňajú na závery o emisiách, zabezpečení paliva ani pripravenosti.

## Kto ju používa
- **Strojník na hliadke (Crew / OOW / Engineer):** vedie záznam strojnej hliadky v reálnom čase a v chronologickom poradí (13.03 §6).
- **Hlavný strojník:** posudzuje uložený denník, prepája ho s pracovnými príkazmi a poruchami a zaznamenáva posúdenie ako ľudský úkon (13.03 §5). Posúdenie strojného denníka môže zaznamenať iba vymenovanie s funkciou hlavného strojníka.

## Skôr než začnete
- V hornom kontextovom paneli vyberte **správnu spoločnosť a loď**.
- Čítanie vyžaduje `daily.engine.read`; ukladanie vyžaduje `daily.engine.write` a práva na úpravy na tejto lodi. Zaznamenanie posúdenia vyžaduje `daily.engine.write` s právami na úpravy na tejto lodi, `daily.engine.review` a navyše platné vymenovanie za hlavného strojníka pre presne tento záznam.
- Prepájanie pracovných príkazov alebo porúch vyžaduje aj prístup na čítanie týchto záznamov.

## Krok za krokom
1. Vyberte loď. **Zaznamenané koncepty** uvádza každý denník podľa dátumu a revízie s označením „Draft · review required“.
2. Stlačte **Nový koncept** alebo otvorte uvedený denník a zvoľte **Upraviť zaznamenaný koncept**.
3. Vyplňte **Kontext strojnej hliadky**, **Stav motora a hodiny**, **Spotreba a výroba**, **Merania, alarmy a poruchy** a **Pracovný a environmentálny kontext**. Pri každom poli vyberte **Známe**, **Neznáme** alebo **Chýba**.
4. Stlačte **Uložiť koncept**. Každé uloženie pridá revíziu do **Zaznamenaná história revízií**.
5. Na uloženom denníku použite **Pracovné príkazy a poruchy**: vyberte **Typ záznamu** a **Záznam** a potom **Prepojiť**. Prepojenie pripne jednu presnú revíziu; **Odstrániť odkaz** ho odstráni.
6. V prípade potreby otvorte **Otvoriť palivový záznam z uloženého zdroja** alebo **Otvoriť záznam odstávky z uloženého zdroja**. Množstvá a jednotky sa nekopírujú.
7. Hlavný strojník otvorí **Záznam ľudského posúdenia**, vyberie **Poverenie oprávnenej osoby** a **Záznam posúdenia**, napíše **Odôvodnenie posúdenia** a stlačí **Zaznamenať posúdenie**.

## Čo znamenajú stavy
- **Neoverené vyhlásenie / Známe / Neznáme / Chýba:** deklarované hodnoty a stav ich údajov. Neznáme neznamená „Nie“, „Pripravené“ ani „Neuplatňuje sa“.
- **Čakajúce · posúdenie nezaznamenané:** pre túto revíziu neexistuje žiadne ľudské posúdenie.
- **Ľudské metadáta zaznamenané · neposúdené:** pre túto revíziu existuje záznam posúdenia hlavného strojníka. Nezakladá overenie ani schválenie hlavným strojníkom.
- **Aktuálna revízia / Existuje novšia revízia:** či sa prepojený pracovný príkaz alebo porucha od prepojenia zmenili. Prepojenie vyjadruje iba pôvod; daný záznam neoveruje, neuzatvára ani neschvaľuje.
- **vyhľadávanie nedostupné — NEZNÁME:** prepojený typ záznamu nemožno s vaším prístupom prečítať.

## Čo nemôžete robiť / upozornenie
- Systém denník nikdy neposudzuje ani nefinalizuje. Posúdenie je ľudský úkon vymenovaného hlavného strojníka (13.03 §5).
- Ktokoľvek je autorom niektorej revízie denníka, nemôže ju posúdiť. Autorovi sa neponúka žiadne vymenovanie a stránka uvádza „You authored this log revision; an independent reviewer must record the review.“; server takéto posúdenie tiež odmietne z dôvodu oddelenia povinností.
- Zápisy veďte v reálnom čase a chronologicky; nemeňte posúdený záznam niekoho iného (13.03 §6).
- Prepojenie poruchy tu nezaznamenáva obmedzenie ani poruchu neuzatvára; urobte to na stránke poruchy alebo pracovného príkazu (13.03 §5).
- Prepojenia vyžadujú aktuálne online pripojenie; offline sa nič neprepojí. Offline konflikty sa nikdy nezlučujú automaticky.

## Ak sa niečo pokazí
- **„The log entry changed. Reload it before changing links.“:** znova načítajte a potom znova prepojte.
- **„Save outcome unknown“:** použite **Zopakovať presný uložený príkaz**; nevypĺňajte znova.
- **„Human conflict resolution required“:** otvorte **Preskúmať konflikt** alebo Centrum konfliktov a vyberte výslovne (13.03 §10).
- **Nie je dostupné žiadne vymenovanie alebo je prístup odmietnutý:** skontrolujte svoju rolu a kontext lode so správcom nájomcu (13.03 §10).

## Súvisiace stránky
