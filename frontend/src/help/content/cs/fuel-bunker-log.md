---
key: fuel-bunker-log
title: Palivo, bunkrování a ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka paliva, bunkrování a ROB uchovává **prohlášení o palivu a bunkrování** vybrané lodi jako neověřené koncepty s historií jejich zdroje. Odpovídá na otázku: „Jaké palivo bylo přijato nebo spotřebováno, jaké ROB jsme deklarovali před a po, které reference BDN, dodavatele, vzorku a nádrže k tomu patří a navazuje deklarovaný řetězec ROB z jednoho záznamu na další?“ Stránka neopravuje množství, nepočítá emise a nezakládá soulad ani ověření.

## Kdo ji používá
- **Vrchní strojník / strojní důstojníci:** zaznamenávají prohlášení o bunkrování, spotřebě a ROB s jejich referencemi na důkazy (13.03 §5).
- **Kapitán:** sleduje prohlášení o palivu vedle souhrnů Noon zkontrolovaných kapitánem (13.03 §2).

## Než začnete
- V horním kontextovém pruhu vyberte **správnou společnost a loď**. Pokud není vybrána žádná loď, stránka zobrazí „Vybrat loď“.
- Čtení vyžaduje `operations.fuel.read`. Vytváření a ukládání vyžaduje `operations.fuel.write` a práva k úpravám na této lodi.
- Časy musí obsahovat datum, sekundy a Z nebo výslovný posun vůči UTC; místní čas bez časového pásma se nepřijímá.

## Krok za krokem
1. Vyberte loď. **Záznamy paliva a ROB** uvádějí vrácené záznamy. Pomocí **Filtrovat vrácenou stranu** zúžíte pouze tuto stránku.
2. Pod seznamem si přečtěte **Kontrola kontinuity ROB**. Pro každý deklarovaný druh paliva ukazuje bilanci (před + přijato − spotřebováno − po), **Kontinuita s předchozím záznamem** a **Příznaky ke kontrole**.
3. Jako kontext si přečtěte **Souhrny paliva a ROB z Noon zkontrolované kapitánem**. Zobrazují se jako deklarovaný text a nekopírují se do evidence.
4. Stiskněte **Nový koncept záznamu**. Vyplňte **Kontext záznamu a dodávky**, **Prohlášení o množství a ROB** a **Vlastnosti paliva a záznam vzorku**. U každého pole ponechte deklarovanou hodnotu, **Chybí** nebo **Neznámé**.
5. Stiskněte **Uložit neověřený koncept**. Každé uložení přidá revizi do **Zaznamenaná historie revizí**.
6. Chcete-li začít z uložené polední zprávy nebo strojního deníku, použijte na dané stránce **Otevřít palivový záznam z uloženého zdroje**. **Propojené denní prohlášení** zde zůstává pouze pro čtení.

## Co znamenají stavy
- **Prohlášení v konceptu / Neověřený koncept záznamu:** hodnoty deklarovala osoba; nic není ověřeno.
- **Vyžaduje kontrolu:** stav kontroly souladu u každého záznamu; soulad nebyl posouzen.
- **Čeká; ověření neprokázáno:** stav kontroly ověření; žádné ověření neexistuje.
- **Nezjištěn rozdíl:** deklarovaná čísla se sčítají. Nejde o ověření.
- **Neznámé:** kontinuitu nelze posoudit, například když hodnota chybí nebo je starší historie mimo posuzované období. Neznámé neznamená „Nezjištěn rozdíl“ ani „Připraveno“.
- **Chybí / Neznámé:** odlišné od nuly.

## Co nemůžete dělat / upozornění
- Lidské posouzení, provozní nebo právní schválení, finalizace a záznam offline nejsou na této stránce dostupné.
- Systém označuje rozdíly k lidskému posouzení; nikdy neopravuje množství a nerozhoduje o souladu paliva.
- Posuzuje se pouze nejnovějších 100 záznamů. Bez reference na nádrž je mezera na plavidle s více nádržemi otázkou chybějících údajů o nádrži, nikoli chybou dat.
- Množství z polední zprávy nebo strojního deníku s nevyřešenou jednotkou se nikdy nepřevádějí na hodnoty přijato, spotřebováno ani ROB.

## Když se něco pokazí
- **„Uložený zdroj se změnil.“:** vaše vstupy zůstanou zachovány; před dalším uložením výslovně znovu načtěte nebo zahoďte změny.
- **„Výsledek neznámý.“:** použijte **Zopakovat přesný provozní příkaz**; nic znovu nepište.
- **„Zkontrolujte deklarované hodnoty, výslovné časové posuny a aktuální oprávnění zdroje.“:** opravte označené pole nebo ho ponechte jako Chybí nebo Neznámé.
- **Provozní zdroj nebo přístup není dostupný:** použijte **Zopakovat čtení zdroje**; pokud přístup zůstává odepřen, zkontrolujte svou roli a kontext lodi se správcem svého tenanta (13.03 §10).

## Související stránky
