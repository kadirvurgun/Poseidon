---
key: pilot-program
title: Pilotný program: týždne a správa o hodnote
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Pilotný program uchováva na jednom mieste pilotné týždne pre loď (a voliteľne remorkér), položky kontrolného zoznamu každého týždňa, problémy pilotu, dennú spätnú väzbu a správu o hodnote. Odpovedá na otázku: „Boli položky každého pilotného týždňa splnené skutočnými záznamami a ako sa namerané hodnoty zmenili oproti východiskovej hodnote?“ Platí upozornenie v hornej časti: „Software cannot complete a pilot. A checklist item is satisfied only by a link you make to a real record that the server verifies; a missing baseline stays Unknown; only the appointed Product Owner closes a week or records the program decision.“ (Softvér nemôže dokončiť pilot. Položka kontrolného zoznamu je splnená iba prepojením, ktoré vytvoríte na skutočný záznam overený serverom; chýbajúca východisková hodnota zostáva Neznáme; týždeň uzatvára alebo rozhodnutie programu zaznamenáva iba vymenovaný vlastník produktu.)

## Kto ju používa
- **Vlastník produktu:** uzatvára týždne a zaznamenáva rozhodnutie programu (Pokračovať, Opraviť, Zastaviť, Rozšíriť). Vyžaduje to platné vymenovanie za vlastníka produktu.
- **Pilotný tím:** prepája záznamy s položkami kontrolného zoznamu, hlási problémy, zaznamenáva spätnú väzbu a merania.
- **Vlastník triáže:** triedi a rieši problémy.
- 13.03 pre túto stránku nedefinuje samostatnú rolu; platia zásady správy nájomcu (13.03 §9).

## Skôr než začnete
- Zobrazenie vyžaduje čítanie v rámci celého nájomcu a čítanie pilotného programu. Vytváranie programov, zaznamenávanie, triáž a rozhodovanie vyžadujú každé vlastné oprávnenie; uzavretia týždňov a rozhodnutia vyžadujú aj vymenovanie za vlastníka produktu.
- Záznamy, ktoré prepájate (napríklad záznam osvedčenia, posádky alebo pracovného príkazu), musia už existovať na svojich vlastných stránkach.
- Do spätnej väzby nikdy nezadávajte mená, e-mailové adresy, telefónne čísla ani čísla dokladov; používajte iba pseudonym a rolu.
- Zaznamenávanie vyžaduje živé pripojenie.

## Krok za krokom
1. Otvorte program v časti **Pilotné programy**. Pre nový program vyplňte v časti **Vytvoriť pilotný program** polia **Názov programu**, **Loď**, **Remorkér (voliteľné)**, **Dátum začiatku 1. týždňa** a **Časové pásmo kalendára (IANA)** a stlačte **Zaznamenať**.
2. V každej časti **Týždeň** skontrolujte stĺpce **Položka kontrolného zoznamu**, **Kritérium (zdroj)**, **Oblasť**, **Stav** a **Prepojené záznamy**.
3. Ak chcete položku splniť, zadajte v časti **Prepojiť záznam** hodnoty **Položka kontrolného zoznamu**, **Typ záznamu** a **Identifikátor záznamu** a stlačte **Prepojiť záznam**. Nesprávne prepojenie odstránite pomocou **Stiahnuť prepojenie** po napísaní **Dôvod stiahnutia**.
4. V časti **Problémy pilotu** pomocou **Nahlásiť problém** zadajte oblasť, **Kategória**, závažnosť a **Zhrnutie problému**. Vlastník triáže použije **Triáž** s **Poznámka k triáži** a potom problém vyrieši.
5. V časti **Denná spätná väzba** pomocou **Zaznamenať spätnú väzbu** zadajte **Zhrnutie** a **Hodnotenie**.
6. V časti **Správa o hodnote** pomocou **Zaznamenať meranie** zadajte **Metrika**, **Fáza** (Východisková hodnota alebo Pilotné obdobie), **Hodnota**, **Veľkosť vzorky**, **Obdobie od**, **Obdobie do**, **Referencia dôkazu** a **Metóda a vzorka**.
7. Ak ste vlastník produktu, zaznamenajte výsledok a odôvodnenie v časti **Uzavrieť týždeň**. Na konci programu vyplňte v časti **Zaznamenať rozhodnutie programu** rozhodnutie, **Vymenovanie** a **Odôvodnenie** a stlačte **Zaznamenať**.

