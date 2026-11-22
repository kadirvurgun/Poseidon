---
key: deck-daily-log
title: Palubní denní deník
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Palubní denní deník uchovává **záznamy palubních strážních služeb** vybrané lodi jako koncepty se zaznamenanou verzí zdroje. Odpovídá na otázku: „Co stráž pro toto datum a období deklarovala o poloze, kurzu, rychlosti, počasí, viditelnosti, navigaci, přístavních a nákladových činnostech, balastu, palubních pracích, bezpečnostních pozorováních a předání, a bylo pro tuto přesnou revizi zaznamenáno posouzení?“ Uložený zápis je neověřená deklarace, nikoli schválený deník.

## Kdo ji používá
- **Strážní důstojník (Crew / OOW):** vede záznam stráže v reálném čase a v chronologickém pořadí (13.03 §6).
- **Kapitán nebo první důstojník:** posuzuje uložený deník a zaznamenává toto posouzení jako lidský úkon (13.03 §2). Posouzení palubního deníku může zaznamenat pouze jmenování s funkcí kapitána nebo prvního důstojníka.

## Než začnete
- V horním kontextovém pruhu vyberte **správnou společnost a loď**. Pokud není vybrána žádná loď, stránka zobrazí „Vybrat loď“.
- Čtení vyžaduje `daily.deck.read`. Vytváření a ukládání vyžaduje `daily.deck.write` a práva k úpravám na této lodi; jinak uvidíte „Přístup pouze pro čtení. Úpravy a ukládání nejsou povoleny.“
- Zaznamenání posouzení vyžaduje `daily.deck.write` s právy k úpravám na této lodi, `daily.deck.review` a navíc platné jmenování kapitánem nebo prvním důstojníkem pro tento přesný záznam.

## Krok za krokem
1. Vyberte loď. **Zaznamenané koncepty** uvádějí každý deník podle data a revize se stavem „Koncept · vyžaduje posouzení“.
2. Stiskněte **Nový koncept** pro **Nový denní koncept**, nebo otevřete uvedený deník a zvolte **Upravit zaznamenaný koncept**.
3. Nejprve vyplňte **Kontext stráže** (**Datum deníku**, **Stráž / období**, **Reference strážního důstojníka**). Seznam důstojníků nabízí pouze osoby dostupné k deklarovanému datu deníku.
4. Vyplňte **Navigace a počasí**, **Přístavní a nákladové činnosti** a **Palubní práce a předání**. U každého pole zvolte **Známé**, **Neznámé** nebo **Chybí**.
5. Během stráže nebo hned po ní stiskněte **Uložit koncept**. Každé uložení přidá revizi do **Zaznamenaná historie revizí**; dřívější revize zůstávají beze změny.
6. Kapitán nebo první důstojník otevře **Záznam lidského posouzení**, zvolí **Jmenování oprávněné osoby** a **Položka posouzení**, napíše **Odůvodnění posouzení** a stiskne **Zaznamenat položku posouzení**.

## Co znamenají stavy
- **Koncept · prohlášení neověřena / Neověřené prohlášení:** deklarováno osobou; neověřeno.
- **Známé / Neznámé / Chybí:** datový stav pole. Neznámé neznamená „Ne“, „Připraveno“ ani „Nepoužije se“.
- **Čeká · posouzení nezaznamenáno:** pro tuto revizi neexistuje žádné lidské posouzení.
- **Lidská metadata zaznamenána · neposouzeno:** pro tuto revizi existuje položka posouzení. Nezakládá ověření, odeslání ani bezpečnostní schválení kapitánem.
- **Zaznamenat posouzené prohlášení (nevyhodnoceno) / Vrátit k opravě:** dva typy položek posouzení, které může posuzovatel zaznamenat.
- **Historická revize:** posouzení patří ke starší revizi, nikoli k aktuálnímu konceptu.

## Co nemůžete dělat / upozornění
- Systém deník nikdy neposuzuje ani nefinalizuje. Posouzení je lidský úkon jmenovaného kapitána nebo prvního důstojníka (13.03 §2).
- Kdo napsal kteroukoli revizi deníku, nemůže ho posoudit. Autorovi se nenabízí žádné jmenování a stránka uvádí „Tuto revizi deníku jste napsali vy; přezkoumání musí zaznamenat nezávislý posuzovatel.“; server takové posouzení také odmítne z důvodu oddělení povinností.
- Zápisy veďte v reálném čase a chronologicky; nepřepisujte starou stráž tak, aby vypadala jako nová (13.03 §6).
- Neměňte posouzený záznam někoho jiného; pozdější uložení je nová revize a vyžaduje nové posouzení (13.03 §6).
- Záznam v režimu offline pokrývá pouze výslovně klasifikovaná pole. Konflikt offline se nikdy neslučuje automaticky; nepředpokládejte, že váš záznam vyhraje (13.03 §6).

## Když se něco pokazí
- **„Záznam se změnil. Vaše vstupy jsou zachovány.“:** před novým pokusem výslovně zahoďte změny a znovu načtěte.
- **„Výsledek uložení neznámý“:** použijte **Zopakovat přesný uložený příkaz**; pole nevyplňujte znovu.
- **Reference důstojníka není dostupná:** ponechte pole jako Chybí nebo Neznámé; napsané jméno nemůže nahradit vyhledání.
- **„Vyžaduje lidské vyřešení konfliktu“:** otevřete **Posoudit konflikt** nebo Centrum konfliktů a rozhodněte výslovně (13.03 §10).
- **Není dostupné žádné jmenování:** zkontrolujte svou roli a kontext lodi se správcem svého tenanta (13.03 §10).

## Související stránky
