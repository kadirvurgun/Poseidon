---
key: deck-daily-log
title: Palubný denník
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Palubný denník uchováva **záznamy palubnej hliadky** vybranej lode ako koncepty so zaznamenanou verziou zdroja. Odpovedá na otázku: „Čo hliadka pre tento dátum a obdobie deklarovala o polohe, kurze, rýchlosti, počasí, viditeľnosti, navigácii, prístavných a nákladových prácach, balaste, palubných prácach, bezpečnostných pozorovaniach a odovzdaní hliadky, a bolo pre presne túto revíziu zaznamenané posúdenie?“ Uložený zápis je neoverené vyhlásenie, nie schválený denník.

## Kto ju používa
- **Strážny dôstojník (Crew / OOW):** vedie záznam hliadky v reálnom čase a v chronologickom poradí (13.03 §6).
- **Kapitán alebo prvý dôstojník:** posudzuje uložený denník a zaznamenáva toto posúdenie ako ľudský úkon (13.03 §2). Posúdenie palubného denníka môže zaznamenať iba vymenovanie s funkciou kapitána alebo prvého dôstojníka.

## Skôr než začnete
- V hornom kontextovom paneli vyberte **správnu spoločnosť a loď**. Ak nie je vybraná žiadna loď, stránka zobrazí „Vybrať loď“.
- Čítanie vyžaduje `daily.deck.read`. Vytváranie a ukladanie vyžaduje `daily.deck.write` a práva na úpravy na tejto lodi; inak uvidíte „Read-only access. Editing and saving are not granted.“
- Zaznamenanie posúdenia vyžaduje `daily.deck.write` s právami na úpravy na tejto lodi, `daily.deck.review` a navyše platné vymenovanie za kapitána alebo prvého dôstojníka pre presne tento záznam.

## Krok za krokom
1. Vyberte loď. **Zaznamenané koncepty** uvádza každý denník podľa dátumu a revízie s označením „Draft · review required“.
2. Stlačte **Nový koncept** pre **Nový denný koncept** alebo otvorte uvedený denník a zvoľte **Upraviť zaznamenaný koncept**.
3. Najprv vyplňte **Kontext hliadky** (**Dátum denníka**, **Hliadka / obdobie**, **Referencia strážneho dôstojníka**). Zoznam dôstojníkov ponúka iba osoby dostupné k deklarovanému dátumu denníka.
4. Vyplňte **Navigácia a počasie**, **Prístavné a nákladové činnosti** a **Palubné práce a odovzdanie**. Pri každom poli vyberte **Známe**, **Neznáme** alebo **Chýba**.
5. Stlačte **Uložiť koncept** počas hliadky alebo hneď po nej. Každé uloženie pridá revíziu do **Zaznamenaná história revízií**; skoršie revízie zostanú nezmenené.
6. Kapitán alebo prvý dôstojník otvorí **Záznam ľudského posúdenia**, vyberie **Poverenie oprávnenej osoby** a **Záznam posúdenia**, napíše **Odôvodnenie posúdenia** a stlačí **Zaznamenať posúdenie**.

## Čo znamenajú stavy
- **Koncept · vyhlásenia neoverené / Neoverené vyhlásenie:** deklarované osobou; neoverené.
- **Známe / Neznáme / Chýba:** stav údajov poľa. Neznáme neznamená „Nie“, „Pripravené“ ani „Neuplatňuje sa“.
- **Čakajúce · posúdenie nezaznamenané:** pre túto revíziu neexistuje žiadne ľudské posúdenie.
- **Ľudské metadáta zaznamenané · neposúdené:** pre túto revíziu existuje záznam posúdenia. Nezakladá overenie, odoslanie ani bezpečnostné schválenie kapitánom.
- **Zaznamenať posúdené vyhlásenie (neposúdené) / Vrátiť na opravu:** dva typy záznamov posúdenia, ktoré môže posudzovateľ zaznamenať.
- **Historical revision:** posúdenie patrí staršej revízii, nie aktuálnemu konceptu.

## Čo nemôžete robiť / upozornenie
- Systém denník nikdy neposudzuje ani nefinalizuje. Posúdenie je ľudský úkon vymenovaného kapitána alebo prvého dôstojníka (13.03 §2).
- Ktokoľvek je autorom niektorej revízie denníka, nemôže ju posúdiť. Autorovi sa neponúka žiadne vymenovanie a stránka uvádza „You authored this log revision; an independent reviewer must record the review.“; server takéto posúdenie tiež odmietne z dôvodu oddelenia povinností.
- Zápisy veďte v reálnom čase a chronologicky; neprepisujte starú hliadku tak, aby vyzerala ako nová (13.03 §6).
- Nemeňte posúdený záznam niekoho iného; neskoršie uloženie je nová revízia a vyžaduje nové posúdenie (13.03 §6).
- Offline zaznamenávanie pokrýva iba výslovne klasifikované polia. Offline konflikt sa nikdy nezlúči automaticky; nepredpokladajte, že váš záznam vyhrá (13.03 §6).

## Ak sa niečo pokazí
- **„The record changed. Your inputs are retained.“:** pred novým pokusom výslovne zahoďte zmeny a znova načítajte.
- **„Save outcome unknown“:** použite **Zopakovať presný uložený príkaz**; polia nevypĺňajte znova.
- **Referencia dôstojníka nie je dostupná:** ponechajte pole ako **Chýba** alebo **Neznáme**; napísané meno nemôže nahradiť vyhľadanie.
- **„Human conflict resolution required“:** otvorte **Preskúmať konflikt** alebo Centrum konfliktov a vyberte výslovne (13.03 §10).
- **Nie je dostupné žiadne vymenovanie:** skontrolujte svoju rolu a kontext lode so správcom nájomcu (13.03 §10).

## Súvisiace stránky
