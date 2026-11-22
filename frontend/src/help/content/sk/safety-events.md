---
key: safety-events
title: Bezpečnostné udalosti a skoronehody
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Bezpečnostné udalosti a skoronehody vám umožňuje rýchlo nahlásiť bezpečnostnú udalosť na lodi a sledovať ľudské kroky, ktoré po nej nasledujú: prvotné posúdenie, vyšetrovanie, nezávislé posúdenie, nápravné a preventívne opatrenia, overenie, účinnosť a uzavretie. Odpovedá na otázku: „Čo sa stalo, kto to rieši a zabralo opatrenie naozaj?“ Nič tu automaticky nerozhoduje o závažnosti, hlavnej príčine ani uzavretí; každé z toho je samostatný ľudský záznam. Stránka sleduje reťazec Zistenie → Opatrenie → Overenie a **uzavreté nie je to isté ako účinné** (13.03 §4).

## Kto ju používa
- **Posádka / OOW / strojník a kapitán:** hlásia skoronehody, incidenty, nehody, únavu, environmentálne alebo kybernetické udalosti (13.03 §2, §6).
- **DPA / HSEQ:** ako bezpečnostný most sledujú kritické udalosti a oneskorené opatrenia a riadia vyšetrovanie a účinnosť opatrení (13.03 §4).
- Prvotné posúdenie, vyšetrovanie, posúdenie, čítanie identity, zápis opatrení, overenie a posúdenie účinnosti sú samostatné oprávnenia; každý používateľ vidí iba tie časti, na ktoré má oprávnenie.

## Skôr než začnete
- Zvoľte správnu loď (**Vyberte plavidlo**). Bez prístupu uvidíte „K bezpečnostným udalostiam tejto lode nemáte prístup.“
- Hlásenie vyžaduje oprávnenie na hlásenie; čítanie zoznamu vyžaduje oprávnenie na čítanie.
- Hlásenie **Dôverne** a **Odoslať hlásenie** vyžadujú spojenie. Offline môžete uložiť iba menovitý koncept, a to iba na zariadení pripravenom vopred.
- Čas sa zadáva v UTC.

## Krok za krokom
1. Udalosti lode si pozrite v zozname **Udalosti**. Pomocou **Iba moje hlásenia** uvidíte len svoje vlastné.
2. Pre nový záznam stlačte **Nahlásiť udalosť**. Ako **Spôsob hlásenia** zvoľte **Menovite** alebo **Dôverne**. Pre dôverné hlásenie, ktoré potrebuje ďalšiu komunikáciu, zvoľte **Chcem súkromný kód pre ďalšiu komunikáciu**.
3. Vyplňte **Typ udalosti**, **Kedy sa to stalo?**, **Operácia**, **Miesto na palube**, **Zhrnutie**, **Čo sa stalo**, **Bolo prijaté okamžité opatrenie?** a voliteľne **Váš odhad závažnosti**. Ak čas nie je známy, zvoľte **Neznáme** a vyplňte **Prečo nie je čas známy?**.
4. Kým je hlásenie neúplné, môžete ho **Uložiť koncept**. Keď je **Pred odoslaním je ešte potrebné** prázdne, stlačte **Odoslať hlásenie**. Neodoslaný koncept je možné stiahnuť pomocou **Stiahnuť koncept** s uvedením dôvodu.
5. Ak bol vydaný kód pre ďalšiu komunikáciu, kód v **Váš dôverný kód pre ďalšiu komunikáciu** si ihneď súkromne uložte a stlačte **Kód som si uložil(a)**. Zobrazí sa iba raz. Neskôr ho zadajte ako **Kód pre ďalšiu komunikáciu** v časti **Dôverná ďalšia komunikácia** a stlačte **Otvoriť**, aby ste si prečítali odpovede tímu.
6. Osoba vykonávajúca prvotné posúdenie zaznamená v **Prvotné posúdenie** závažnosť a **Vyžaduje sa vyšetrovanie** s dôvodom a stlačí **Zaznamenať triedenie**, alebo hlásenie vráti pomocou **Vrátiť oznamovateľovi**.
7. Vyšetrovateľ stlačí **Začať vyšetrovanie**, vyplní **Prispievajúce faktory**, **Hlavné príčiny**, **Bariéry** a **Neistoty**, potom stlačí **Uložiť vyšetrovanie** a **Odoslať na posúdenie**. V **Nezávislé posúdenie** iná osoba zvolí **Schváliť**, **Vrátiť** alebo **Zamietnuť**.
8. V **Nápravné a preventívne opatrenia** použite **Pridať opatrenie** so zodpovednou osobou, termínom a zistením, ktoré rieši. Zodpovedná osoba zaznamená **Nahlásiť dokončenie**, iná osoba zaznamená **Overiť** a tretia zaznamená **Posúdiť účinnosť**.
9. Keď je **Prekážky uzavretia** prázdne, posudzovateľ (oprávnenie na posúdenie), ktorý nie je oznamovateľom ani autorom vyšetrovania, stlačí **Uzavrieť udalosť**. V **Správy** môžete posielať správy s viditeľnosťou **Iba tím** alebo **Zdieľané s oznamovateľom**.

