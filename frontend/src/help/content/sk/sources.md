---
key: sources
title: Koncepty zdrojov
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Koncepty zdrojov uchováva regulačné a pravidlové zdroje, na ktoré sa vaša organizácia spolieha (dohovor, vlajka, klasifikačná spoločnosť, spoločnosť alebo miestne pravidlo), ako **verzované záznamy konceptov**. Odpovedá na otázku: „Na ktorej revízii ktorého oficiálneho zdroja táto požiadavka stojí?“ Platí upozornenie v hornej časti: „KONCEPT lokálny pre nájomcu · Deklarované a neoverené. Dátumy a kontrolné súčty nezakladajú žiadnu právnu autoritu. Odkazy na zdroje sa ukladajú ako text.“ (13.03 §8).

## Kto ju používa
- **Používateľ Regulatory / Assurance:** registruje oficiálny, verzovaný zdroj a uvádza jeho dátum účinnosti a životný cyklus (13.03 §8).
- **DPA / HSEQ:** číta ju, aby zistil, na ktorú revíziu zdroja sa audit alebo zistenie odvoláva (13.03 §4).

## Skôr než začnete
- Zobrazenie vyžaduje čítanie v rozsahu celého nájomcu (tenant) a čítanie regulačných konceptov. Vytváranie a ukladanie konceptov vyžaduje navyše zápis regulačných konceptov; bez neho sa tlačidlo **Nový koncept** nezobrazí.
- Podzobrazenia vyžadujú vlastné oprávnenia, vždy s rozsahom čítania celého nájomcu. **Životný cyklus a uplatniteľnosť** vyžaduje čítanie uplatniteľnosti; úprava pravidiel vyžaduje zápis pravidiel, žiadosť o posúdenie vyžaduje posúdenie pravidiel a hodnotenie vyžaduje hodnotenie uplatniteľnosti; kvalifikácia a potvrdenie faktov vyžadujú regulačnú kvalifikáciu alebo potvrdenie faktov a navyše vymenovanie zaznamenané na serveri. **Kontroly a doklady** vyžaduje čítanie kontrol, čítanie dokladov, čítanie súborov a čítanie uplatniteľnosti; prepojenie vyžaduje zápis kontrol alebo zápis dokladov s právom úprav na lodi a schválenie vyžaduje schválenie kontrol.
- Majte po ruke oficiálny text zdroja a údaje o jeho verzii/zmene. POSEIDON text zdroja nesťahuje ani neoveruje; vy ho deklarujete.
- Uloženie vyžaduje živé spojenie so serverom. Offline sa vaše úpravy uchovávajú na karte, ale neukladajú sa.

## Krok za krokom
1. Zaznamenané koncepty zdrojov si pozrite v zozname. Ak chcete vyhľadávať, píšte do **Search drafts** a stlačte **Hľadať**. Medzi stranami sa pohybujte pomocou **Predchádzajúce** a **Ďalšie**.
2. Pre nový zdroj stlačte **Nový koncept**.
3. V **Draft fields** najprv zvoľte pre každé pole **Stav**: **Chýba**, **Neznáme** alebo **Known (declared)**. Hodnotu môžete zadať až po zvolení **Known (declared)**. Polia sú **Názov**, **Deklarovaný orgán**, **Zdrojová referencia (iba text)**, **Vyhlásenie verzie / zmeny**, **Deklarovaný dátum účinnosti**, **Vyhlásenie / pôvod** a **Čas pozorovania zdroja (s posunom UTC)**.
4. Stlačte **Uložiť koncept**. Pri úspechu uvidíte „Koncept uložený. Neoverené.“; záznam otvorte pomocou **Open saved draft and history**.
5. Otvorte existujúci koncept zo zoznamu, zmeňte ho a uložte znova. Každé uloženie vytvorí novú **Revízia**; staršie revízie zostávajú v **Nemenná história** so svojím obsahom a **Kontrolný súčet integrity (nie oprávnenie)**.
6. Na prechod k pravidlám uplatniteľnosti alebo k reťazcu kontrola – doklad použite **Životný cyklus a uplatniteľnosť** alebo **Kontroly a doklady** nad zoznamom. Na návrat stlačte **Back to register**.

## Čo znamenajú stavy
- **KONCEPT:** uložený stav zobrazený v zozname a v hlavičke záznamu. Každý záznam zdroja na tejto stránke je koncept; nie je to oficiálne prijatie ani rozhodnutie o účinnosti.
- **Chýba:** pre pole zatiaľ nebola zadaná žiadna informácia. Neskoršie kroky (napríklad hodnotenie uplatniteľnosti) sú blokované, kým chýbajú požadované údaje.
- **Neznáme:** informácia sa hľadala, ale nie je známa. Systém nikdy nečíta Neznáme ako „nie“ ani „neuplatňuje sa“; keď sú kritické údaje neznáme, výsledok zostáva v posudzovaní (REVIEW_REQUIRED).
- **Known (declared):** hodnotu ste deklarovali vy. Deklarácia nie je overená informácia.
- **Kontrolný súčet integrity (nie oprávnenie):** ukazuje, že obsah revízie sa nezmenil; nie je to dôkaz právnej platnosti.

## Čo nemôžete robiť / upozornenie
- Uloženú revíziu nemôžete zmeniť. Keď sa zdroj zmení, uložte novú revíziu; požiadavky zostávajú naviazané na staršiu revíziu a história sa uchováva (13.03 §1).
- Nepredpokladajte, že zdroje pravidiel klasifikačnej spoločnosti, vlajky, spoločnosti a miestne pravidlá sa navzájom zastupujú; každý zaznamenajte ako samostatný zdroj (13.03 §8).
- Táto stránka oficiálne neprijíma zdroj a nikomu nedáva výsledok o súlade, povolení ani pripravenosti. Prijatie zdroja a nezávislé posúdenie sú ľudskou právomocou.
- Dátumy účinnosti a kontrolné súčty nezakladajú žiadnu právnu autoritu.

## Ak sa niečo pokazí
- **Permission denied.** Skontrolujte svoju rolu a rozsah nájomcu; v prípade potreby požiadajte Tenant Admin (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Niekto zmenil záznam pred vami. Otvorte aktuálnu revíziu pomocou **Znova načítať uložený koncept** a urobte svoju zmenu znova.
- **Check field states, bounds, dates and source revision.** Hodnota poľa alebo formát dátumu je neplatný.
- **Service unavailable. Your draft is retained.** Keď sa pripojenie obnoví, odošlite ten istý príkaz znova pomocou **Retry frozen command**.
- **Sign in to continue; retain your draft.** Vaša relácia skončila; použite odkaz **Prihlásiť sa**.
- Ak chyba aplikácie pretrváva, nahláste podpore ID korelácie, obrazovku, čas a kroky (13.03 §10).

## Súvisiace stránky
