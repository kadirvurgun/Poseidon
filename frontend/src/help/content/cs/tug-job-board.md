---
key: tug-job-board
title: Přehled zakázek
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [canRead, canWrite]
related: [tug-command, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 7]
reviewed: false
---
## K čemu tato stránka slouží
Přehled zakázek uvádí pro plánování **koncepty požadavků na zakázky remorkérů** zvoleného kontextu remorkéru. Odpovídá na otázku: „Jaké požadavky existují, jaké časové okno požadují, kolik remorkérů a jaký tah na kůlu (BP) potřebují a co ještě chybí pro plánování?“ Přehled zobrazuje pouze prohlášení ve stavu konceptu; není to provozní přidělení, způsobilost, povolení ani bezpečnostní schválení.

## Kdo ji používá
- **Koordinátor provozu remorkérů / pobřežní provoz:** vidí požadavky, sleduje informace o konfliktech a připravenosti a připravuje návrh přidělení (13.03 §7).
- **Kapitán remorkéru:** vidí předem nadcházející práci pro svůj remorkér (13.03 §3).

## Než začnete
- V horní kontextové liště zvolte **správnou společnost a remorkér**. Přehled zobrazuje pouze požadavky tohoto kontextu.
- K zobrazení přehledu potřebujete přístup ke čtení. Vytvoření požadavku vyžaduje přístup k zápisu; bez něj se tlačítko **Nový požadavek** nezobrazí.

## Krok za krokem
1. V souhrnu v horní části uvidíte počet požadavků na této stránce, zakázky se zaznamenanou blokující položkou, zakázky čekající na posouzení připravenosti a zakázky s neznámou připraveností. **Počty se týkají pouze této stránky.**
2. V případě potřeby napište do **Hledat požadavky** číslo zakázky nebo název asistovaného plavidla, zvolte filtry **Priorita**, **Stav** a **Časové okno** a stiskněte **Použít filtry**.
3. U každého řádku si přečtěte požadované okno (UTC), koncepty přidělení, požadovaný počet remorkérů, požadovaný BP oproti plánovací kapacitě, připravenost a provozní konflikt.
4. Otevřete na řádku **Proč / další akce**, abyste viděli odkazy na přidělené remorkéry, rozsah porovnání a zaznamenané důvody.
5. Pomocí **Otevřít zakázku** přejdete na podrobnosti požadavku.
6. Pro nový požadavek stiskněte **Nový požadavek**, čímž otevřete **Příjem zakázky** (vyžaduje přístup k zápisu).
7. Pomocí **Obnovit přehled** načtete data znovu a pomocí **Předchozí** / **Další** přejdete na další stránky.

## Co znamenají stavy
- **Draft / Submitted / Cancelled:** stav životního cyklu požadavku. Odeslání zaznamená požadavek pro plánování; není to schválení, přidělení ani rozhodnutí o připravenosti.
- **Blokováno:** požadavek má zaznamenanou blokující položku. Důvod je uveden v **Proč / další akce**.
- **Vyžaduje posouzení:** informace o připravenosti čekají na posouzení oprávněnou osobou.
- **Neznámé:** pro připravenost nebo konflikt není dostatek zdrojových dat. Neznámé neznamená „Připraveno“ ani „žádný konflikt“ a přehled z toho nevyvozuje žádný závěr o připravenosti.
- **Authorized human review required / Boundary touch review required:** dva požadavky se mohou časově překrývat nebo na sebe přímo navazovat. Rozhoduje člověk.
- **BP v profilu (neověřený):** zobrazený tah na kůlu je deklarovaná hodnota, nikoli naměřená ani ověřená.

## Co nemůžete dělat / upozornění
- Přehled nikdy neprohlašuje požadavek za „připravený“ ani „bezpečný“. Návrh přidělení z pobřeží neodnímá Kapitánovi remorkéru bezpečnostní pravomoc na palubě (13.03 §7).
- Pole odeslaného nebo zrušeného požadavku se z přehledu nemění; zrušený požadavek nelze znovu zahájit, vytvořte místo toho nový.
- Filtr časového okna používá čas serveru. Požadavky bez začátku zůstávají v seznamu jako „Neznámé“.
- Data zobrazená offline mohou být stará a přehled nelze obnovit.

## Když se něco pokazí
- **Current board unavailable:** zkontrolujte připojení a použijte **Obnovit přehled**.
- **No draft requests:** pokud jste použili filtry, zkuste jiné vyhledávání nebo prioritu; nové požadavky se zobrazí v tomto kontextu.
- **Access unavailable:** zkontrolujte svou roli a kontext remorkéru; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **Neočekávaná blokující položka nebo posouzení:** otevřete **Proč / další akce** a projděte zaznamenané důvody s vlastníkem požadavku.

## Související stránky
