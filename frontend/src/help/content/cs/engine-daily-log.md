---
key: engine-daily-log
title: Strojní denní deník
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Strojní denní deník uchovává **záznamy strojních strážních služeb** vybrané lodi jako koncepty se zaznamenanou verzí zdroje. Odpovídá na otázku: „Co strojní stráž deklarovala o stavu motoru a provozních hodinách, spotřebě paliva a mazacího oleje, sladké vodě, kritických měřeních, alarmech, závadách, bunkrování, kontrolách podpalubní vody a životního prostředí, a zaznamenal vrchní strojník posouzení této přesné revize?“ Uložený zápis je neověřená deklarace. Data konceptu se nepřevádějí na závěry o emisích, zajištění paliva ani připravenosti.

## Kdo ji používá
- **Strážní strojník (Crew / OOW / Engineer):** vede záznam strojní stráže v reálném čase a v chronologickém pořadí (13.03 §6).
- **Vrchní strojník:** posuzuje uložený deník, propojuje ho s pracovními příkazy a závadami a zaznamenává posouzení jako lidský úkon (13.03 §5). Posouzení strojního deníku může zaznamenat pouze jmenování s funkcí vrchního strojníka.

## Než začnete
- V horním kontextovém pruhu vyberte **správnou společnost a loď**.
- Čtení vyžaduje `daily.engine.read`; ukládání vyžaduje `daily.engine.write` a práva k úpravám na této lodi. Zaznamenání posouzení vyžaduje `daily.engine.write` s právy k úpravám na této lodi, `daily.engine.review` a navíc platné jmenování vrchním strojníkem pro tento přesný záznam.
- Propojení pracovních příkazů nebo závad vyžaduje také přístup ke čtení těchto záznamů.

## Krok za krokem
1. Vyberte loď. **Zaznamenané koncepty** uvádějí každý deník podle data a revize se stavem „Koncept · vyžaduje posouzení“.
2. Stiskněte **Nový koncept**, nebo otevřete uvedený deník a zvolte **Upravit zaznamenaný koncept**.
3. Vyplňte **Kontext strojní stráže**, **Stav motoru a provozní hodiny**, **Spotřeba a výroba**, **Měření, alarmy a závady** a **Pracovní a environmentální kontext**. U každého pole zvolte **Známé**, **Neznámé** nebo **Chybí**.
4. Stiskněte **Uložit koncept**. Každé uložení přidá revizi do **Zaznamenaná historie revizí**.
5. U uloženého deníku použijte **Pracovní příkazy a závady**: zvolte **Typ záznamu** a **Záznam** a pak **Propojit**. Propojení připne jednu přesnou revizi; **Odebrat odkaz** ho odebere.
6. V případě potřeby otevřete **Otevřít palivový záznam z uloženého zdroje** nebo **Otevřít záznam odstávky z uloženého zdroje**. Množství a jednotky se nekopírují.
7. Vrchní strojník otevře **Záznam lidského posouzení**, zvolí **Jmenování oprávněné osoby** a **Položka posouzení**, napíše **Odůvodnění posouzení** a stiskne **Zaznamenat položku posouzení**.

## Co znamenají stavy
- **Neověřené prohlášení / Známé / Neznámé / Chybí:** deklarované hodnoty a jejich datový stav. Neznámé neznamená „Ne“, „Připraveno“ ani „Nepoužije se“.
- **Čeká · posouzení nezaznamenáno:** pro tuto revizi neexistuje žádné lidské posouzení.
- **Lidská metadata zaznamenána · neposouzeno:** pro tuto revizi existuje položka posouzení vrchního strojníka. Nezakládá ověření ani schválení vrchním strojníkem.
- **Aktuální revize / Existuje novější revize:** zda se propojený pracovní příkaz nebo závada od propojení změnily. Propojení vyjadřuje pouze původ; tento záznam neověřuje, neuzavírá ani neschvaluje.
- **vyhledávání nedostupné — NEZNÁMÉ:** typ propojeného záznamu nelze s vaším přístupem přečíst.

## Co nemůžete dělat / upozornění
- Systém deník nikdy neposuzuje ani nefinalizuje. Posouzení je lidský úkon jmenovaného vrchního strojníka (13.03 §5).
- Kdo napsal kteroukoli revizi deníku, nemůže ho posoudit. Autorovi se nenabízí žádné jmenování a stránka uvádí „Tuto revizi deníku jste napsali vy; přezkoumání musí zaznamenat nezávislý posuzovatel.“; server takové posouzení také odmítne z důvodu oddělení povinností.
- Zápisy veďte v reálném čase a chronologicky; neměňte posouzený záznam někoho jiného (13.03 §6).
- Propojení závady zde nezaznamenává omezení ani závadu neuzavírá; to proveďte na stránce závady nebo pracovního příkazu (13.03 §5).
- Propojení vyžadují aktuální online připojení; offline se nic nepropojuje. Konflikty offline se nikdy neslučují automaticky.

## Když se něco pokazí
- **„Záznam se změnil. Před změnou odkazů jej znovu načtěte.“:** znovu načtěte a pak znovu propojte.
- **„Výsledek uložení neznámý“:** použijte **Zopakovat přesný uložený příkaz**; nic znovu nepište.
- **„Vyžaduje lidské vyřešení konfliktu“:** otevřete **Posoudit konflikt** nebo Centrum konfliktů a rozhodněte výslovně (13.03 §10).
- **Není dostupné žádné jmenování nebo byl přístup odepřen:** zkontrolujte svou roli a kontext lodi se správcem svého tenanta (13.03 §10).

## Související stránky
