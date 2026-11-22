---
key: departure-signoff
title: Pripravenosť na vyplávanie a rozhodnutie kapitána
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: [1, 2, 3, 4, 5, 6, 7, 9]
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka zobrazuje **poradné posúdenie pripravenosti** pre vybranú loď a plánované obdobie vyplávania a zaznamenáva rozhodnutie vymenovaného kapitána. Odpovedá na otázku: „Čo hovoria zdroje o posádke, technickom stave, zákonných požiadavkách, núdzovej pripravenosti a bezpečnostných udalostiach pre toto obdobie a prečo?“ Rozhodnutie vyplávanie uvoľniť, pozdržať alebo zastaviť patrí **kapitánovi** (13.03 §2). Systém nikdy neuvoľňuje vyplávanie; zaznamenané rozhodnutie nie je povolením na vyplávanie.

## Kto ju používa
- **Kapitán:** posúdi hodnotenie a zoznam „Prečo“ a potom zaznamená Uvoľniť, Pozdržať alebo Zamietnuť s dôvodom (13.03 §2).
- **Pobrežné operácie / Technický inšpektor:** sledujú hodnotenie a zaznamenané rozhodnutia kapitána iba na čítanie. Pobrežný personál nemôže zaznamenať rozhodnutie namiesto kapitána.

## Skôr než začnete
- V hornom kontextovom paneli skontrolujte, že je vybraná **správna spoločnosť**. Stránka uvádza iba lode vo vašom rozsahu (13.03 §1).
- Na zobrazenie stránky potrebujete prístup na čítanie pripravenosti na vyplávanie.
- Samotné oprávnenie rozhodovať na zaznamenanie rozhodnutia nestačí: potrebujete aj **platné vymenovanie za kapitána** pre túto loď. Bez neho stránka uvádza „You hold no current Master appointment for this ship. Decisions are read-only.“

## Krok za krokom
1. Vyberte loď zo zoznamu **Vyberte loď**.
2. V časti **Plánované obdobie vyplávania** vyplňte **Dátum vyplávania** a **Koniec plavby** a stlačte **Vyhodnotiť pripravenosť**.
3. V riadku **Hodnotenie** si prečítajte odznak stavu, režim **Aktuálne** alebo **Predpoveď**, **Vyhodnotené** a **Platné do**.
4. V tabuľke kategórií skontrolujte výsledky kategórií Posádka, Technické, Zákonné, Núdzová pripravenosť a Bezpečnostné udalosti.
5. Prečítajte si zoznam **Prečo**. Každý riadok zobrazuje pozorovanie, ktoré nevyhovelo, jeho kód dôvodu a jeho zdroj. Neočakávaný riadok skontrolujte na vlastnej stránke zdroja.
6. Ak hodnotenie nie je aktuálne a v stave Pripravené, prečítajte si **Prečo nie je Uvoľniť dostupné**. Uvádza, kedy je obdobie predpoveďou, vymenúva kategórie, ktoré zatiaľ nevyhovujú, s ich výsledkami, a vysvetľuje, ktorý záznam by uzavrel medzery v zdrojoch kategórie Núdzová pripravenosť (zatiaľ nie je pripojený žiadny zdroj cvičení 61.02) a Zákonné (nie je definovaná žiadna sada požadovaných osvedčení). Iba vysvetľuje kategórie servera; nemení bránu.
7. V časti **Rozhodnutia kapitána** si pozrite skôr zaznamenané rozhodnutia a ich platnosť.
8. Ak ste kapitán, v časti **Zaznamenať rozhodnutie kapitána** vyberte Uvoľniť, Pozdržať alebo Zamietnuť, v poli **Vymenovanie** vyberte svoje vymenovanie, napíšte **Dôvod** (10–2000 znakov) a stlačte **Zaznamenať**.
9. Pomocou **Obnoviť** znova načítate hodnotenie.

