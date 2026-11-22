---
key: requirements
title: Koncepty požiadaviek
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Koncepty požiadaviek uchováva **požiadavky** odvodené z revízie zdroja ako verzované koncepty a viaže každú požiadavku na presnú revíziu zdroja. Odpovedá na otázku: „Na ktorej revízii ktorého zdroja táto požiadavka stojí a aký je jej zaznamenaný životný cyklus?“ Požiadavka je prvým článkom reťazca Požiadavka → Kontrola → Doklad → Overenie. Žiadny záznam v tomto reťazci by nemal zostať neprepojený (osirelý) (13.03 §8).

## Kto ju používa
- **Používateľ Regulatory / Assurance:** viaže požiadavku na jej zdroj, hodnotí uplatniteľnosť pomocou kontextových vstupov a udržiava reťazec úplný (13.03 §8).
- **DPA / HSEQ:** číta základ požiadavky pri práci na zisteniach a auditoch (13.03 §4).

## Skôr než začnete
- Zobrazenie vyžaduje čítanie v rozsahu celého nájomcu (tenant) a čítanie regulačných konceptov; úpravy vyžadujú navyše zápis regulačných konceptov.
- Podzobrazenia vyžadujú vlastné oprávnenia, vždy s rozsahom čítania celého nájomcu. **Životný cyklus a uplatniteľnosť** vyžaduje čítanie uplatniteľnosti; úprava pravidiel vyžaduje zápis pravidiel, žiadosť o posúdenie vyžaduje posúdenie pravidiel a hodnotenie vyžaduje hodnotenie uplatniteľnosti; kvalifikácia a potvrdenie faktov vyžadujú regulačnú kvalifikáciu alebo potvrdenie faktov a navyše vymenovanie zaznamenané na serveri. **Kontroly a doklady** vyžaduje čítanie kontrol, čítanie dokladov, čítanie súborov a čítanie uplatniteľnosti; prepojenie vyžaduje zápis kontrol alebo zápis dokladov s právom úprav na lodi a schválenie vyžaduje schválenie kontrol.
- Najprv zaznamenajte zdroj na stránke **Koncepty zdrojov**. Požiadavku je možné naviazať iba na existujúcu revíziu zdroja.
- Uloženie vyžaduje živé spojenie so serverom; offline sa úpravy uchovávajú na karte.

## Krok za krokom
1. Pozrite si koncepty požiadaviek v zozname; vyhľadávajte pomocou **Search drafts** a **Hľadať**.
2. Pre novú požiadavku stlačte **Nový koncept**.
3. V **Draft fields** zvoľte pre každé pole **Stav** (**Chýba**, **Neznáme**, **Known (declared)**) a zadajte známe hodnoty: **Právny nástroj**, **Vyhlásenie verzie / zmeny**, **Deklarovaný dátum účinnosti**, **Vyhlásenie / pôvod**, **Čas pozorovania zdroja (s posunom UTC)**.
4. V **Immutable source citation** píšte do **Find a source** a stlačte **Search sources**. Vyberte zdroj v **Source draft**, potom presnú revíziu v **Exact revision**. Obsah zvolenej revízie skontrolujte v **Selected immutable source content**.
5. Stlačte **Uložiť koncept**. Keď je záznam otvorený, zobrazia sa sekcie **Zaznamenaný životný cyklus** a **Nemenná história**.
6. Použite **Životný cyklus a uplatniteľnosť** pre uplatniteľnosť a **Kontroly a doklady** pre prepojenia kontrol a dokladov. **Hlásenie kritických osirelých záznamov** v rámci **Kontroly a doklady** uvádza kritické vyhlásenia bez prepojenia na požiadavku; tieto záznamy prepojte s požiadavkou.

## Čo znamenajú stavy
- **Zaznamenaný životný cyklus:** stav, ktorý server odvodzuje z naviazaných pravidiel: **Koncept**, **Prijaté**, **Budúca účinnosť**, **Účinné**, **Nahradené**. Ak ho nie je možné prečítať, zobrazí sa **Nedostupné**; systém nepredpokladá koncept ani účinnosť.
- „Naviazané pravidlá zaznamenávajú rôzne stavy životného cyklu. Zobrazená hodnota je softvérová priorita; vyžaduje sa odborné posúdenie.“ Zobrazená hodnota vtedy nie je rozhodnutím; je potrebné odborné posúdenie.
- **Neznáme:** informácia nie je známa. Keď sú kritické údaje neznáme, výsledok uplatniteľnosti zostáva **Vyžaduje posúdenie** (REVIEW_REQUIRED); nikdy sa nepočíta ako „neuplatňuje sa“ ani „nie“ (13.03 §8).
- **Vyžaduje sa posúdenie uplatniteľnosti / Chýba väzba na požiadavku / Overenie nevykonané:** otvorené položky v reťazci kontrola – doklad.
- **Dokončenie neznáme · Neoverené · Účinnosť neznáma:** dokončenie, overenie a účinnosť sú samostatné stavy. Dokončený záznam nie je overený a overený záznam nie je účinný.

## Čo nemôžete robiť / upozornenie
- Požiadavku nemôžete naviazať na „najnovší“ zdroj; vždy sa volí **Exact revision**. Keď zdroj dostane novú revíziu, stará väzba zostáva v histórii.
- Náhľad uplatniteľnosti nezakladá žiadny súlad, pripravenosť, povolenie ani právne schválenie: „NÁHĽAD / KONCEPT. Predpovede nezakladajú súlad, pripravenosť, povolenia ani právne schválenie.“
- Zmena životného cyklu prechádza cez **Vyžiadať ľudské posúdenie**; rozhoduje prijatie zdroja a kvalifikovaný nezávislý posudzovateľ. Systém ju nikdy neuzavrie sám.
- Nepredpokladajte, že zdroje pravidiel klasifikačnej spoločnosti, vlajky, spoločnosti a miestne pravidlá sa navzájom zastupujú (13.03 §8).

## Ak sa niečo pokazí
- **Source selection unavailable.** Stlačte **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Otvorte aktuálnu revíziu pomocou **Znova načítať uložený koncept**.
- **Check field states, bounds, dates and source revision.** Ak ste zvolili zdroj, zvoľte aj jeho presnú revíziu.
- **History unavailable. Editor retained.** Stlačte **Retry history**.
- Ak je životný cyklus neočakávaný, skontrolujte naviazané pravidlá a revízie zdrojov; v prípade potreby kontaktujte zodpovednú osobu kontroly alebo odborníka (SME) (13.03 §10).
- Bez oprávnenia skontrolujte svoju rolu a rozsah nájomcu a požiadajte Tenant Admin (13.03 §10).

## Súvisiace stránky
