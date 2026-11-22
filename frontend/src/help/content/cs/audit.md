---
key: audit
title: Historie akcí
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Historie akcí zobrazuje záznam změn obchodních záznamů ve vašem oprávněném rozsahu, který je **pouze pro čtení** a přiřazený konkrétním aktérům. Odpovídá na otázku: „Kdy se tento záznam změnil, jakou akcí a v kontextu jakého aktéra a role?“ Bezpečnostní události jsou omezeny zvlášť a zde se nezobrazují (13.03 §4, §9).

## Kdo ji používá
- **DPA / HSEQ:** sleduje záznamy zjištění, opatření a ověření (13.03 §4).
- **Regulatory / Assurance user:** kontroluje historii záznamu před auditem nebo přezkumem (13.03 §8).
- **Tenant Admin:** audituje změny členství a oprávnění a časy začátku a konce delegace (13.03 §9).

## Než začnete
- Zobrazení vyžaduje oprávnění ke čtení auditu. Bez něj uvidíte „Je nutné oprávnění k auditu.“
- Zobrazují se pouze záznamy v rozsahu tenanta a plavidla, pro který máte oprávnění.
- Pokud hledáte konkrétní záznam, mějte připravené jeho **ID plavidla** nebo **ID objektu**. Odkazy na historii na jiných stránkách otevírají tuto stránku již vyfiltrovanou.

## Krok za krokem
1. Vyplňte filtry: **ID plavidla**, **ID objektu**, **Typ objektu** a **Akce** (**Vše**, **Vytvořeno**, **Aktualizováno**, **Smazáno**).
2. Stiskněte **Filtrovat**. Pro obnovení výchozího stavu použijte **Vymazat filtry**.
3. Zkontrolujte sloupce **Čas**, **Objekt** a **Akce** v části **Oprávněné záznamy**. Mezi stránkami se pohybujte pomocí **První strana** a **Další**.
4. Stisknutím odkazu na objekt v řádku otevřete **Podrobnosti záznamu**. Přečtěte si **Reference aktéra**, **Reference přidělených rolí**, **Důvěryhodná oprávnění**, **Důvod**, **Metadata**, **Verze prostředku** a **Korelace**.
5. Pokud existuje důkaz neměnné revize, stiskněte **Otevřít přesnou neměnnou revizi**, abyste viděli záznam tak, jak byl. Podrobnosti zavřete tlačítkem **Zavřít**.

## Co znamenají stavy
- **Vytvořeno / Aktualizováno / Smazáno:** druh akce provedené se záznamem.
- **Zaznamenáno:** metadata byla zachycena úplně.
- **Historická metadata jsou neúplná / Historický identifikátor není dostupný / Historický kontext není dostupný:** starší záznamy tyto informace neuchovaly. Chybějící informace se nikdy nedoplňují odhadem.
- **Hodnoty zadrženy:** hodnota pole je z důvodu ochrany soukromí skryta; zobrazuje se pouze skutečnost, že se změnila.
- **Důkaz neměnné revize není pro tento záznam dostupný.** Pro tento typ záznamu neexistuje snímek revize.

## Co nemůžete dělat / upozornění
- Stránka je pouze pro čtení. Položku historie akcí nelze upravit ani smazat.
- To, že se záznam objeví v historii, neznamená, že práce byla ověřena nebo byla účinná; Ověřeno ≠ Účinné (13.03 §4).
- Tato stránka zaznamenává obchodní změny. Sama o sobě není úplným auditem uživatelských akcí ani důkazem souladu s ochranou soukromí; bezpečnostní události se uchovávají zvlášť.
- Seznam zobrazuje pouze rozsah, pro který máte oprávnění. Záznamy mimo váš rozsah se nezobrazují; to, že záznam nevidíte, neznamená, že se nikdy nezměnil.

## Když se něco pokazí
- **Je nutné oprávnění k auditu.** Zkontrolujte svou roli a rozsah; v případě potřeby se obraťte na Tenant Admin (13.03 §10).
- **Pro pokračování se přihlaste.** Přihlaste se znovu.
- **Historie není dostupná. Zkontrolujte filtry a zkuste to znovu.** Zkontrolujte, že identifikátory jsou úplné a správné, a pak stiskněte **Zkusit znovu**.
- **Těmto filtrům neodpovídají žádné oprávněné záznamy.** Rozšiřte filtry.
- Pokud chyba aplikace přetrvává, nahlaste ji podpoře s ID korelace, obrazovkou, časem a postupem (13.03 §10).

## Související stránky
