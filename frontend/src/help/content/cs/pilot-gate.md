---
key: pilot-gate
title: Přezkum go/no-go pilotu
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Přezkum go/no-go pilotu zobrazuje na jednom místě stav bran požadovaných pro pilot, počet otevřených P0 a připravenost podpory a zaznamenává rozhodnutí go/no-go jmenovaného vlastníka produktu. Odpovídá na otázku: „Byla každá brána pilotu splněna s doklady a bylo zaznamenáno lidské rozhodnutí?“ Platí upozornění nahoře: „Pouze poradní přezkum. Neznámé nebo odložené brány a neznámé či otevřené P0 doporučují NO-GO. Rozhodnutí zaznamenává jen jmenovaný vlastník produktu.“

## Kdo ji používá
- **Vlastník produktu:** čte výsledek přezkumu a zaznamenává rozhodnutí go/no-go s odůvodněním. Rozhodování vyžaduje platné jmenování vlastníkem produktu.
- **Podpora / Tenant Admin:** označuje položky připravenosti podpory a pohotovosti s odkazem na doklad.
- 13.03 pro tuto stránku nedefinuje samostatnou roli; platí zásady správy tenanta (nejnižší oprávnění, řízené publikování) (13.03 §9).

## Než začnete
- Zobrazení vyžaduje čtení pro celý tenant a čtení brány pilotu. Označení položky podpory vyžaduje oprávnění k záznamu podpory; rozhodování vyžaduje oprávnění rozhodovat a navíc platné jmenování vlastníkem produktu zřízené na serveru. Samotné oprávnění nestačí.
- Označit podporu nebo rozhodnout může pouze lidský účet; jmenování musí odpovídat vaší aktivní roli a být v období své platnosti.
- Vstupy bran pocházejí z jiných stránek; například brány Ship UAT a Tug UAT se počítají z ověřených běhů na stránce **Záznamy UAT podle rolí**.
- Zaznamenání vyžaduje živé připojení; v režimu offline je vypnuto.

## Krok za krokem
1. V části **Přezkum brány pilotu** si přečtěte doporučení (**Poradní doporučení**), počet **Otevřené P0** a tabulku bran: **Brána**, **Stav**, **Zdroj**, **Důvody**.
2. V části **Připravenost podpory a pohotovosti** zvolte u každé položky **Stav**, pro **Připraveno** zadejte **Odkaz na doklad**, napište **Poznámka** a stiskněte **Označit**.
3. Pokud jste vlastník produktu, zvolte rozhodnutí v části **Zaznamenat rozhodnutí go/no-go** (**NO-GO**, **Podmíněné GO**, **GO**), v poli **Jmenování** zvolte své jmenování vlastníkem produktu a napište **Odůvodnění**. Pro **Podmíněné GO** vyplňte **Podmínky (jedna na řádek)**.
4. Stiskněte **Zaznamenat**. Rozhodnutí se zobrazí v části **Rozhodnutí vlastníka produktu** se svým pořadovým číslem.
5. Stisknutím **Obnovit** data znovu načtete.

## Co znamenají stavy
- **Práh důkazů splněn (PASS):** zdroj brány splňuje práh důkazů. Nejde o schválení; rozhoduje člověk.
- **Nevyhovuje (FAIL)**, **Neznámé (UNKNOWN)**, **Odloženo (DEFERRED)**: brána není splněna. Neznámá nebo odložená brána se nikdy nepočítá jako splněná a doporučuje NO-GO.
- **Doporučeno NO-GO:** alespoň jedna brána není PASS, nebo je počet otevřených P0 neznámý či vyšší než nula.
- **Způsobilé pro GO; čeká se na lidské rozhodnutí:** každá brána je PASS a otevřených P0 je nula. Systém nikdy nerozhoduje o GO; zaznamenává ho pouze vlastník produktu.
- **Neoznačeno / Připraveno / Nepřipraveno:** poslední označený stav položky podpory.
- **Aktuální / Zastaralé: přezkum se změnil:** rozhodnutí je vázáno na přezkum v době, kdy bylo učiněno. Pokud se přezkum později změnil, rozhodnutí se zobrazí jako zastaralé a je třeba ho znovu zvážit.

## Co nemůžete dělat / upozornění
- „GO je možné jen tehdy, když jsou všechny brány PASS a otevřených P0 je nula.“ Jinak je možnost GO vypnuta.
- Podmíněné GO vyžaduje stejný stav jako GO (každá brána PASS, otevřených P0 prokazatelně nula) a alespoň jednu podmínku. Nelze ho zaznamenat, dokud je kterákoli brána nesplněná, neznámá nebo odložená nebo dokud je P0 otevřené či neznámé. NO-GO lze zaznamenat vždy, bez podmínek.
- Systém, AI ani automatizace nikdy nečiní rozhodnutí o pilotu a nikdy nevytvářejí brány ani schválení. Brány pilotu a rozhodnutí jsou lidské záznamy.
- Zaznamenané rozhodnutí nemůžete změnit; nové rozhodnutí se přidá s novým pořadovým číslem.

## Když se něco pokazí
- **Nemáte platné jmenování vlastníkem produktu. Rozhodnutí jsou jen pro čtení.** Jmenování se zřizují na serveru; obraťte se na Tenant Admin (13.03 §10).
- **K těmto záznamům nemáte přístup.** Zkontrolujte svou roli a rozsah tenanta.
- **Záznam se změnil nebo již byl použit. Znovu načtěte.** Přezkum se změnil, zatímco jste rozhodovali; otevřete aktuální přezkum pomocí **Znovu načíst**. Zobrazuje se také tehdy, když se stavy bran změnily tak, že rozhodnutí již není povoleno.
- **Výsledek je nejistý. Opakování odešle stejný příkaz.** Pomocí **Zkusit znovu** bezpečně odešlete stejný příkaz znovu.
- **Odmítnuto pravidly.** Některá brána je odložena.
- **Nepovoleno.** Vaše jmenování není platné nebo váš účet není lidský účet.
- **Služba není dostupná. Zkuste to znovu.** Pokud problém přetrvává, nahlaste podpoře ID korelace, čas a postup (13.03 §10).

## Související stránky