## Čo znamenajú stavy
- **Pripravené:** žiadny zaznamenaný zdroj nemá pozorovanie, ktoré by nevyhovelo. Je to iba informácia; rozhodnutie o vyplávaní stále patrí kapitánovi.
- **Podmienené:** aspoň jeden zdroj hlási podmienku. Pre toto hodnotenie sa Uvoľniť neponúka.
- **Nepripravené:** aspoň jeden zdroj vracia „Nevyhovuje“. To, že ostatné kategórie vyhovujú, to nevyvažuje.
- **Vyžaduje posúdenie:** aspoň jeden vstup je „Neznáme“. Oprávnená osoba sa naň musí pozrieť.
- **Vyhovuje / Podmienka / Nevyhovuje / Neznáme:** výsledok kategórie v tabuľke kategórií. **Neznáme** znamená, že zdroj nebolo možné prečítať alebo nemá údaje; neznamená „Vyhovuje“ ani „Pripravené“. Pre kategóriu Núdzová pripravenosť nie je v tejto verzii pripojený žiadny zdroj núdzovej pripravenosti, preto tento riadok zobrazuje Neznáme. Kategória Zákonné môže zostať Neznáme, aj keď je každé zaznamenané osvedčenie platné, pretože žiadny zdroj nedefinuje sadu požadovaných osvedčení pre loď. Kategória Zákonné zahŕňa aj signály dôveryhodnosti údajov (DATA_TRUST_*); bez prístupu na čítanie dôveryhodnosti údajov zobrazuje Zákonné hodnotu Neznáme.
- **Predpoveď:** obdobie nezahŕňa dnešok. Je to iba plánovacia predpoveď a nemožno ju uvoľniť.
- **Aktuálne / Zastarané — vstupy sa zmenili:** platnosť zaznamenaného rozhodnutia. Keď sa vstupy zmenia, skoršie rozhodnutie sa stane zastaraným a neodstráni sa.

## Čo nemôžete robiť / upozornenie
- Systém nikdy neuvoľňuje vyplávanie a žiadny stav nezobrazuje ako povolenie na vyplávanie. Zaznamenané rozhodnutie je vlastný ľudský záznam vymenovaného kapitána.
- Uvoľniť možno zaznamenať iba pri aktuálnom hodnotení READY; každá kategória musí vyhovovať. Keďže kategória Núdzová pripravenosť zostáva v tejto verzii Neznáme, hodnotenie nemôže dosiahnuť Pripravené a Uvoľniť sa neponúka; **Prečo nie je Uvoľniť dostupné** ukazuje prečo. Aj pri stave Pripravené je rozhodnutie o uvoľnení na kapitánovi; systém nič neschvaľuje. „Pozdržať a Zamietnuť možno vždy zaznamenať s dôvodom.“
- Každé rozhodnutie vyžaduje dôvod. Zaznamenané rozhodnutia sa nemenia; nové rozhodnutie je nový záznam.
- Rozhodnutie nemôžete zaznamenať, kým je hodnotenie zastarané alebo ste offline; najprv obnovte.

## Ak sa niečo pokazí
- **Ste offline:** hodnotenie môže byť zastarané a rozhodnutie je vypnuté. Po opätovnom pripojení obnovte (13.03 §10).
- **Nemáte prístup k pripravenosti na vyplávanie pre toto plavidlo:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu.
- **Neočakávaná pripravenosť:** otvorte zoznam **Prečo**; ak zdroj vyzerá nesprávne, odovzdajte to vlastníkovi kontroly alebo odbornému expertovi.
- **Výsledok je neistý:** **Skúsiť znova** odošle ten istý príkaz. Nepíšte nové rozhodnutie.
- **História rozhodnutí sa zmenila / Hodnotenie sa zmenilo:** použite **Znova načítať**, posúďte aktuálne hodnotenie a potom rozhodnite.
- **Príkaz nebol prijatý:** nahláste to podpore so snímkou obrazovky, časom a vašimi krokmi.

## Súvisiace stránky
