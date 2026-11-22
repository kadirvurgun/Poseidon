---
key: system-health
title: Stav systému
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
„Stav systému“ zobrazuje administrátorom **pozorovania služieb** na jednej obrazovke: API, zapisovač auditu, synchronizáciu, úlohy oznámení, súborovú službu a aktuálnosť vstupov. Odpovedá na otázku: „Ktoré signály služieb boli pozorované pre zvolenú loď a tohto nájomcu a ktoré nie?“ Konfigurácia a chýbajúca telemetria nezakladajú zdravú službu ani pripravenosť lode.

## Kto ju používa
- **Systémový administrátor / Tenant Admin:** na zachytenie problémov so službami a synchronizáciou a ich kontrolované riešenie (13.03 §9).
- **Podpora / vývoj:** na posúdenie pozorovaní, keď je nahlásená chyba aplikácie (13.03 §10).

## Skôr než začnete
- Stránka je viditeľná iba používateľom s prístupom na čítanie v rozsahu celého nájomcu, čítanie histórie akcií a čítanie stavu systému.
- V hornej kontextovej lište skontrolujte, či je zvolená **správna spoločnosť**.
- Pozorovania sa vyžadujú pre každú loď; najprv musíte zvoliť loď.

## Krok za krokom
1. Zvoľte loď v **Rozsah pozorovaní plavidla**. Pri dlhom zozname použite **Predchádzajúce plavidlá** / **Ďalšie plavidlá**.
2. Skontrolujte čas „Načítané o“.
3. V **Dostupnosť služby** si prečítajte čas spustenia procesu a dobu prevádzky.
4. V kartách **API** a **Zapisovač auditu** si prečítajte údaje o požiadavkách, chybách, uložených záznamoch a latencii. Pokrývajú tohto nájomcu od spustenia procesu.
5. V **Synchronizácia** uvidíte potvrdené výsledky, nevyriešené konflikty a poslednú potvrdenú synchronizáciu.
6. V **Úlohy oznámení** uvidíte otvorené oznámenia, eskalácie po termíne a stav vyhodnocovacej úlohy.
7. Prečítajte si karty **Súborová služba** a **Aktuálnosť vstupu**.
8. V časti **Metriky pilotu** posúďte pozorovania zo zaregistrovaných pilotných klientov.
9. Pre aktuálne údaje stlačte **Obnoviť pozorovania**.

## Čo znamenajú stavy
- **Nepozorované / Nedostupné:** pre tento signál neexistuje žiadne pozorovanie. Neznamená to „zdravé“ ani „žiadny problém“.
- **Nastavené · neotestované sondou:** súborová služba sa javí ako nastavená, ale nebola vykonaná žiadna živá sonda úložiska ani skenera.
- **V rámci plánu / Prebieha / Po termíne / Plánovač nie je povolený / Beh opustený (zámok vypršal):** stav vyhodnocovacej úlohy oznámení.
- **Nevyriešené konflikty:** offline konflikty čakajúce na ľudské vyriešenie. Nikdy sa nezlučujú automaticky.
- **Aktuálne / Zastarané / Neznáme / Nedostupné:** počty posúdení aktuálnosti vstupov. Neznáme neznamená „Aktuálne“.
- **This snapshot is older than two minutes:** pred spoľahnutím sa naň obnovte údaje.

## Čo nemôžete robiť / upozornenie
- Táto stránka je iba na čítanie; na službách, lodiach ani záznamoch sa nevykonáva žiadna akcia.
- Záznamy oznámení existujú iba v aplikácii; nie sú dôkazom doručenia ani žiadneho bezpečnostného rozhodnutia.
- Dostupnosť služby ukazuje iba to, že tento proces API odpovedal; nedokazuje, že ostatné služby bežia.
- Metriky pilotu sú deklarované a neoverené; nie sú tvrdením o akceptácii.
- Neopravujte údaje priamo v produkcii; zmeny konfigurácie zverejňujte kontrolovaným spôsobom (13.03 §9).

## Ak sa niečo pokazí
- **Health access is unavailable for this context:** skontrolujte svoju rolu a rozsah nájomcu.
- **Service observations unavailable:** nevyvodzuje sa žiadny zdravý stav; po chvíli použite „Obnoviť pozorovania“.
- **There is an unresolved conflict:** nasmerujte dotknutého používateľa do Centra konfliktov (13.03 §10).
- **Application error:** nahláste ju podpore/vývoju s ID korelácie, obrazovkou, časom a krokmi.

## Súvisiace stránky