## Čo znamenajú stavy
- **Prepojené a overené:** k položke je prepojený skutočný záznam overený serverom. Položky **Prepojenie už neplatí** a **Chýba** nie sú splnené.
- **Completion required:** prepojený záznam musí byť v stave dokončenia. „Audit trace proves that a person wrote the record; it does not prove the record or flow is completed.“ (Auditná stopa dokazuje, že záznam napísala osoba; nedokazuje, že záznam alebo postup je dokončený.)
- **Pripravený na uzavretie človekom / Nepripravené:** odporúčanie pre týždeň; ako **Prijaté** alebo **Neprijatý** ho uzatvára iba vlastník produktu.
- **Triáž v ten istý deň / Triáž oneskorene / Triáž sa očakáva dnes / Po termíne:** načasovanie triáže problémov P0 voči kalendárnemu dňu programu. Pri problémoch so stavom **Zdroj neuvádza cieľ v ten istý deň** si systém nevymýšľa žiadny termín.
- **Namerané / Neznáme:** keď chýba východisková alebo pilotná hodnota, správa zostáva **Neznáme**; nikdy sa nepočíta ako nula ani ako zlepšenie. Hodnoty **Neporovnateľné** sa neporovnávajú.
- **Telemetria (deklarovaná, neoverená):** deklarovaná hodnota telemetrie.
- **Rozšírenie možné; čaká sa na rozhodnutie človeka / Odporúča sa oprava:** odporúčanie programu, nie rozhodnutie.

## Čo nemôžete robiť / upozornenie
- „Prijaté je možné iba vtedy, keď je každá položka overená a žiadna triáž P0 nie je otvorená, čakajúca ani oneskorená.“
- „Rozšíriť je možné iba vtedy, keď je každý týždeň prijatý a hodnoty, UAT, poruchy a P0 sú bez nedostatkov.“
- Ak ste autorom dôkazu, na ktorom stojí týždeň alebo rozhodnutie (prepojenia, spätná väzba, merania), nemôžete tento týždeň uzavrieť ani zaznamenať toto rozhodnutie.
- Pre fiktívny testovací (fixture) program nemožno zaznamenať žiadne uzavretie týždňa ani rozhodnutie.
- Systém nikdy nedokončuje pilot, nikdy neuzatvára týždeň a nikdy nevymýšľa rozhodnutia ani schválenia.

## Ak sa niečo pokazí
- **Nemáte platné menovanie Product Ownera; uzavretia a rozhodnutia sú len na čítanie.** Obráťte sa na správcu nájomcu (Tenant Admin) kvôli vymenovaniu (13.03 §10).
- **Zber spätnej väzby nie je dostupný, kým operátor nenastaví zásadu uchovávania.** Požiadajte správcu systému o nastavenie zásady uchovávania.
- **Záznam sa zmenil alebo už bol použitý. Znova načítajte.** Aktuálny stav otvorte pomocou **Znova načítať**.
- **Výsledok je neistý. Opakovanie odošle rovnaký príkaz.** Pomocou **Skúsiť znova** bezpečne znova odošlete ten istý príkaz.
- **Odmietnuté pravidlami.** Napríklad prepojený záznam nemusí existovať, môže byť mimo rozsahu alebo nemusí byť dokončený.
- **Nepovolené.** Ste autorom prepojení, spätnej väzby alebo meraní, na ktorých stojí toto uzavretie týždňa alebo rozhodnutie; musí ho zaznamenať iná osoba.
- Týždeň, ktorý sa ešte nezačal, nemožno uzavrieť (zobrazuje sa ako „Záznam sa zmenil alebo už bol použitý. Znova načítajte.“).
- **K týmto záznamom nemáte prístup.** Skontrolujte svoju rolu a rozsah nájomcu (13.03 §10).

## Súvisiace stránky
