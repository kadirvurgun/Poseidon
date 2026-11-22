---
key: noon-report
title: Poludňajšie hlásenie
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: [1]
reviewed: false
---
## Na čo slúži táto stránka
Stránka poludňajšieho hlásenia uchováva **denné poludňajšie vyhlásenia** vybranej lode ako koncepty so zaznamenanou verziou zdroja. Odpovedá na otázku: „Čo sme deklarovali o čase hlásenia, plavbe, polohe, vzdialenosti, počasí, náklade, hodinách strojných zariadení, palive a ROB a zaznamenal kapitán posúdenie presne tejto revízie?“ Uložený koncept je neoverené vyhlásenie. Jeho uložením nevzniká schválenie kapitánom ani hlavným strojníkom a stránka z neho nevyvodzuje žiadny záver o emisiách, zabezpečení paliva ani pripravenosti.

## Kto ju používa
- **Dôstojník pripravujúci hlásenie (Crew / OOW):** vypĺňa a ukladá poludňajší koncept pre správnu loď a dátum (13.03 §6).
- **Kapitán:** posudzuje uložené hlásenie a zaznamenáva toto posúdenie ako ľudský úkon (13.03 §2). Poludňajšie posúdenie môže zaznamenať iba vymenovanie s funkciou kapitána.

## Skôr než začnete
- V hornom kontextovom paneli vyberte **správnu spoločnosť a loď**. Ak nie je vybraná žiadna loď, stránka zobrazí „Vybrať loď“.
- Čítanie vyžaduje `daily.noon.read`. Vytváranie a ukladanie konceptov vyžaduje `daily.noon.write` a práva na úpravy na tejto lodi; bez nich uvidíte „Read-only access. Editing and saving are not granted.“
- Zaznamenanie posúdenia vyžaduje `daily.noon.write` s právami na úpravy na tejto lodi, `daily.noon.review` a navyše platné vymenovanie za kapitána pre presne tento záznam.

## Krok za krokom
1. Vyberte loď. Zoznam **Zaznamenané koncepty** zobrazuje každé hlásenie podľa dátumu a revízie s odznakom „Draft · review required“. Na stránkovanie použite **Predchádzajúce koncepty** / **Ďalšie koncepty**.
2. Stlačením **Nový koncept** otvoríte **Nový denný koncept**, alebo otvorte uvedené hlásenie a zvoľte **Upraviť zaznamenaný koncept**.
3. Vyplňte skupiny (**Hlásenie a plavba**, **Poloha a činnosť**, **Počasie a náklad**, **Strojné zariadenia, palivo a zostatky**). Pri každom poli vyberte **Stav údajov**: **Známe**, **Neznáme** alebo **Chýba**. Neúplné koncepty možno uložiť.
4. Stlačte **Uložiť koncept**. Stránka potvrdí „Draft saved. Human review and operational approval remain separate.“ Každé uloženie pridá revíziu do **Zaznamenaná história revízií**.
5. Na uloženom hlásení kapitán otvorí **Záznam ľudského posúdenia**, vyberie **Poverenie oprávnenej osoby**, **Záznam posúdenia** („Zaznamenať posúdené vyhlásenie (neposúdené)“ alebo „Vrátiť na opravu“), napíše **Odôvodnenie posúdenia** a potom stlačí **Zaznamenať posúdenie**.
6. Ak treba zaznamenať palivo alebo prístavnú udalosť, použite **Použiť uložený prevádzkový zdroj** a **Otvoriť palivový záznam z uloženého zdroja** alebo **Otvoriť prístavnú udalosť z uloženého zdroja**. Množstvá, jednotky a schválenia sa nekopírujú.

## Čo znamenajú stavy
- **Koncept · vyhlásenia neoverené / Neoverené vyhlásenie:** hodnoty deklarovala osoba; nikto ich neoveril.
- **Známe / Neznáme / Chýba:** stav údajov poľa. Neznáme neznamená „Nie“, „Pripravené“ ani „Neuplatňuje sa“ a Chýba nie je nula.
- **Čakajúce · posúdenie nezaznamenané:** pre túto revíziu nebolo zaznamenané žiadne ľudské posúdenie.
- **Ľudské metadáta zaznamenané · neposúdené / Zaznamenané · neposúdené:** pre zobrazenú revíziu existuje záznam posúdenia kapitánom. Zaznamenanie posúdenia nezakladá overenie, odoslanie ani bezpečnostné schválenie kapitánom.
- **Historical revision:** záznam posúdenia patrí staršej revízii a neposudzuje aktuálny uložený koncept.

## Čo nemôžete robiť / upozornenie
- Systém hlásenie nikdy neposudzuje ani nefinalizuje. Zaznamenanie posúdenia je ľudský úkon vymenovaného kapitána (13.03 §2).
- Ktokoľvek je autorom niektorej revízie denníka, nemôže ju posúdiť. Autorovi sa neponúka žiadne vymenovanie a stránka uvádza „You authored this log revision; an independent reviewer must record the review.“; server takéto posúdenie tiež odmietne z dôvodu oddelenia povinností. Ak hlásenie napísal kapitán, posúdenie musí aj tak zaznamenať nezávislý posudzovateľ s oprávnením na posúdenie.
- Nové uloženie vytvorí novú revíziu; skoršie posúdenie zostáva na svojej revízii. Po zmenách posúďte znova.
- Napísaná voľná textová referencia nemôže nahradiť autorizované vyhľadanie; ponechajte pole ako **Chýba** alebo **Neznáme**.
- V režime offline môžu do šifrovaného frontu ísť iba výslovne klasifikované polia. Polia posúdenia, schválenia a overenia sú vylúčené.

## Ak sa niečo pokazí
- **„The record changed. Your inputs are retained.“:** niekto uložil novšiu verziu. Pred novým pokusom výslovne zahoďte zmeny a znova načítajte.
- **„Save outcome unknown“:** polia sú zmrazené; použite **Zopakovať presný uložený príkaz**, nevypĺňajte znova.
- **„No current source-authorized human appointment is available for this record.“:** skontrolujte svoju rolu a kontext lode so správcom nájomcu (13.03 §10).
- **„Human conflict resolution required“:** offline záznam je v konflikte so serverom. Otvorte **Preskúmať konflikt** alebo Centrum konfliktov a vyberte výslovne; nič sa nezlúči automaticky (13.03 §10).
- **Denné zdroje nie sú dostupné alebo je prístup odmietnutý:** použite **Zopakovať načítanie zdrojov**; ak problém pretrváva, nahláste podpore čas a vaše kroky.

## Súvisiace stránky
