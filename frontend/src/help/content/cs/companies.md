---
key: companies
title: Společnosti
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## K čemu tato stránka slouží
Stránka Společnosti zobrazuje **záznamy společností** vašeho pracovního prostoru, které máte oprávnění vidět. Odpovídá na otázku: „Které společnosti jsou v tomto pracovním prostoru definovány a jaký mají kód a název?“ Záznamy lodí a remorkérů patří ke společnosti, takže správná společnost je prvním krokem při volbě vašeho kontextu.

## Kdo ji používá
- **Tenant Admin:** vytváří a upravuje záznamy společností podle principu nejnižších oprávnění (13.03 §9).
- **Pobřežní provoz:** vyhledává správný kontext společnosti.

## Než začnete
- K zobrazení této stránky potřebujete oprávnění ke čtení společností. Bez něj se zobrazí zpráva „K tomuto pracovnímu prostoru nemáte přístup.“
- Vytvoření záznamu vyžaduje oprávnění k vytváření. Zda můžete záznam upravit, závisí na oprávnění, které server pro daný záznam udělí.

## Krok za krokem
1. V seznamu vidíte kód a název každé společnosti. Pro další stránky použijte **Předchozí** / **Další**.
2. Chcete-li vyhledávat, pište do pole **Hledat podle kódu nebo názvu** a stiskněte **Hledat**.
3. Vyberte název společnosti, abyste otevřeli část **Podrobnosti**.
4. Pro novou společnost stiskněte **Nový záznam**, zadejte kód a název a stiskněte **Uložit**. Všechna pole jsou povinná.
5. Chcete-li změnit existující záznam, použijte v podrobnostech **Upravit**.
6. Otevřete **Zobrazit záznam právnické osoby**, abyste viděli informace o právnické osobě; tato část je pouze pro čtení.

## Co znamenají stavy
- **Nenalezeny žádné záznamy.:** pro vaše hledání nebo rozsah oprávnění nebyl vrácen žádný záznam.
- **Právnická osoba: Koncept – neúplný, neaktivní / Aktivní / Neaktivní:** stav záznamu právnické osoby. Koncept znamená, že před aktivací ještě chybí hodnoty.
- **Neznámé:** hodnota v záznamu není. Neznámé neznamená „Ne“ ani „platné“.
- **Uloženo.:** záznam byl zapsán na server. Tento záznam nevytváří žádné provozní schválení.

## Co nemůžete dělat / upozornění
- Vytvoření nebo úprava záznamu společnosti není provozním schválením, schválením souladu ani bezpečnostním schválením.
- Hodnoty právnické osoby se na této stránce nemění; mění se pouze auditovanými příkazy HR. Identifikátory z rejstříku se zobrazují maskované.
- Stejný kód nemohou používat dvě společnosti.
- Pokud stránku opustíte s neuloženým konceptem, budete dotázáni, zda ho zahodit, nebo pokračovat v úpravách.

## Když se něco pokazí
- **Tento záznam se změnil v jiné operaci:** váš koncept zůstane zachován. Zahoďte koncept, aby se načetl aktuální záznam, a pak svou změnu proveďte znovu.
- **Tento kód se již používá:** zadejte jiný kód.
- **Výsledek nebylo možné potvrdit:** pole zůstanou uzamčena, dokud nebude výsledek znám; bezpečně zopakujte stejný příkaz.
- **Vaše oprávnění se mohla změnit / Bez přístupu:** zkontrolujte svou roli a kontext pracovního prostoru; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Služba není dostupná:** použijte „Zkusit znovu“; pokud problém přetrvává, nahlaste ho s referencí podpory.

## Související stránky