## Čo znamenajú stavy
- **Koncept / Odoslané / Vrátené / Roztriedené / Vyšetruje sa / Sledovanie opatrení / Uzavreté / Stiahnuté:** životný cyklus udalosti.
- **Závažnosť — Netriedené / Neposúdené:** zatiaľ neexistuje ľudská klasifikácia. **Odhad oznamovateľa** je iba návrh. **Severity policy: not configured – human classification only** ukazuje, že systém neodvodzuje žiadnu závažnosť.
- **Potreba vyšetrovania je UNKNOWN:** nebolo prijaté žiadne rozhodnutie; neznamená to „nie“ a blokuje to uzavretie.
- **Opatrenie — Otvorené / Dokončené / Overené:** Dokončené nie je Overené a Overené nie je Účinné.
- **Účinnosť — Čaká / Účinné / Neúčinné / Nepreukázateľné / Žiadne opatrenia / Nezobrazené:** účinnosť je ľudské posúdenie pre každé opatrenie a je oddelená od uzavretia. **Žiadne opatrenia** neznamená účinné.
- Súhrn **Otvorené bezpečnostné udalosti**: **Otvorené udalosti s vysokou závažnosťou**, **Vyžaduje posúdenie** alebo **Žiadna otvorená udalosť s vysokou závažnosťou**. Je to iba varovanie; neposkytuje žiadne rozhodnutie o pripravenosti.
- **Dôverné (chránené):** identita oznamovateľa je v obmedzenom trezore a čitateľom sa nezobrazuje.
- **Zaznamenané offline / Čaká na synchronizáciu / Uložené v tomto zariadení. Zatiaľ neodoslané.** Zaradené do frontu neznamená odoslané.

## Čo nemôžete robiť / upozornenie
- Oznamovateľ nemôže vykonať prvotné posúdenie vlastného hlásenia. Autor vyšetrovania, ten, kto ho odoslal, ani oznamovateľ nemôže vykonať nezávislé posúdenie.
- Dokončenie hlási iba zodpovedná osoba opatrenia. Overiť nemôže ani zodpovedná osoba, ani ten, kto opatrenie dokončil; účinnosť nemôže posúdiť nikto zo zodpovednej osoby, toho, kto dokončil, ani overovateľa. Sú to samostatné záznamy rôznych osôb.
- Hlavné príčiny sú ľudské vyhlásenia; systém nikdy žiadnu nenavrhuje.
- Odoslané hlásenie nemôže jeho autor nikdy stiahnuť ani vymazať.
- V dôvernom hlásení sa identita otvára iba na základe potreby vedieť, osobou s oprávnením na čítanie identity, ktorá zvolí **Účel**; každé čítanie sa zaznamená spolu s účelom. Anonymné hlásenie vyžaduje verejný kanál príjmu, ktorý nie je nastavený.
- Posúdenie Nepreukázateľné je možné zopakovať; Účinné a Neúčinné sú konečné; opatrenie, ktoré nezabralo, vyžaduje nové následné opatrenie.

## Ak sa niečo pokazí
- **The server refused the command:** doplňte chýbajúce alebo blokujúce položky uvedené pod ním.
- **Záznam sa medzitým zmenil. Pred ďalším pokusom ho znova načítajte a skontrolujte.** Stlačte **Znova načítať**.
- **Výsledok je neznámy (spojenie stratené).** Použite **Zopakovať rovnaký príkaz**; nepoužije sa dvakrát.
- **Táto akcia vám nie je povolená.** Skontrolujte svoju rolu, loď a kontext objektu; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).
- **The device key is unavailable** alebo **Online re-authorisation is needed:** zaznamenané údaje sa uchovávajú; po pripojení sa znova overte. Pri chybe aplikácie kontaktujte podporu s ID korelácie, obrazovkou, časom a krokmi (13.03 §10).

## Súvisiace stránky
