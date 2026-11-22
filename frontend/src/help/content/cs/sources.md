---
key: sources
title: Zdrojové koncepty
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Zdrojové koncepty uchovává regulatorní zdroje a zdroje pravidel, na které se vaše organizace spoléhá (úmluva, vlajka, klasifikační společnost, pravidla společnosti nebo místní pravidla), jako **verzované záznamy konceptů**. Odpovídá na otázku: „Na které revizi kterého oficiálního zdroje tento požadavek stojí?“ Platí upozornění v horní části: „KONCEPT místní pro tenanta · Deklarováno a neověřeno. Data a hashe nezakládají žádnou právní pravomoc. Odkazy na zdroje se ukládají jako text.“ (13.03 §8).

## Kdo ji používá
- **Uživatel Regulatory / Assurance:** registruje oficiální verzovaný zdroj a uvádí jeho datum účinnosti a životní cyklus (13.03 §8).
- **DPA / HSEQ:** čte jej, aby viděl, na kterou revizi zdroje odkazuje audit nebo zjištění (13.03 §4).

## Než začnete
- Zobrazení vyžaduje čtení v rámci celého tenanta a čtení regulatorních konceptů. Vytváření a ukládání konceptů vyžaduje navíc zápis regulatorních konceptů; bez něj se tlačítko **Nový koncept** nezobrazí.
- Dílčí pohledy vyžadují vlastní oprávnění, vždy s rozsahem čtení v rámci celého tenanta. **Životní cyklus a použitelnost** vyžaduje čtení použitelnosti; úprava pravidel vyžaduje zápis pravidel, vyžádání posouzení vyžaduje posouzení pravidel a vyhodnocení vyžaduje vyhodnocení použitelnosti; kvalifikace a atestace faktů vyžadují regulatorní kvalifikaci nebo atestaci faktů a navíc jmenování zaznamenané na serveru. **Kontroly a doklady** vyžaduje čtení kontrol, čtení dokladů, čtení souborů a čtení použitelnosti; propojení vyžaduje zápis kontrol nebo zápis dokladů s právem úprav na plavidle a schválení vyžaduje schválení kontrol.
- Mějte po ruce oficiální text zdroje a údaje o jeho verzi/změně. POSEIDON text zdroje nestahuje ani neověřuje; vy jej deklarujete.
- Uložení vyžaduje živé spojení se serverem. Offline zůstávají vaše úpravy zachovány v kartě, ale nejsou uloženy.

## Krok za krokem
1. Prohlédněte si zaznamenané zdrojové koncepty v seznamu. Pro vyhledávání pište do **Search drafts** a stiskněte **Hledat**. Mezi stránkami se pohybujte pomocí **Předchozí** a **Další**.
2. Pro nový zdroj stiskněte **Nový koncept**.
3. V **Draft fields** nejprve zvolte pro každé pole **Stav**: **Chybí**, **Neznámé** nebo **Known (declared)**. Hodnotu můžete zadat až po zvolení **Known (declared)**. Pole jsou **Název**, **Deklarovaná pravomoc**, **Reference zdroje (pouze text)**, **Prohlášení o verzi / změně**, **Deklarované datum účinnosti**, **Prohlášení / původ** a **Zdroj pozorován (s posunem UTC)**.
4. Stiskněte **Uložit koncept**. Při úspěchu uvidíte „Koncept uložen. Neověřeno.“; otevřete záznam pomocí **Open saved draft and history**.
5. Otevřete existující koncept ze seznamu, změňte jej a uložte znovu. Každé uložení vytvoří novou **Revize**; starší revize zůstávají v **Neměnná historie** se svým obsahem a **Hash integrity (nikoli pravomoc)**.
6. Pro přechod k pravidlům použitelnosti nebo k řetězci kontrola–doklad použijte **Životní cyklus a použitelnost** nebo **Kontroly a doklady** nad seznamem. Pro návrat stiskněte **Back to register**.

## Co znamenají stavy
- **KONCEPT:** uložený stav zobrazený v seznamu a v záhlaví záznamu. Každý záznam zdroje na této stránce je koncept; není to oficiální přijetí ani rozhodnutí o platnosti.
- **Chybí:** pro pole dosud nebyla zadána žádná informace. Pozdější kroky (například vyhodnocení použitelnosti) jsou blokovány, dokud povinná data chybí.
- **Neznámé:** informace byla hledána, ale není známa. Systém nikdy nečte Neznámé jako „ne“ ani „nepoužitelné“; když jsou kritická data neznámá, výsledek zůstává v posouzení (REVIEW_REQUIRED).
- **Known (declared):** hodnotu jste deklarovali vy. Prohlášení není ověřená informace.
- **Hash integrity (nikoli pravomoc):** ukazuje, že se obsah revize nezměnil; není to důkaz právní platnosti.

## Co nemůžete dělat / upozornění
- Uloženou revizi nemůžete změnit. Když se zdroj změní, uložte novou revizi; požadavky zůstávají vázány na starší revizi a historie se uchovává (13.03 §1).
- Nepředpokládejte, že se zdroje pravidel klasifikační společnosti, vlajky, společnosti a místní pravidla navzájem zastupují; každý zaznamenejte jako samostatný zdroj (13.03 §8).
- Tato stránka zdroj oficiálně nepřijímá a nikomu nedává výsledek shody, povolení ani připravenosti. Přijetí zdroje a nezávislé posouzení jsou lidskou pravomocí.
- Data účinnosti a hashe nezakládají žádnou právní pravomoc.

## Když se něco pokazí
- **Permission denied.** Zkontrolujte svou roli a rozsah tenanta; v případě potřeby se obraťte na Tenant Admin (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Někdo změnil záznam před vámi. Otevřete aktuální revizi pomocí **Znovu načíst uložený koncept** a proveďte svou změnu znovu.
- **Check field states, bounds, dates and source revision.** Hodnota pole nebo formát data je neplatný.
- **Service unavailable. Your draft is retained.** Až se připojení obnoví, odešlete stejný příkaz znovu pomocí **Retry frozen command**.
- **Sign in to continue; retain your draft.** Vaše relace skončila; použijte odkaz **Přihlásit se**.
- Pokud chyba aplikace přetrvává, nahlaste podpoře ID korelace, obrazovku, čas a kroky (13.03 §10).

## Související stránky
