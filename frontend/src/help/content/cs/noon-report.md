---
key: noon-report
title: Polední hlášení
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka poledního hlášení uchovává **denní polední prohlášení** vybrané lodi jako koncepty se zaznamenanou verzí zdroje. Odpovídá na otázku: „Co jsme deklarovali o čase hlášení, plavbě, poloze, vzdálenosti, počasí, nákladu, provozních hodinách strojního zařízení, palivu a ROB, a zaznamenal kapitán posouzení této přesné revize?“ Uložený koncept je neověřená deklarace. Jeho uložení nezakládá schválení kapitánem ani vrchním strojníkem a stránka z něj nevyvozuje žádný závěr o emisích, zajištění paliva ani připravenosti.

## Kdo ji používá
- **Důstojník připravující hlášení (Crew / OOW):** vyplňuje a ukládá koncept poledního hlášení pro správnou loď a datum (13.03 §6).
- **Kapitán:** posuzuje uložené hlášení a zaznamenává toto posouzení jako lidský úkon (13.03 §2). Posouzení poledního hlášení může zaznamenat pouze jmenování s funkcí kapitána.

## Než začnete
- V horním kontextovém pruhu vyberte **správnou společnost a loď**. Pokud není vybrána žádná loď, stránka zobrazí „Vybrat loď“.
- Čtení vyžaduje `daily.noon.read`. Vytváření a ukládání konceptů vyžaduje `daily.noon.write` a práva k úpravám na této lodi; bez nich uvidíte „Přístup pouze pro čtení. Úpravy a ukládání nejsou povoleny.“
- Zaznamenání posouzení vyžaduje `daily.noon.write` s právy k úpravám na této lodi, `daily.noon.review` a navíc platné jmenování kapitánem pro tento přesný záznam.

## Krok za krokem
1. Vyberte loď. Seznam **Zaznamenané koncepty** uvádí každé hlášení podle data a revize s odznakem „Koncept · vyžaduje posouzení“. Pro stránkování použijte **Předchozí koncepty** / **Další koncepty**.
2. Stiskněte **Nový koncept** pro otevření **Nový denní koncept**, nebo otevřete uvedené hlášení a zvolte **Upravit zaznamenaný koncept**.
3. Vyplňte skupiny (**Hlášení a plavba**, **Poloha a činnost**, **Počasí a náklad**, **Strojní zařízení, palivo a ROB**). U každého pole zvolte **Stav dat**: **Známé**, **Neznámé** nebo **Chybí**. Neúplné koncepty lze uložit.
4. Stiskněte **Uložit koncept**. Stránka potvrdí „Koncept uložen. Lidské posouzení a provozní schválení zůstávají samostatné.“ Každé uložení přidá revizi do **Zaznamenaná historie revizí**.
5. U uloženého hlášení kapitán otevře **Záznam lidského posouzení**, zvolí **Jmenování oprávněné osoby**, **Položka posouzení** („Zaznamenat posouzené prohlášení (nevyhodnoceno)“ nebo „Vrátit k opravě“) a napíše **Odůvodnění posouzení**, pak stiskne **Zaznamenat položku posouzení**.
6. Pokud je třeba zaznamenat palivo nebo přístavní událost, použijte **Použít uložený provozní zdroj** a **Otevřít palivový záznam z uloženého zdroje** nebo **Otevřít přístavní událost z uloženého zdroje**. Množství, jednotky a schválení se nekopírují.

## Co znamenají stavy
- **Koncept · prohlášení neověřena / Neověřené prohlášení:** hodnoty deklarovala osoba; nikdo je neověřil.
- **Známé / Neznámé / Chybí:** datový stav pole. Neznámé neznamená „Ne“, „Připraveno“ ani „Nepoužije se“ a Chybí není nula.
- **Čeká · posouzení nezaznamenáno:** pro tuto revizi nebylo zaznamenáno žádné lidské posouzení.
- **Lidská metadata zaznamenána · neposouzeno / Zaznamenáno · neposouzeno:** pro zobrazenou revizi existuje položka posouzení kapitána. Zaznamenání posouzení nezakládá ověření, odeslání ani bezpečnostní schválení kapitánem.
- **Historická revize:** položka posouzení patří ke starší revizi a nepředstavuje posouzení aktuálního uloženého konceptu.

## Co nemůžete dělat / upozornění
- Systém hlášení nikdy neposuzuje ani nefinalizuje. Zaznamenání posouzení je lidský úkon jmenovaného kapitána (13.03 §2).
- Kdo napsal kteroukoli revizi deníku, nemůže ho posoudit. Autorovi se nenabízí žádné jmenování a stránka uvádí „Tuto revizi deníku jste napsali vy; přezkoumání musí zaznamenat nezávislý posuzovatel.“; server takové posouzení také odmítne z důvodu oddělení povinností. Pokud hlášení napsal kapitán, posouzení musí přesto zaznamenat nezávislý posuzovatel s pravomocí k posouzení.
- Nové uložení vytvoří novou revizi; dřívější posouzení zůstává u své vlastní revize. Po změnách proveďte posouzení znovu.
- Ručně napsaná textová reference nemůže nahradit oprávněné vyhledání; ponechte pole jako Chybí nebo Neznámé.
- V režimu offline mohou do šifrované fronty přejít pouze výslovně klasifikovaná pole. Pole posouzení, schválení a ověření jsou vyloučena.

## Když se něco pokazí
- **„Záznam se změnil. Vaše vstupy jsou zachovány.“:** někdo uložil novější verzi. Před novým pokusem výslovně zahoďte změny a znovu načtěte.
- **„Výsledek uložení neznámý“:** pole jsou zmrazena; použijte **Zopakovat přesný uložený příkaz**, nic znovu nepište.
- **„Pro tento záznam není dostupné aktuální zdrojem oprávněné lidské jmenování.“:** zkontrolujte svou roli a kontext lodi se správcem svého tenanta (13.03 §10).
- **„Vyžaduje lidské vyřešení konfliktu“:** záznam pořízený offline je v konfliktu se serverem. Otevřete **Posoudit konflikt** nebo Centrum konfliktů a rozhodněte výslovně; nic se neslučuje automaticky (13.03 §10).
- **Denní zdroje nejsou dostupné nebo byl přístup odepřen:** použijte **Zopakovat čtení zdrojů**; pokud problém přetrvává, nahlaste podpoře čas a postup.

## Související stránky
