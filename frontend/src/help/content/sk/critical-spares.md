---
key: critical-spares
title: Kritické náhradné diely a požiadavky
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka kritických náhradných dielov a požiadaviek zobrazuje pre vybrané plavidlo **deklarovanú zásobu, minimum a maximum, upozornenia na nízku zásobu a návrhy technických požiadaviek**. Odpovedá na otázku: „Ktorý kritický náhradný diel dosiahol svoje minimum, koľko otvorených pracovných príkazov naň odkazuje a bola technická požiadavka navrhnutá a rozhodnutá?“ Počty sú ľudské vyhlásenia a nie sú overené voči skladu.

## Kto ju používa
- **Hlavný strojník / Technický inšpektor:** sledujú minimálne prahy kritických náhradných dielov a začínajú technickú požiadavku (13.03 §5). Osoba, ktorá požiadavku navrhne, a osoba, ktorá ju schváli, sú rôzne osoby.

## Skôr než začnete
- Zobrazenie vyžaduje `spare.read`. Definovanie náhradných dielov a zaznamenávanie počtov vyžaduje `spare.write`, navrhnutie požiadavky `requisition.propose` a rozhodnutie `requisition.approve`.
- Návrhy a rozhodnutia vyžadujú prihláseného ľudského používateľa.
- Vyberte správne plavidlo zo zoznamu **Plavidlo**.

## Krok za krokom
1. Vyberte plavidlo. Otvorí sa zobrazenie **Kritické náhradné diely**. Hore si prečítajte počty **Nízka zásoba** a **Vyžaduje posúdenie**.
2. V každom riadku tabuľky si prečítajte **Kritickosť dielu**, **Kritickosť zariadenia**, **Stav zásoby**, **Predpoklad**, **Min. / max.** a **Otvorené pracovné príkazy**.
3. Pre nový náhradný diel stlačte **Nový kritický náhradný diel**, pre existujúci **Upraviť**. Zadajte **Číslo dielu**, **Názov**, **Jednotka**, **Minimálna zásoba** a **Maximálna zásoba**. Ak prahy nie sú rozhodnuté, nechajte ich prázdne.
4. Ak chcete zadať počet, zaškrtnite **Zaznamenať novú inventúru**, vyplňte **Spočítané množstvo** a stlačte **Uložiť**.
5. V riadku **Nízka zásoba** bez otvorenej požiadavky stlačte **Navrhnúť požiadavku**. Server vypočíta navrhované množstvo (maximum mínus predpokladaná zásoba). Ak nie je nastavené maximum, žiadne množstvo sa nenavrhne a schvaľovateľ musí zadať **Schválené množstvo**.
6. V zobrazení **Technické požiadavky** oprávnená osoba, ktorá návrh nevytvorila, skontroluje **Schválené množstvo** a stlačí **Schváliť na export do ERP**, alebo vyberie **Dôvod zamietnutia** a stlačí **Zamietnuť**.
7. Na schválenej požiadavke otvorte **Exportná referencia ERP**, ak chcete vidieť referenciu.

## Čo znamenajú stavy
- **Nízka zásoba:** predpokladaná zásoba (posledný deklarovaný počet mínus náhradné diely zaznamenané na pracovných príkazoch odoslaných od tohto počtu) je na úrovni minima alebo pod ním. V tomto riadku sa ponúka návrh požiadavky.
- **Nad minimom (deklarované):** predpokladaná zásoba je nad minimom; počet nie je overený.
- **Minimum nenastavené:** nebol zadaný žiadny prah. Prázdny prah sa vykazuje ako vyžadujúci posúdenie, nikdy ako v poriadku.
- **Zásoba neznáma / Spotreba nevyriešená:** zásobu nebolo možné vypočítať. Neznáme neznamená „dostatočná zásoba“.
- **Navrhnuté / Schválené na export do ERP / Zamietnuté:** stav technickej požiadavky.
- **Kritický (deklarované) / Nekritický (deklarované) / Neznáme:** kritickosť náhradného dielu je vyhlásenie.

## Čo nemôžete robiť / upozornenie
- O požiadavke, ktorú ste navrhli, nemôžete rozhodnúť. Stránka uvádza „You proposed this; another person must decide.“
- Návrh a schválenie sú samostatné ľudské úkony. Schválenie vytvorí iba exportnú referenciu ERP; nákupná objednávka, dodávateľ ani cena sa tu nespravujú.
- Stránka nemá samostatné pole prahu na opätovnú objednávku; nízka zásoba sa posudzuje voči minimálnej zásobe.
- V režime offline nemožno údaje obnoviť ani vykonať žiadnu akciu.

## Ak sa niečo pokazí
- **Záznam sa zmenil:** pomocou **Skúsiť znova** obnovte stránku a skúste to znova.
- **Výsledok neznámy:** **Zopakovať ten istý príkaz** odošle ten istý príkaz.
- **Požiadavka bola zamietnutá pravidlom:** skontrolujte stav zásoby a informácie o otvorenej požiadavke.
- **Nemáte oprávnenie:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).

## Súvisiace stránky
