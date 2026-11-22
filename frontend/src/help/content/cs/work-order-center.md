---
key: work-order-center
title: Pracovní příkazy
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Centrum pracovních příkazů uchovává **koncepty pracovních příkazů a deklarované záznamy o provedení** zvoleného plavidla. Odpovídá na otázku: „Jaké pracovní příkazy na tomto plavidle existují a které byly vydány, provedeny, měly odeslané doklady a byly ověřeny osobou?“ Každá uložená revize se uchovává v neměnné historii. Ověření a uzavření nejsou návratem do provozu, povolením ani schválením rizika.

## Kdo ji používá
- **Vrchní strojník / Technical Superintendent:** otevírá, vydává, ověřuje a uzavírá plánované i neplánované pracovní příkazy (13.03 §5).
- **Strojník / posádka:** zahajuje přidělenou práci a zaznamenává a odesílá odečty, náhradní díly, fotografie a dokumenty jako doklady (13.03 §6).

## Než začnete
- Čtení vyžaduje `workorder.read`, `equipment.read` a `maintenance.read`. Koncepty vyžadují `workorder.write`, vydání `workorder.release`, provedení `workorder.execute`, ověření `workorder.verify` a uzavření `workorder.close`.
- Vydání, ověření a uzavření vyžadují přihlášeného lidského uživatele.
- Zvolte správné plavidlo z **Vybrat plavidlo nebo remorkér**.

## Krok za krokem
1. Zvolte plavidlo. V **Zaznamenané pracovní příkazy** filtrujte tuto stránku pomocí **Hledat vrácená čísla pracovních příkazů** a otevřete záznam.
2. Pro novou práci stiskněte **Nový koncept pracovního příkazu**, vyplňte pole a stiskněte **Uložit neposouzený koncept**. Neúplné koncepty lze uložit.
3. Máte-li oprávnění k vydání, zvolte v **Provedení a důkazy** v části **Politika povinných důkazů** **Určit povinné důkazy**, zaškrtněte druhy dokladů a stiskněte **Vydat pracovní příkaz**. Pokud vydáte bez určení, ověření zůstává blokováno, dokud osoba s oprávněním k vydání nepoužije **Změnit povinné důkazy** → **Uložit povinné důkazy**.
4. Vykonavatel stiskne **Zahájit práci**, vyplní **Odečty**, **Spotřebované náhradní díly**, **Fotografie a dokumenty jako důkazy**, **Provedená práce** a **Zjištění**, stiskne **Uložit důkazy** a poté **Odeslat důkazy**.
5. Oprávněný ověřovatel, který práci neprováděl, posoudí doklady. Stiskne **Zaznamenat ověření**, nebo zvolí **Důvod vrácení** a stiskne **Vrátit vykonavateli**.
6. U ověřeného pracovního příkazu stiskne oprávněná osoba **Zaznamenat uzavření**.
7. Dřívější revize uvidíte v **Neměnná historie pracovních příkazů**. Je-li potřeba záznam o odstávce, použijte **Otevřít propojený koncept odstávky**.

## Co znamenají stavy
- **Not released for execution. / Released / In progress:** práce dosud není vydána, je vydána, nebo se provádí.
- **Důkazy odeslány:** vykonavatel deklaroval práci a doklady. Neznamená to ověřeno.
- **Ověřeno (záznam člověka):** zaznamenáno oprávněným ověřovatelem, který práci neprováděl. **Uzavřeno (záznam člověka)** je samostatný krok.
- **Required evidence missing / Present, pending human verification / Verified by a human:** stav brány povinných dokladů.
- **Nutná kontrola: politika není nastavena:** nebyly určeny žádné povinné doklady. Nenastaveno nikdy neznamená, že není nic vyžadováno; ověření zůstává blokováno.
- **Vráceno ověřovatelem:** práce se vrátila vykonavateli se zobrazeným důvodem.

## Co nemůžete dělat / upozornění
- Pokud jste práci prováděli, nemůžete ji ověřit ani uzavřít. Stránka uvádí „Tuto práci jste prováděli, proto ji nemůžete ověřit ani uzavřít.“
- Důkazy odeslány, Ověřeno a Uzavřeno jsou samostatné lidské záznamy. Stránka nikdy neprohlašuje zařízení za připravené k provozu.
- Souhrn „Uložený zdroj pracovního příkazu“ popisuje uložená pole konceptu a uvádí: „Vydání, záznam dokladů, nezávislé ověření a uzavření se zaznamenávají v panelu provedení. Odeslané doklady přijímá pouze nezávislé lidské ověření, nikdy osoba, která práci provedla; neexistuje samostatný krok přijetí dokladů a ověření není rozhodnutím o návratu do provozu.“ Tyto akce se provádějí v **Provedení a důkazy**.
- Odečty se uchovávají tak, jak byly deklarovány; nepoužívá se žádný převod jednotek ani prahová hodnota.
- Offline fronta dokladů funguje pouze tehdy, když existuje schválená offline politika, a pouze během probíhající práce. Konflikty se nikdy neslučují automaticky; použijte **Otevřít centrum konfliktů** a rozhodne člověk.

## Když se něco pokazí
- **The record changed or this action is not allowed in its current state:** pomocí **Znovu načíst** načtěte aktuální záznam a zkuste to znovu.
- **Outcome unknown:** **Zopakovat stejný příkaz** odešle stejný příkaz; nevytváří nový záznam.
- **Permission or human authority is missing:** zkontrolujte svou roli a kontext plavidla; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **The required-evidence gate rejected this action:** doplňte chybějící druhy dokladů. Pokud záznam nelze ověřit, zkontrolujte doklady a jejich původ s ověřovatelem (13.03 §10).

## Související stránky
