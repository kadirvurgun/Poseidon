---
key: requirements
title: Koncepty požadavků
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Koncepty požadavků uchovává **požadavky** odvozené z revize zdroje jako verzované koncepty a váže každý požadavek na přesnou revizi zdroje. Odpovídá na otázku: „Na které revizi kterého zdroje tento požadavek stojí a jaký je jeho zaznamenaný životní cyklus?“ Požadavek je prvním článkem řetězce Požadavek → Kontrola → Doklad → Ověření. Žádný záznam v tomto řetězci by neměl zůstat bez propojení (osiřelý) (13.03 §8).

## Kdo ji používá
- **Uživatel Regulatory / Assurance:** váže požadavek k jeho zdroji, posuzuje použitelnost pomocí kontextových vstupů a udržuje řetězec úplný (13.03 §8).
- **DPA / HSEQ:** čte podklad požadavku při práci na zjištěních a auditech (13.03 §4).

## Než začnete
- Zobrazení vyžaduje čtení v rámci celého tenanta a čtení regulatorních konceptů; úpravy vyžadují navíc zápis regulatorních konceptů.
- Dílčí pohledy vyžadují vlastní oprávnění, vždy s rozsahem čtení v rámci celého tenanta. **Životní cyklus a použitelnost** vyžaduje čtení použitelnosti; úprava pravidel vyžaduje zápis pravidel, vyžádání posouzení vyžaduje posouzení pravidel a vyhodnocení vyžaduje vyhodnocení použitelnosti; kvalifikace a atestace faktů vyžadují regulatorní kvalifikaci nebo atestaci faktů a navíc jmenování zaznamenané na serveru. **Kontroly a doklady** vyžaduje čtení kontrol, čtení dokladů, čtení souborů a čtení použitelnosti; propojení vyžaduje zápis kontrol nebo zápis dokladů s právem úprav na plavidle a schválení vyžaduje schválení kontrol.
- Nejprve zaznamenejte zdroj na stránce **Zdrojové koncepty**. Požadavek lze vázat pouze na existující revizi zdroje.
- Uložení vyžaduje živé spojení se serverem; offline zůstávají úpravy zachovány v kartě.

## Krok za krokem
1. Prohlédněte si koncepty požadavků v seznamu; vyhledávejte pomocí **Search drafts** a **Hledat**.
2. Pro nový požadavek stiskněte **Nový koncept**.
3. V **Draft fields** zvolte pro každé pole **Stav** (**Chybí**, **Neznámé**, **Known (declared)**) a zadejte známé hodnoty: **Předpis**, **Prohlášení o verzi / změně**, **Deklarované datum účinnosti**, **Prohlášení / původ**, **Zdroj pozorován (s posunem UTC)**.
4. V **Immutable source citation** napište text do **Find a source** a stiskněte **Search sources**. Zvolte zdroj v **Source draft** a poté přesnou revizi v **Exact revision**. Obsah zvolené revize zkontrolujte pomocí **Selected immutable source content**.
5. Stiskněte **Uložit koncept**. Když je záznam otevřen, zobrazí se sekce **Zaznamenaný životní cyklus** a **Neměnná historie**.
6. Pro použitelnost použijte **Životní cyklus a použitelnost** a pro propojení kontrol a dokladů **Kontroly a doklady**. **Protokol kritických záznamů bez přiřazení** v rámci **Kontroly a doklady** uvádí kritická prohlášení bez propojení na požadavek; propojte tyto záznamy s požadavkem.

## Co znamenají stavy
- **Zaznamenaný životní cyklus:** stav, který server odvozuje z vázaných pravidel: **Koncept**, **Přijato**, **Budoucí účinnost**, **V platnosti**, **Nahrazeno**. Pokud jej nelze načíst, zobrazí se **Nedostupné**; systém nepředpokládá koncept ani platnost.
- „Vázaná pravidla zaznamenávají různé stavy životního cyklu. Zobrazená hodnota je softwarová priorita; je nutné odborné posouzení.“ Zobrazená hodnota pak není rozhodnutím; je nutné odborné posouzení.
- **Neznámé:** informace není známa. Když jsou kritická data neznámá, výsledek použitelnosti zůstává **Vyžaduje posouzení** (REVIEW_REQUIRED); nikdy se nepočítá jako „nepoužitelné“ ani „ne“ (13.03 §8).
- **Applicability review required / Requirement link missing / Verification not performed:** otevřené položky v řetězci kontrola–doklad.
- **Dokončení neznámé · neověřeno · účinnost neznámá:** dokončení, ověření a účinnost jsou samostatné stavy. Dokončený záznam není ověřený a ověřený záznam není účinný.

## Co nemůžete dělat / upozornění
- Požadavek nemůžete vázat na „nejnovější“ zdroj; vždy se volí **Exact revision**. Když zdroj získá novou revizi, staré vázání zůstává v historii.
- Náhled použitelnosti nezakládá žádnou shodu, připravenost, povolení ani právní schválení: „NÁHLED / KONCEPT. Předpovědi nezakládají shodu, připravenost, povolení ani právní schválení.“
- Změna životního cyklu prochází přes **Vyžádat lidské posouzení**; rozhoduje přijetí zdroje a kvalifikovaný nezávislý posuzovatel. Systém ji nikdy neuzavře sám.
- Nepředpokládejte, že se zdroje pravidel klasifikační společnosti, vlajky, společnosti a místní pravidla navzájem zastupují (13.03 §8).

## Když se něco pokazí
- **Source selection unavailable.** Stiskněte **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Otevřete aktuální revizi pomocí **Znovu načíst uložený koncept**.
- **Check field states, bounds, dates and source revision.** Pokud jste zvolili zdroj, zvolte také jeho přesnou revizi.
- **History unavailable. Editor retained.** Stiskněte **Retry history**.
- Pokud je životní cyklus neočekávaný, zkontrolujte vázaná pravidla a revize zdrojů; v případě potřeby kontaktujte odpovědnou osobu kontroly nebo odborníka na danou oblast (SME) (13.03 §10).
- Bez oprávnění zkontrolujte svou roli a rozsah tenanta a požádejte Tenant Admin (13.03 §10).

## Související stránky
