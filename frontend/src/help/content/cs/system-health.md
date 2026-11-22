---
key: system-health
title: Stav systému
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
„Stav systému“ zobrazuje administrátorům **pozorování služeb** na jedné obrazovce: API, zapisovač auditu, synchronizaci, úlohy oznámení, souborovou službu a aktuálnost vstupů. Odpovídá na otázku: „Které signály služeb byly pro zvolené plavidlo a tento tenant pozorovány a které ne?“ Konfigurace ani chybějící telemetrie nezakládají zdravý stav služby ani připravenost plavidla.

## Kdo ji používá
- **Systémový administrátor / Tenant Admin:** aby si všiml problémů se službami a synchronizací a řešil je kontrolovaným způsobem (13.03 §9).
- **Podpora / vývoj:** aby při nahlášení chyby aplikace zkontroloval pozorování (13.03 §10).

## Než začnete
- Stránka je viditelná pouze uživatelům s přístupem ke čtení v rámci celého tenanta, ke čtení historie akcí a ke čtení stavu systému.
- V horní kontextové liště zkontrolujte, že je zvolena **správná společnost**.
- Pozorování se vyžadují po jednotlivých plavidlech; nejprve musíte zvolit plavidlo.

## Krok za krokem
1. Zvolte plavidlo v **Rozsah pozorování plavidla**. U dlouhého seznamu použijte **Předchozí plavidla** / **Další plavidla**.
2. Zkontrolujte čas „Načteno v“.
3. V **Dostupnost služby** si přečtěte čas spuštění procesu a dobu provozu.
4. Na kartách **API** a **Zapisovač auditu** si přečtěte údaje o požadavcích, chybách, uložených záznamech a latenci. Tyto údaje se týkají tohoto tenanta od spuštění procesu.
5. V **Synchronizace** uvidíte potvrzené výsledky, nevyřešené konflikty a poslední potvrzenou synchronizaci.
6. V **Úlohy oznámení** uvidíte otevřená oznámení, eskalace po termínu a stav vyhodnocovací úlohy.
7. Přečtěte si karty **Souborová služba** a **Aktuálnost vstupů**.
8. V části **Metriky pilotu** zkontrolujte pozorování od zapsaných pilotních klientů.
9. Pro aktuální data stiskněte **Obnovit pozorování**.

## Co znamenají stavy
- **Nepozorováno / Nedostupné:** pro tento signál neexistuje žádné pozorování. Neznamená to „zdravé“ ani „žádný problém“.
- **Nastaveno · neotestováno:** souborová služba se jeví jako nakonfigurovaná, ale nebyla provedena žádná živá zkouška úložiště ani skeneru.
- **V rámci plánu / Probíhá / Po termínu / Scheduler not enabled / Run abandoned (lease expired):** stav vyhodnocovací úlohy oznámení.
- **Nevyřešené konflikty:** offline konflikty čekající na lidské vyřešení. Nikdy se neslučují automaticky.
- **Aktuální / Zastaralé / Neznámé / Nedostupné:** počty posouzení aktuálnosti vstupů. Neznámé neznamená „Aktuální“.
- **This snapshot is older than two minutes:** před spoléháním se na něj obnovte data.

## Co nemůžete dělat / upozornění
- Tato stránka je pouze pro čtení; neprovádí se žádná akce na službách, plavidlech ani záznamech.
- Záznamy oznámení existují pouze v aplikaci; nejsou důkazem doručení ani jakéhokoli bezpečnostního rozhodnutí.
- Dostupnost služby ukazuje pouze to, že tento proces API odpověděl; neprokazuje, že běží ostatní služby.
- Metriky pilotu jsou deklarované a neověřené; nejsou tvrzením o přijetí.
- Neopravujte data přímo v produkci; změny konfigurace zveřejňujte kontrolovaným způsobem (13.03 §9).

## Když se něco pokazí
- **Health access is unavailable for this context:** zkontrolujte svou roli a rozsah tenanta.
- **Service observations unavailable:** neodvozuje se žádný zdravý stav; po chvíli použijte „Obnovit pozorování“.
- **There is an unresolved conflict:** nasměrujte dotčeného uživatele do centra konfliktů (13.03 §10).
- **Application error:** nahlaste ji podpoře/vývoji s ID korelace, obrazovkou, časem a kroky.

## Související stránky
