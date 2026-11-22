---
key: audit
title: História činností
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka História činností zobrazuje záznam zmien obchodných záznamov vo vašom autorizovanom rozsahu, ktorý je **read-only** (iba na čítanie) a priradený konkrétnemu aktérovi. Odpovedá na otázku: „Kedy sa tento záznam zmenil, akou akciou a v kontexte akého aktéra a roly?“ Bezpečnostné udalosti sú obmedzené samostatne a nezobrazujú sa tu (13.03 §4, §9).

## Kto ju používa
- **DPA / HSEQ:** sleduje záznamy zistení, opatrení a overení (13.03 §4).
- **Regulatory / Assurance user:** kontroluje históriu záznamu pred auditom alebo posúdením (13.03 §8).
- **Tenant Admin:** audituje zmeny členstva a oprávnení a časy začiatku a konca delegovania (13.03 §9).

## Skôr než začnete
- Zobrazenie vyžaduje oprávnenie na čítanie auditu. Bez neho uvidíte „Vyžaduje sa oprávnenie auditu.“
- Zobrazujú sa iba záznamy v rozsahu nájomcu a plavidla, na ktorý máte oprávnenie.
- Ak hľadáte konkrétny záznam, pripravte si jeho **ID plavidla** alebo **ID objektu**. Odkazy na históriu na iných stránkach otvoria túto stránku už filtrovanú.

## Krok za krokom
1. Vyplňte filtre: **ID plavidla**, **ID objektu**, **Typ objektu** a **Akcia** (**Všetko**, **Vytvorené**, **Aktualizované**, **Odstránené**).
2. Stlačte **Filtrovať**. Na obnovenie použite **Vymazať filtre**.
3. Skontrolujte stĺpce **Čas**, **Objekt** a **Akcia** v časti **Autorizované záznamy**. Medzi stranami sa presúvajte pomocou **Prvá strana** a **Ďalšie**.
4. Stlačením odkazu na objekt v riadku otvorte **Podrobnosti záznamu**. Prečítajte si **Referencia aktéra**, **Referencie pridelených rolí**, **Dôveryhodné oprávnenia**, **Dôvod**, **Metadáta**, **Verzia prostriedku** a **Korelácia**.
5. Ak existuje dôkaz nemennej revízie, stlačte **Otvoriť presnú nemennú revíziu** a uvidíte záznam v podobe, v akej bol. Podrobnosti zatvoríte tlačidlom **Zavrieť**.

## Čo znamenajú stavy
- **Vytvorené / Aktualizované / Odstránené:** druh akcie vykonanej na zázname.
- **Zaznamenané:** metadáta boli zachytené úplne.
- **Historické metadáta sú neúplné / Historický identifikátor nie je dostupný / Historický kontext nie je dostupný:** staršie záznamy tieto informácie neuchovali. Chýbajúce informácie sa nikdy nedopĺňajú odhadom.
- **Hodnoty sú nesprístupnené:** hodnota poľa je skrytá z dôvodu ochrany súkromia; zobrazuje sa iba skutočnosť, že sa zmenila.
- **Dôkaz nemennej revízie nie je pre tento záznam dostupný.** Pre tento typ záznamu neexistuje snímka revízie.

## Čo nemôžete robiť / upozornenie
- Stránka je iba na čítanie. Záznam histórie činností nemožno upraviť ani odstrániť.
- To, že sa záznam objaví v histórii, neznamená, že práca bola overená alebo účinná; Overené ≠ Účinné (13.03 §4).
- Táto stránka zaznamenáva obchodné zmeny. Sama osebe nie je úplným auditom činností používateľov ani dôkazom súladu s ochranou súkromia; bezpečnostné udalosti sa uchovávajú samostatne.
- Zoznam zobrazuje iba rozsah, na ktorý máte oprávnenie. Záznamy mimo vášho rozsahu sa nezobrazujú; to, že záznam nevidíte, neznamená, že sa nikdy nezmenil.

## Ak sa niečo pokazí
- **Vyžaduje sa oprávnenie auditu.** Skontrolujte svoju rolu a rozsah; v prípade potreby sa obráťte na správcu nájomcu (Tenant Admin) (13.03 §10).
- **Ak chcete pokračovať, prihláste sa.** Prihláste sa znova.
- **História nie je dostupná. Skontrolujte filtre a skúste to znova.** Skontrolujte, či sú identifikátory úplné a správne, a potom stlačte **Skúsiť znova**.
- **Týmto filtrom nezodpovedajú žiadne autorizované záznamy.** Rozšírte filtre.
- Ak chyba aplikácie pretrváva, nahláste ju podpore s ID korelácie, obrazovkou, časom a krokmi (13.03 §10).

## Súvisiace stránky
