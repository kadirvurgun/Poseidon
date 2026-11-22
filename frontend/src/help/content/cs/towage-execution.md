---
key: towage-execution
title: Provedení vlečení
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Konzole provedení zaznamenává **skutečné provedení** odeslané zakázky remorkéru jako časovou osu: zahájení, pozorování, akce **Suspend / Resume / Abort / Complete** a pozdější ověření. Odpovídá na otázku: „Kdy operace začala, kdy bylo vlečné lano připojeno a uvolněno, jaká pozorování byla zaznamenána, v jakém stavu je operace a byl záznam ověřen?“ Zahájení a přechody jsou lidské záznamy Kapitána remorkéru nebo Kapitána na provádějícím remorkéru; konzole nikdy nevydává GO.

## Kdo ji používá
- **Kapitán remorkéru / Kapitán:** zahajuje operaci a zaznamenává časovou osu Start/Suspend/Resume/Abort/Complete a její doklady (13.03 §3).
- **Ověřovatel (osoba s pravomocí ověřovat provedení):** ověřuje dokončený nebo přerušený záznam nebo jeho ověření zamítá (13.03 §1).
- **Koordinátor provozu remorkérů / pobřežní provoz:** sleduje úplnost dokladů a historii dokončených zakázek; nemůže operaci zahájit ani přerušit (13.03 §7).

## Než začnete
- Otevřete remorkér z **Provoz remorkérů** a zvolte zakázku ve **Vyberte odeslanou zakázku remorkéru**.
- Pro zahájení musí být aktuální všechny tyto položky: aktivní přidělení remorkéru, schválený **Plán vleku** (remorkér musí být v plánu), vydaný **Riziko vleku a briefing** potvrzený každým účastníkem a záznam **GO** v **Kontrola provozní připravenosti** vázaný na aktuální posouzení.
- Zahájení a přechody vyžadují **Jmenování** (Kapitán remorkéru nebo Kapitán) pro daný remorkér zaznamenané na serveru.

## Krok za krokem
1. V sekci **Podmínka zahájení** si u každého remorkéru přečtěte **Rozhodnutí o připravenosti** a **Překážky**.
2. Pokud nejsou žádné překážky, přečtěte si jako jmenovaná osoba na palubě text potvrzení, zvolte své **Jmenování**, volitelně zadejte **Počáteční poloha** a stiskněte **Zahájit operaci**.
3. Jakmile je provedení otevřeno, sledujte **Stav**, **Ověření** a pole záznamníku (vlečné lano připojeno/uvolněno, maximální pozorované zatížení vleku, komunikace, naviják, zkouška nouzového uvolnění, aktivní minuty).
4. V **Zaznamenat pozorování** zvolte typ pozorování (například vlečné lano připojeno, zatížení vleku, kontrola komunikace, počasí, stav navijáku, zkouška nouzového uvolnění, poloha, doklad, poznámka), zadejte hodnoty a stiskněte **Zaznamenat pozorování**.
5. V **Úkony velitele remorkéru** zvolte **Akce** (Suspend, Resume, Abort, Complete) a zadejte své **Jmenování** a **Důvod**. Přerušení a dokončení vyžadují důvod o délce alespoň 20 znaků a otevírají pole **Koncová poloha**; dokončení se bez koncové polohy nepřijme.
6. Pokud jsou uvedeny **Položky blokující dokončení**, odstraňte je; je-li například vlečné lano stále připojeno, nejprve zaznamenejte jeho uvolnění.
7. Jako ověřovatel napište důvod a stiskněte **Ověřit záznam** nebo **Zamítnout ověření**.
8. Všechny události uvidíte v pořadí v **Časová osa**.

## Co znamenají stavy
- **Active / Suspended / Completed / Aborted:** stav operace. Dokončeno neznamená, že je záznam ověřen.
- **Pending / Verified / Rejected:** stav ověření. Dokončeno ≠ Ověřeno; ověření je záznam jiné osoby.
- **Good / Lost / Pass / Fail / Conditional / Unknown:** výsledek pozorování komunikace, navijáku a nouzového uvolnění. Unknown ukazuje, že nebylo nic zaznamenáno; nikdy nezastupuje „Good“ ani „Pass“.
- **Safety events — Unknown:** tato konzole nepočítá bezpečnostní události; nevyvozuje se žádný závěr „žádné události“.
- **GO (human record) / NO GO (human record):** rozhodnutí zaznamenané Kapitánem remorkéru nebo Kapitánem v kontrole připravenosti.

## Co nemůžete dělat / upozornění
- Pobřežní uživatelé, AI a systémové identity nemohou operaci zahájit ani přerušit. Přidělení z pobřeží nikdy nepřevažuje nad bezpečnostním rozhodnutím Kapitána remorkéru na palubě (13.03 §7).
- Pozastavit, obnovit, přerušit nebo dokončit smí pouze jmenovaný Kapitán remorkéru nebo Kapitán. Přerušení je k dispozici vždy, dokud operace probíhá.
- Osoba, která operaci zahájila nebo ukončila, nemůže tentýž záznam ověřit; ověření probíhá až po ukončení operace.
- Záznamník umožňuje pouze přidávání; zaznamenané události se nikdy nemažou ani nemění.
- Offline se neodesílá žádný příkaz. Pozorování lze zařadit do fronty v **Offline operace remorkérů**; zahájení, přechody a ověření zůstávají online.

## Když se něco pokazí
- **Zpráva „The server listed missing or blocking items.“:** odstraňte kódy v **Překážky** nebo **Položky blokující dokončení** (například plán neschválen, čeká se na potvrzení briefingu, záznam GO chybí nebo není aktuální).
- **Zpráva „The record changed or the command conflicts.“:** pomocí **Načíst nejnovější** záznam zkontrolujte.
- **Zpráva „The outcome is unknown.“:** **Zkusit znovu** odešle stejný příkaz znovu.
- **Bez oprávnění:** zkontrolujte svou roli, remorkér a kontext jmenování; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).

## Související stránky
