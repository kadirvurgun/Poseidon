---
key: regulatory-review
title: Regulačné preskúmanie SME
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Regulačné preskúmanie SME zaznamenáva kampane, v ktorých ľudský expert posudzuje položky zdrojov a uplatniteľnosti lode alebo remorkéra. Odpovedá na otázku: „Ktoré položky zdrojov a požiadaviek pre toto plavidlo potvrdil oprávnený posudzovateľ a ktoré sú stále otvorené a sledované?“ Je to iba záznam ľudského posúdenia: systém neschvaľuje žiadnu požiadavku, neaktivuje žiadne pravidlo ani povolenie a netvrdí žiadne schválenie klasifikačnou spoločnosťou ani typové schválenie (13.03 §8).

## Kto ju používa
- **Regulatory / Assurance user:** otvára kampane posúdenia, zaznamenáva výroky k položkám a uzatvára kampaň (13.03 §8).
- **Vymenovaný posudzovateľ (SME):** potvrdzuje alebo sleduje položky v rámci svojej vlastnej autority zdroja.
- **DPA / HSEQ:** sleduje otvorené a nesledované položky (13.03 §4).

## Skôr než začnete
- Zobrazenie vyžaduje čítanie regulačného posúdenia. Otváranie a uzatváranie kampaní vyžaduje oprávnenie na správu; zaznamenávanie výrokov vyžaduje oprávnenie na výroky a platné vymenovanie za posudzovateľa pre tento rozsah.
- Na kartách **Rozsah kontroly** vyberte **Loď** alebo **Remorkér** a potom otvorte správne plavidlo v časti **Vyberte plavidlo** (13.03 §1).
- Zdroje by mali byť zaregistrované ako oficiálne a verziované na stránke [Koncepty zdrojov](/app/sources) (13.03 §8).
- V režime offline môžu byť údaje zastarané a príkazy sú vypnuté.

## Krok za krokom
1. Vyberte rozsah a plavidlo. Ak neexistuje žiadna kampaň, stlačte **Otvoriť kontrolnú kampaň**.
2. V časti **Matica vlastníctva zdrojov** posúďte pri každej autorite zdroja kategóriu, počty otvorených a potvrdených položiek a zodpovedné roly.
3. Vyberte položku v časti **Položky na posúdenie**.
4. Vo formulári **Zaznamenať výrok kontroly** vyplňte **Vymenovanie kontrolóra**, **Výrok**, **Autorita zdroja** a **Zodpovedná rola**. Ak položka nemá zaregistrovaný zdroj, zadajte **Odkaz na autoritatívny zdroj** a **Verzia / vydanie zdroja**. Pri položke, ktorá zostáva otvorená, zadajte **ID sledovanej úlohy**.
5. Do poľa **Poznámka kontrolóra** napíšte dôvod s dĺžkou 10–2000 znakov a stlačte **Zaznamenať**.
6. Keď sú všetky položky vybavené, vyberte v časti **Uzavrieť kampaň** možnosť **Výsledok uzavretia**, napíšte **Dôvod uzavretia** a stlačte **Uzavrieť kampaň**.

## Čo znamenajú stavy
- Kampaň: **Otvorené**, **V kontrole**, **Uzavretá s otvorenými položkami**, **Uzavreté**.
- Výrok: **Potvrdené**, **Neuplatniteľné (potvrdené)**, **Vyžaduje posúdenie**, **Nejednoznačné**. Keď sú kritické údaje neznáme, položka zostáva **Vyžaduje posúdenie**; neznáma položka sa nikdy nepočíta ako „Neuplatňuje sa“.
- **Kandidát (neoverený):** zdroj ešte nie je viazaný na overený oficiálny zdroj.
- **Zastarané — väzba zmenená** a **Profil plavidla sa zmenil:** zdroj alebo profil plavidla sa po výroku zmenil; položka sa musí posúdiť znova.
- **Nesledované otvorené položky:** otvorené položky bez sledovanej úlohy. Kým je ich počet vyšší ako nula, kampaň nemožno uzavrieť.
- Kategórie **Zákon**, **Pravidlo klasifikačnej spoločnosti**, **SMS spoločnosti** a **Pokyn — nie je zákon** sa nikdy navzájom nenahrádzajú (13.03 §8).

## Čo nemôžete robiť / upozornenie
- Osoba, ktorá kampaň otvorila, v nej nemôže zaznamenávať výroky; bez vymenovania za posudzovateľa sú výroky iba na čítanie.
- Posudzovateľ, ktorého kvalifikácia je neoverená, môže sledovať otvorené položky, ale nemôže potvrdzovať. Zastaranú položku nemožno potvrdiť.
- Potvrdenie vyžaduje zaregistrovaný zdroj alebo odkaz na autoritatívny zdroj s jeho vydaním. V reťazci Požiadavka → Kontrola → Dôkaz → Overenie nenechávajte žiadny osirelý záznam ani záznam bez zdroja (13.03 §8).
- **Uzavreté** vyžaduje, aby bola každá položka potvrdená; inak uzavrite s otvorenými položkami, z ktorých každá je sledovaná.
- V poznámke neuvádzajte schválenie klasifikačnou spoločnosťou ani typové schválenie. V rozsahu remorkérov nie je žiadny limit vlečenia bez riadeného zdroja zákonnou predvolenou hodnotou.
- Výroky zaznamenávajú iba ľudskí používatelia; účty AI a automatizácie ich zaznamenať nemôžu.

## Ak sa niečo pokazí
- **Záznam sa zmenil. Pred pokračovaním ho znova načítajte a skontrolujte.** Stlačte **Znova načítať**.
- **Výsledok je neistý. Opakovanie odošle rovnaký príkaz.** Použite **Skúsiť znova**.
- **Pravidlá kontroly tento príkaz odmietli.** Skontrolujte, či položka nie je zastaraná, odkaz na zdroj a sledovanú úlohu.
- **K regulačnej kontrole tohto plavidla nemáte prístup.** alebo **Na túto akciu nemáte oprávnenie.** Skontrolujte svoju rolu a rozsah; v prípade potreby sa obráťte na správcu nájomcu (Tenant Admin) (13.03 §10).
- Ak uplatniteľnosť vyzerá neočakávane, skontrolujte zdroj a vstupy kontextu a obráťte sa na vlastníka kontroly alebo odborného experta (SME) (13.03 §10).

## Súvisiace stránky
