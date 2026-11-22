---
key: port-activity-sof
title: Přístavní činnost a SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka přístavní činnosti a SOF uchovává **přístavní události** vybrané lodi jako provozní záznam s referencemi na důkazy a umožňuje jmenované osobě interně finalizovat **Statement of Facts (SOF)**. Odpovídá na otázku: „Které přístavní události jsme deklarovali, v jakém pořadí a intervalu UTC, s jakými referencemi na náklad, ponor, lodivoda, remorkér, zpoždění a protest, a finalizovala jmenovaná osoba tuto revizi SOF?“ Stránka nevytváří žádný podpis, laytime ani demurrage a zaznamenané události nejsou pokyny ani schválená plánovaná období.

## Kdo ji používá
- **Kapitán:** vede příjezd a přístavní činnost jako provozní záznam s důkazy a finalizuje SOF (13.03 §2).
- **První důstojník / Vrchní strojník:** zaznamenávají přístavní události a s platným jmenováním finalizují SOF.

## Než začnete
- V horním kontextovém pruhu vyberte **správnou společnost a loď**. Pokud není vybrána žádná loď, stránka zobrazí „Vybrat loď“.
- Čtení vyžaduje `operations.port.read`; ukládání konceptů vyžaduje `operations.port.write` a práva k úpravám na této lodi.
- Finalizace vyžaduje `operations.port.write`, práva k úpravám na této lodi a `operations.port.finalize` a navíc platné jmenování k finalizaci (kapitán, první důstojník nebo vrchní strojník) pro tuto loď.

## Krok za krokem
1. Vyberte loď. **Časová osa zaznamenaných přístavních událostí** uvádí vrácené události v pořadí UTC serveru. **Filtrovat vrácenou stranu** zužuje pouze tuto stránku.
2. Pod seznamem si přečtěte **Časová osa Statement of Facts**: každou událost s **Zaznamenaný interval**, **Důvod zpoždění** a **Poznámky**.
3. Stiskněte **Nový koncept záznamu**. Vyplňte **Kontext návštěvy přístavu**, **Zaznamenaná činnost a časování**, **Prohlášení o nákladu a ponoru** a **Podpora, výjimky a důkazy**. Časy událostí vyžadují Z nebo výslovný posun vůči UTC.
4. Stiskněte **Uložit neověřený koncept**. Každé uložení přidá revizi do **Zaznamenaná historie revizí**.
5. U uloženého záznamu otevřete **Finalizace Statement of Facts**. Pokud se zobrazí **Chybějící povinné údaje**, nejprve tato pole doplňte.
6. Zaškrtněte každou uvedenou nevyřešenou referenci, abyste ji vzali na vědomí, zvolte své **Jmenování** a stiskněte **Finalizovat SOF**. Systém zaznamená, kdo finalizoval, kdy, funkci a důvod.

## Co znamenají stavy
- **KONCEPT:** SOF není finalizován.
- **FINÁLNÍ (interní):** jmenovaná osoba finalizovala tuto revizi. Je interní: nejde o podpis ani o externí dohodu.
- **Podpis nezaznamenán:** zobrazuje se vždy; na této stránce neexistuje žádný podpis.
- **Změněno po FINÁLNÍ: je nutná nová kontrola a finalizace.:** záznam byl po finalizaci znovu uložen a je opět ve stavu KONCEPT.
- **NEZNÁMÉ (nevyřešené reference):** pro tyto povinné reference neexistuje žádné vlastní vyhledávání. Jejich vzetí na vědomí z nich nečiní známé.
- **Vyžaduje kontrolu / Neověřený koncept záznamu:** přístavní události jsou deklarace; nic není ověřeno.
- **Koncept (nedokončeno) / Konečný (interní; nejde o podpis):** stav SOF, který server pro tuto revizi zaznamenal, zobrazený v souhrnu záznamu. Konečný stav vzniká pouze ze zaznamenané lidské finalizace.

## Co nemůžete dělat / upozornění
- Systém SOF nikdy nefinalizuje. Finalizace je lidský úkon jmenovaného kapitána, prvního důstojníka nebo vrchního strojníka (13.03 §2).
- Finalizovat SOF zůstává vypnuto, dokud chybějí povinné údaje, neexistuje jmenování nebo nejsou nevyřešené reference vzaty na vědomí.
- Finalizace není podpis, není externí dohoda a není laytime ani demurrage.
- Zápatí stránky uvádí: „Dokončení SOF je interní lidský úkon, který v panelu dokončení zaznamenává jmenovaný kapitán, první důstojník nebo první strojník. Podpisy, provozní/právní schválení a offline záznam zůstávají nedostupné.“
- Ukládání konceptů a finalizace vyžadují aktuální online relaci; tato stránka nemá žádný záznam offline.

## Když se něco pokazí
- **„Pro toto plavidlo neexistuje platné jmenování k finalizaci.“:** zkontrolujte svou roli a kontext lodi se správcem svého tenanta (13.03 §10).
- **„Výsledek neznámý: opakování odešle stejný příkaz jednou.“:** použijte **Zkusit znovu**; nezahajujte novou finalizaci.
- **„Příkaz nebyl přijat“:** přečtěte si zobrazený kód, znovu načtěte záznam pomocí **Načíst aktuální uložený záznam** a zkuste to znovu.
- **„Uložený zdroj se změnil.“:** před dalším uložením výslovně znovu načtěte nebo zahoďte změny.

## Související stránky
