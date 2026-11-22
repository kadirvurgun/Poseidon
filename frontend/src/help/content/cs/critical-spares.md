---
key: critical-spares
title: Kritické náhradní díly a požadavky
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka kritických náhradních dílů a požadavků zobrazuje pro vybrané plavidlo **deklarovanou zásobu, minimum a maximum, upozornění na nízkou zásobu a návrhy technických požadavků**. Odpovídá na otázku: „Který kritický náhradní díl dosáhl svého minima, kolik otevřených pracovních příkazů na něj odkazuje a byl navržen a rozhodnut technický požadavek?“ Počty jsou lidské deklarace a nejsou ověřeny oproti skladu.

## Kdo ji používá
- **Vrchní strojník / Technický inspektor:** sledují minimální prahy kritických náhradních dílů a zahajují technický požadavek (13.03 §5). Osoba, která požadavek navrhne, a osoba, která ho schválí, jsou různé osoby.

## Než začnete
- Zobrazení vyžaduje `spare.read`. Definování náhradních dílů a zaznamenávání počtů vyžaduje `spare.write`, navržení požadavku `requisition.propose` a rozhodnutí `requisition.approve`.
- Návrhy a rozhodnutí vyžadují přihlášeného lidského uživatele.
- Vyberte správné plavidlo ze seznamu **Plavidlo**.

## Krok za krokem
1. Vyberte plavidlo. Otevře se zobrazení **Kritické náhradní díly**. Nahoře si přečtěte počty **Nízká zásoba** a **Vyžaduje kontrolu**.
2. V každém řádku tabulky si přečtěte **Kritičnost dílu**, **Kritičnost zařízení**, **Stav zásoby**, **Předpoklad**, **Min. / max.** a **Otevřené pracovní příkazy**.
3. Pro nový náhradní díl stiskněte **Nový kritický náhradní díl**, pro existující **Upravit**. Zadejte **Číslo dílu**, **Název**, **Jednotka**, **Minimální zásoba** a **Maximální zásoba**. Pokud prahy nejsou rozhodnuty, ponechte je prázdné.
4. Chcete-li zadat počet, zaškrtněte **Zaznamenat novou inventuru**, vyplňte **Spočtené množství** a stiskněte **Uložit**.
5. V řádku se stavem **Nízká zásoba** bez otevřeného požadavku stiskněte **Navrhnout požadavek**. Server vypočítá navrhované množství (maximum minus předpokládaná zásoba). Pokud není nastaveno maximum, žádné množství se nenavrhne a schvalovatel musí zadat **Schválené množství**.
6. V zobrazení **Technické požadavky** oprávněná osoba, která návrh nepodala, zkontroluje **Schválené množství** a stiskne **Schválit pro export do ERP**, nebo zvolí **Důvod zamítnutí** a stiskne **Zamítnout**.
7. U schváleného požadavku otevřete **Exportní reference ERP**, abyste viděli referenci.

## Co znamenají stavy
- **Nízká zásoba:** předpokládaná zásoba (poslední deklarovaný počet minus náhradní díly zaznamenané v pracovních příkazech odeslaných od tohoto počtu) je na minimu nebo pod ním. V tomto řádku se nabízí návrh požadavku.
- **Nad minimem (deklarováno):** předpokládaná zásoba je nad minimem; počet není ověřen.
- **Minimum nenastaveno:** nebyl zadán žádný práh. Prázdný práh se vykazuje jako vyžadující kontrolu, nikdy jako v pořádku.
- **Zásoba neznámá / Spotřeba nevyřešena:** zásobu nebylo možné vypočítat. Neznámé neznamená „dostatečná zásoba“.
- **Navrženo / Schváleno pro export do ERP / Zamítnuto:** stav technického požadavku.
- **Kritický (deklarováno) / Nekritický (deklarováno) / Neznámé:** kritičnost náhradního dílu je deklarace.

## Co nemůžete dělat / upozornění
- O požadavku, který jste navrhli, nemůžete rozhodnout. Stránka uvádí „Navrhli jste to vy; rozhodnout musí jiná osoba.“
- Návrh a schválení jsou samostatné lidské úkony. Schválení vytvoří pouze exportní referenci ERP; žádná objednávka, dodavatel ani cena se zde nespravuje.
- Stránka nemá samostatné pole pro práh doobjednání; nízká zásoba se posuzuje oproti minimální zásobě.
- V režimu offline nelze data obnovit a nelze provést žádnou akci.

## Když se něco pokazí
- **Záznam se změnil:** pomocí **Zkusit znovu** obnovte stránku a zkuste to znovu.
- **Výsledek neznámý:** **Zopakovat stejný příkaz** odešle stejný příkaz.
- **Požadavek byl zamítnut pravidlem:** zkontrolujte stav zásoby a informace o otevřeném požadavku.
- **Nemáte oprávnění:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).

## Související stránky
