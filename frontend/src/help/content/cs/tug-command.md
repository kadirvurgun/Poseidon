---
key: tug-command
title: Řízení remorkérů
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka, v menu zobrazená jako **Řízení remorkérů**, ukazuje **zdrojový kontext** zvoleného remorkéru. Odpovídá na otázku: „Jaká role, typ, tah na kůlu (BP) a prohlášení o vybavení jsou pro tento remorkér zaznamenány, odkud pocházejí a jak jsou aktuální?“ Stránka zobrazuje pouze prohlášení a jejich zdroje; nedává žádnou provozní připravenost, způsobilost, povolení ani bezpečnostní schválení. Odznak v horní části to uvádí: „Zdrojová prohlášení · provozní připravenost neznámá“.

## Kdo ji používá
- **Kapitán remorkéru:** před zahájením práce potvrzuje roli, BP a kontext vybavení svého vlastního remorkéru (13.03 §3).
- **Koordinátor provozu remorkérů / pobřežní provoz:** před přechodem na přehled zakázek a příjem zakázky se ujišťuje, že je ve správném kontextu remorkéru (13.03 §7).
- **Posádka:** vidí svůj pohled role a obrazovky režimu lodi, které má k dispozici (13.03 §6).

## Než začnete
- Potřebujete režim remorkéru a přístup ke čtení remorkéru. Bez něj stránka zobrazí „Přístup není k dispozici.“
- Pokud není zvolen žádný kontext, stránka uvede oprávněné remorkéry v **Vybrat remorkér**; jeden otevřete.
- Seznam vybavení vyžaduje také přístup ke čtení vybavení; bez něj se zdroj vybavení zobrazí jako „nedostupné nebo neuděleno“.

## Krok za krokem
1. Otevřete remorkér ze seznamu **Vybrat remorkér**. Řádek **Kontext remorkéru** pod nadpisem zobrazuje kód remorkéru, název a režim.
2. V **Role (přiřazení serverem)** a **Pohled menu režimu remorkéru** si přečtěte roli a pohled menu, které vám server přidělil.
3. V **Viditelnost režimu** uvidíte, zda jsou obrazovky režimu lodi otevřené. Sekce **Obrazovky režimu lodi** odkazuje pouze na obrazovky, které vám byly uděleny.
4. Podle **Typ remorkéru**, **Zdroj typu / revize** a **Pozorování zdroje typu / pravidla** zkontrolujte, ze kterého zdroje a revize typ pochází.
5. Přečtěte si **BP v profilu (neověřený)**, **Zdroj BP / verze / původ** a **Aktuálnost BP / pozorování**. Je to deklarovaná hodnota, nikoli naměřená.
6. V **Kontext vybavení** uvidíte prohlášení o vybavení, jejich úrovně, revize a původ.
7. Pomocí karet stránky přejděte na **Přehled zakázek** nebo (s přístupem k zápisu) **Příjem zakázky**, nebo (s přístupem k zápisu) stiskněte **Nový požadavek**.

## Co znamenají stavy
- **Neznámé — žádné přiřazení role:** server pro vás na tomto remorkéru nemá žádné přiřazení role. Neznamená to „žádná role, žádný problém“; je potřeba přiřazení administrátorem.
- **Role labels unavailable / Menu lens unavailable:** zdroj nelze nyní načíst. Chybějící informace se nepovažuje za prázdnou.
- **Pro režim remorkéru není nastaven žádný pohled:** v tomto režimu pro vás není definován žádný pohled menu.
- **V režimu remorkéru skryté. Vyžaduje se mezirežimová role.:** obrazovky režimu lodi vyžadují výslovně oprávněnou mezirežimovou roli.
- **BP v profilu (neověřený):** deklarovaný tah na kůlu. Není to ověřený výsledek zkoušky.
- **Nebyla vrácena žádná prohlášení o vybavení. Ověřená provozní způsobilost zůstává neznámá.:** prázdný seznam nikdy neznamená, že je vybavení způsobilé.
- **Neznámé / Nedostupné:** zdrojová hodnota chybí nebo ji nebylo možné načíst. Neznámé nikdy nezastupuje „Připraveno“ ani „Ne“.

## Co nemůžete dělat / upozornění
- Na této stránce se neupravuje žádné pole a nevytváří se žádný výsledek připravenosti. Provozní rozhodnutí a bezpečnostní pravomoc zůstávají u Kapitána remorkéru na palubě (13.03 §3).
- Návrh přidělení z pobřeží nikdy nepřevažuje nad bezpečnostním rozhodnutím Kapitána remorkéru na palubě (13.03 §7).
- I s deklarovaným mezirežimovým oprávněním vyžaduje každá obrazovka režimu lodi stále vlastní autorizaci serveru.
- Offline se aktuální zdroje profilu nezobrazují; znovu se připojte, abyste znovu zkontrolovali přístup a verze zdrojů.

## Když se něco pokazí
- **Access is not available:** zkontrolujte svou roli, remorkér a kontext objektu; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **Upozornění „Current identity and vessel access must be checked“:** stiskněte **Znovu zkontrolovat přístup**.
- **Data unavailable:** zkontrolujte připojení a stiskněte **Zkusit znovu**.
- **Neočekávané hodnoty BP nebo vybavení:** poznamenejte si pole zdroje a verze a projděte je s vlastníkem zdroje; pokud záznam nelze ověřit, zkontrolujte jeho doklady a původ (13.03 §10).

## Související stránky
