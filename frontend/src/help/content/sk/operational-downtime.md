---
key: operational-downtime
title: Prevádzková odstávka
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka zaznamenáva **deklarované prevádzkové prerušenia** lode (odstávky) spolu s ich pôvodom zdroja. Odpovedá na otázku: „Aké prerušenie bolo na tejto lodi deklarované, kedy, z akej príčiny a s ktorou poruchou alebo pracovným príkazom je prepojené a kde je jeho posúdenie a uzavretie?“ Stránka nerozhoduje o nároku zo straty nájmu (loss of hire), o bezpečnej prevádzke ani o žiadnom bezpečnostnom uvoľnení.

## Kto ju používa
- **Hlavný strojník / Technický inšpektor:** zaznamenávajú odstávku spôsobenú technickou poruchou alebo údržbou a prepájajú ju s históriou poruchy alebo pracovného príkazu (13.03 §5).
- **Kapitán:** sleduje prevádzkový dopad odstávky a posudzuje ju, ak je vymenovaný (13.03 §2).
- **Vymenovaný posudzovateľ (kapitán, prvý dôstojník, hlavný strojník, manažér flotily, DPA):** overuje záznam odstávky, vracia ho na opravu alebo ho uzatvára.

## Skôr než začnete
- V hornom kontextovom paneli skontrolujte, že je vybraná **správna spoločnosť**; stránka uvádza iba lode vo vašom rozsahu.
- Čítanie vyžaduje prístup na čítanie odstávok; otvorenie konceptu vyžaduje prístup na zápis. Akcie posúdenia vyžadujú aj prístup na posudzovanie a platné vymenovanie do funkcie pre túto loď.
- Na otvorenie prepojenej odstávky môžete na stránke poruchy alebo pracovného príkazu použiť **Otvoriť prepojený koncept odstávky**.

## Krok za krokom
1. Vyberte loď zo zoznamu **Vybrať loď**. Otvorí sa zoznam **Zaznamenané prevádzkové prerušenia**.
2. Pomocou **Vyhľadať príčiny na tejto strane** zúžite záznamy na tejto strane.
3. Pre nový záznam stlačte **Nový koncept odstávky**.
4. Pri každom poli najprv vyberte stav hodnoty: **Chýba**, **Neznáme** alebo **Deklarovaná hodnota**. Hodnotu zadávate iba vtedy, keď je vybraná **Deklarovaná hodnota**.
5. Do polí **Začiatok prerušenia** a **Koniec prerušenia** napíšte úplnú časovú značku so sekundami a výslovným posunom UTC.
6. Stlačte **Uložiť neposúdený koncept**. Neúplný koncept možno uložiť.
7. V časti **Kontrola a uzavretie odstávky** v prípade potreby prepojte dôkaz k nároku pomocou **Vyberte dôkaz** a potom stlačte **Odoslať na kontrolu**.
8. Ak ste vymenovaný posudzovateľ, vyberte svoje **Vymenovanie** a použite **Overiť**, **Vrátiť na opravu** alebo **Uzavrieť**.
9. Skoršie revízie posúďte v časti **Nemenná história konceptu**.

## Čo znamenajú stavy
- **Koncept / Vyžaduje kontrolu:** zaznamenaný obsah je neposúdený. Dôvody sú uvedené v časti **Prečo tento koncept zostáva neposúdený**.
- **Neodoslané / Otvorené / V kontrole:** stav životného cyklu záznamu.
- **Overené (človekom):** overila ho vymenovaná osoba iná ako tá, ktorá záznam zaznamenala. Dokončené nie je Overené (13.03 §1).
- **Uzavreté:** odstávka bola uzavretá. Overené uzavretie nie je bezpečnostným uvoľnením.
- **Neznáme:** hodnota nebola deklarovaná alebo ju nebolo možné určiť. Neznáme neznamená „Nie“ ani „Neuplatňuje sa“. Z tohto dôvodu strata nájmu a uzavretie zostávajú „Neznáme“.
- **Kandidát Loss of Hire:** iba kandidát; neuvádza žiadny nárok ani sumu.
- **Zaznamenaný interval:** neposúdené technické pozorovanie; nenahrádza deklarované minúty.

## Čo nemôžete robiť / upozornenie
- Kto odstávku zaznamenal, nemôže ju overiť ani uzavrieť; musí to urobiť iná vymenovaná osoba.
- Odoslanie, overenie a uzavretie nie sú možné, kým chýbajú povinné skutočnosti.
- Prepojenie dôkazu k nároku nie je overenie.
- Z vlastného zdroja (porucha, pracovný príkaz) sa nekopíruje žiadny popis, čas ani rozhodnutie o uzavretí; uchováva sa iba prepojenie na presnú revíziu.
- V režime offline nemožno čítať aktuálne zdroje a offline úložisko neexistuje; vaše zápisy zostávajú iba na otvorenej stránke.

## Ak sa niečo pokazí
- **Výsledok uloženia je neznámy:** váš pôvodný príkaz je chránený. S tým istým účtom použite **Zopakovať presný príkaz odstávky**.
- **Uložený koncept sa zmenil:** niekto iný zmenil záznam. Vaše zápisy zostanú zachované; na opätovné načítanie použite **Načítať aktuálny koncept** alebo zmeny zahoďte.
- **Záznamy odstávok alebo aktuálny prístup nie sú dostupné:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Zdroj sa zmenil:** pre nový koncept použite aktuálnu revíziu vlastného zdroja.
- **Príkaz nebol prijatý:** nahláste to podpore so snímkou obrazovky, časom a vašimi krokmi.

## Súvisiace stránky
