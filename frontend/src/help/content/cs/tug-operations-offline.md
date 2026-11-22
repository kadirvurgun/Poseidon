---
key: tug-operations-offline
title: Offline operace remorkérů
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka uchovává na zařízení **předem stažený snímek** zakázky remorkéru a ukládá tři druhy záznamů pořízených bez připojení do **trvalé, šifrované fronty**: potvrzení briefingu, pozorování při provádění a záznamy denního deníku. Odpovídá na otázku: „Co mohu bezpečně zaznamenat bez připojení, co čeká ve frontě a jak to server přijal?“ Uvolnění kapitánem, GO připravenosti, zahájení/přechody provedení, ověření a posouzení zůstávají online lidskými záznamy.

## Kdo ji používá
- **Kapitán remorkéru:** pokračuje v kritické práci tam, kde je připojení slabé, a sleduje stav synchronizace (13.03 §10).
- **Posádka:** provádí offline potvrzení briefingu a záznamy hlídky a řeší konflikty bez předpokladu, že její vlastní záznam automaticky vyhraje (13.03 §6).

## Než začnete
- Otevřete stránku ze zakázky remorkéru; bez zakázky stránka zobrazí „Otevřete tuto obrazovku ze zakázky remorkéru, abyste předem stáhli její snímek.“
- Řádek **Offline politika** musí ukazovat nakonfigurovanou politiku úložiště (`CONFIGURED_UNVERIFIED`); bez politiky nejsou předběžné stažení snímku ani offline záznam k dispozici.
- Snímek stáhněte předem, **dokud jste online**. Před zaznamenáním zkontrolujte, že zápůjčka snímku nevypršela.

## Krok za krokem
1. Dokud jste online, stiskněte **Předem stáhnout snímek zakázky**. Zobrazí se zpráva „Snímek zapečetěn na tomto zařízení.“
2. V **Snímek zakázky** zkontrolujte číslo zakázky, její stav a **Zapůjčeno do**.
3. Pokud jste účastníkem vydaného briefingu, zvolte v **Potvrdit briefing** **Potvrdit** nebo **Zpochybnit** (s důvodem o délce alespoň 10 znaků) a stiskněte **Zapečetit a zařadit do fronty**.
4. U probíhajícího provedení zvolte v **Zaznamenat pozorování při provádění** provedení a typ pozorování (poznámka, vlečné lano připojeno, vlečné lano uvolněno), napište poznámku a stiskněte **Zapečetit a zařadit do fronty**.
5. U denního deníku ve stavu konceptu nebo vráceného zvolte deník v **Záznam denního deníku**, napište **Souhrn dne** a stiskněte **Zapečetit a zařadit do fronty**.
6. Stav každého záznamu sledujte v **Zachycené záznamy ve frontě**. Záznamy se po obnovení připojení odesílají v pořadí; k ručnímu odeslání použijte **Odeslat zachycené záznamy z fronty**.
7. Pokud záznam ukazuje **Podstatný konflikt**, přečtěte si **Změněno na serveru**, výslovně zvolte **Ponechat záznam serveru** nebo **Použít mnou zachycenou hodnotu na aktuální záznam** a stiskněte **Vyřešit konflikt**.

## Co znamenají stavy
- **Ve frontě na zařízení (neodesláno):** záznam je zapečetěn na tomto zařízení; zařazeno do fronty neznamená odesláno.
- **Přijato serverem:** server záznam obdržel. Není to schválení ani ověření.
- **Podstatný konflikt:** záznam na serveru se po pořízení záznamu změnil; nikdy se neslučuje automaticky a čeká na rozhodnutí člověka.
- **Server value kept / Refused by server:** výsledek, který server u záznamu vrátil.
- **UNKNOWN (offline):** stav politiky nebylo možné offline načíst; žádná politika se nepředpokládá.

## Co nemůžete dělat / upozornění
- Pouze online: uvolnění kapitánem, GO připravenosti, zahájení a přechody provedení, vydání briefingu, ověření, odeslání a posouzení denního deníku.
- Podstatné konflikty se nikdy neslučují automaticky; vyřešení je vždy výslovnou lidskou volbou (13.03 §1).
- S vypršelou zápůjčkou snímku nelze pořídit žádný nový záznam; stáhněte předem nový snímek.
- Při opakovaném přehrání se každý záznam zpracuje jednou se svým původním ID; nevytvářejte stejný záznam znovu.

## Když se něco pokazí
- **Zpráva „The device key for this queue is unavailable.“:** zachycené záznamy zůstávají zachovány, ale nelze je přečíst; nemažte je a kontaktujte podporu.
- **Zpráva „Outcome unknown“:** stejný záznam bude znovu odeslán se svým původním ID; nevytvářejte nový.
- **Zpráva „The offline policy does not allow a snapshot.“:** kontaktujte administrátora tenanta ohledně konfigurace politiky (13.03 §10).
- **Podstatný konflikt:** použijte formulář **Vyřešit konflikt** na této stránce; konflikty jsou také uvedeny v **Centrum podstatných konfliktů** (13.03 §10).

## Související stránky
