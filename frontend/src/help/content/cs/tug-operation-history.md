---
key: tug-operation-history
title: Historie operací remorkérů
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Historie operací zobrazuje **historii pouze pro čtení** zakázek remorkérů, provedení, denních deníků a dokladů. Odpovídá na otázku: „Které zakázky skončily s jakým výsledkem, byla provedení ověřena, existuje záznam dokladu a které denní deníky k těmto zakázkám patří?“ Zrušené a přerušené záznamy zůstávají viditelné; nic se nemaže ani nepřeznačuje.

## Kdo ji používá
- **Koordinátor provozu remorkérů / pobřežní provoz:** sleduje úplnost dokladů, záznamy událostí a historii dokončených zakázek (13.03 §7).
- **Kapitán remorkéru:** vidí minulé zakázky a časové osy provedení svého vlastního remorkéru (13.03 §3).

## Než začnete
- Otevřete remorkér z **Provoz remorkérů**; bez zvoleného remorkéru stránka zobrazí „Pro pokračování vyberte remorkér.“
- Stačí přístup ke čtení remorkéru; stránka nemění žádná data.
- Historie se načítá ze serveru; offline ji nelze obnovit.

## Krok za krokem
1. Ve **Filtry** volitelně nastavte **ID remorkéru**, **Od (datum UTC)**, **Do (datum UTC)** a **Výsledek** a poté stiskněte **Použít filtry**. Rozsah dat je nejvýše 400 dní.
2. U každého řádku tabulky si přečtěte **Zakázka** (číslo zakázky, asistované plavidlo, typ zakázky, stav plánu a briefingu), **Čas aktivity**, **Výsledek** a **Doklad**.
3. V **Provedení** uvidíte stav každého provedení, stav ověření, dobu trvání, záznam Kapitána a počet záznamů dokladů; pomocí **Otevřít časovou osu provedení** přejdete do konzole provedení.
4. Pomocí odkazů v **Denní záznamy** otevřete související deník. Poznámka „deník nepropojuje“ ukazuje, že deník toto provedení nepropojuje.
5. Pro starší záznamy použijte **Načíst další**. **Načteno** ukazuje, kdy byla data načtena.

## Co znamenají stavy
- **Not started / In progress / Completed (recorded) / Aborted / Cancelled / Mixed:** celkový výsledek zakázky. Completed (recorded) neznamená, že zakázka byla ověřena.
- **Verified, evidence recorded / Verified, no evidence entry / Verification rejected / Evidence unverified / Unknown:** stav dokladů a ověření.
- **verification pending / verified / verification rejected:** stav ověření jednotlivého provedení.
- **Žádné:** pro tuto zakázku neexistuje žádný propojený záznam. Neznamená to, že žádný záznam nebyl potřeba.

## Co nemůžete dělat / upozornění
- Na této stránce se žádný záznam neupravuje, nemaže ani neověřuje.
- Žádné tiché mazání: zrušené zakázky a přerušená nebo zamítnutá provedení zůstávají v historii.
- Dokončeno ≠ Ověřeno: ověření je záznam jiné osoby.
- Zobrazená data odpovídají stavu v čase **Načteno**; offline se neobnovují.

## Když se něco pokazí
- **Zpráva „The filter is not valid“:** zkontrolujte, že ID remorkéru je UUID a rozsah dat nepřesahuje 400 dní.
- **Zpráva „The history could not be loaded.“:** zkontrolujte připojení a stiskněte **Zkusit znovu**.
- **„You do not have access to this tug scope.“:** zkontrolujte svou roli a kontext remorkéru; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **Záznam nelze ověřit:** zkontrolujte odkaz na doklad a jeho zdroj v časové ose provedení; kontaktujte ověřovatele (13.03 §10).

## Související stránky
