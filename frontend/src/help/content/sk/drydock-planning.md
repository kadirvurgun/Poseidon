---
key: drydock-planning
title: Plánovanie suchého doku
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Plánovanie suchého doku uchováva **balíky prác v suchom doku** vybraného plavidla (projekt, lodenica, plánované dátumy a položky rozsahu prác). Odpovedá na otázku: „S ktorým pracovným príkazom, poruchou, osvedčením alebo kontrolným zoznamom prehliadky je každá položka rozsahu prác v suchom doku prepojená a sú tieto prepojenia aktuálne?“ Rozsah prác je prepojený s týmito záznamami (13.03 §5). **Pripravenosť prepojení** vyjadruje iba úplnosť prepojení; nie je to výsledok prehliadky klasifikačnej spoločnosti ani vlajky a nie je to vyhlásenie o spôsobilosti na plavbu.

## Kto ju používa
- **Technický inšpektor:** pripravuje balík prác v suchom doku, prepája rozsah prác so záznamami, plánuje ho a dokončuje (13.03 §5).
- **Hlavný strojník:** sleduje, ako je rozsah prác prepojený so záznamami porúch a pracovných príkazov, a s oprávnením overuje balík, ktorý dokončil niekto iný (13.03 §5).

## Skôr než začnete
- Vyberte správne plavidlo. Ak nie je vybrané žiadne plavidlo, stránka uvádza **Ak chcete pokračovať, vyberte plavidlo v pracovnom priestore.**
- Na zobrazenie balíkov potrebujete prístup na čítanie suchého doku. Vytváranie, úpravy a zmena stavu vyžadujú prístup na zápis; overenie vyžaduje prístup na overovanie.
- Pripravte si identifikátory a revízie záznamov pracovných príkazov, porúch, osvedčení a kontrolných zoznamov prehliadok, ktoré budete prepájať.

## Krok za krokom
1. Otvorte balík zo zoznamu alebo vytvorte prázdny koncept pomocou **Vytvoriť balík prác v suchom doku**.
2. Vyplňte **Projekt suchého doku**, **Lodenica**, **Plánovaný začiatok** a **Plánované dokončenie**.
3. Pridajte položky pomocou **Pridať položku rozsahu prác**; zadajte typ **Položka rozsahu prác**, **Názov**, **Pracovný balík**, **Požiadavka klasifikačnej spoločnosti**, **Zákonná požiadavka** a **Priorita**.
4. Pri každej položke použite **Pridať prepojenie** a zadajte **Typ záznamu**, **Identifikátor záznamu** a **Pripnutá revízia**.
5. Na dokončenie pridajte referenciu na dôkaz v časti **Doklad o dokončení** pomocou **Pridať**. Potom stlačte **Uložiť**.
6. Skontrolujte zoznam **Položky blokujúce plánovanie** alebo **Položky blokujúce dokončenie**. V časti **Zmeniť stav** vyberte **Akcia** (**Naplánovať**, **Späť do konceptu**, **Začať suchý dok**, **Dokončiť suchý dok**, **Zrušiť balík**) a potvrďte ju. Zrušenie vyžaduje poznámku. Ponúkajú sa iba akcie platné pre aktuálny stav; balík možno zrušiť iba v stave Koncept alebo Naplánované.
7. Na dokončenom balíku, ktorý ste nedokončili vy, napíšte **Dôvod** s aspoň 10 znakmi a použite **Overiť záznam** alebo **Zamietnuť overenie**.
8. Plánované a skutočné dátumy a každú revíziu sledujte na **Časová os**.

## Čo znamenajú stavy
- **Koncept / Naplánované / Prebieha / Dokončené / Overené / Zrušené:** životný cyklus balíka. Dokončené a Overené nie sú to isté.
- **Prepojenia aktuálne / Vyžaduje pozornosť / Vyžaduje posúdenie / Neúplné:** pripravenosť prepojení. Ani pri aktuálnych prepojeniach nejde o výsledok prehliadky.
- **Prepojené / Neprepojené / Po termíne / Vyžaduje posúdenie:** stav prepojenia položky rozsahu prác (Vyžaduje posúdenie, keď je niektoré prepojenie Zastarané alebo Nedostupné).
- **Aktuálne / Zastarané / Nedostupné:** prepojený záznam v porovnaní s jeho pripnutou revíziou. Zastarané prepojenie posúďte a pomocou **Znova pripnúť na** ho pripnite k novej revízii.
- **Neznáme / nenastavené:** nebola zadaná žiadna priorita. Neznáme neznamená „Normálna“ ani „Nízka“.

## Čo nemôžete robiť / upozornenie
- Stránka nerozhoduje, že suchý dok je bezpečný ani že plavidlo je spôsobilé na plavbu.
- Nemôžete overiť balík, ktorý ste dokončili. Zamietnutie overenia vráti balík do stavu **Prebieha**.
- Náklady, mena, dodávateľ a rozpočet sú iba externé referencie; nič sa tu nesčítava, neschvaľuje ani neobjednáva.
- Dokončený, overený alebo zrušený balík nemožno upraviť.
- V režime offline sa príkazy neodosielajú.

## Ak sa niečo pokazí
- **Server uviedol chýbajúce alebo blokujúce položky:** vyriešte zoznam prekážok a skúste to znova.
- **Záznam sa zmenil alebo je príkaz v konflikte:** použite **Načítať najnovšie** a posúďte.
- **Výsledok je neznámy:** **Skúsiť znova** odošle identický príkaz znova.
- **K tomuto záznamu nemáte prístup:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Tieto typy prepojených záznamov nemôžete čítať:** overte oprávnenie s vlastníkom záznamu alebo správcom nájomcu.

## Súvisiace stránky
