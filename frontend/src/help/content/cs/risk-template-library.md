---
key: risk-template-library
title: Knihovna šablon rizik
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Knihovna šablon rizik spravuje s revizemi šablony hodnocení rizik používané v celém tenantu (druh práce, nebezpečí, výchozí opatření, požadované bariéry, zdroje SMS a požadavků) a metody rizika (matici). Odpovídá na otázku: „Která zveřejněná šablona platí pro tento druh práce a která revize je v platnosti?“ Stránka je **pouze vývojovým cílem**: zveřejnění zde je zaznamenaný lidský záznam HSEQ, nikoli povolení, odborné přijetí rizika ani splnění brány HSE (13.03 §4).

## Kdo ji používá
- **DPA / HSEQ:** v rámci řízení rizik připravuje, posuzuje a zveřejňuje revize šablon, bariéry a matici rizik (13.03 §4).
- **Kapitán, Kapitán remorkéru a posádka:** čtou zveřejněné šablony a posílají k šabloně **Návrhy posádky**.

## Než začnete
- Čtení vyžaduje oprávnění ke čtení šablon. Zápis a zveřejnění šablon vyžadují rozsah celého tenanta a oprávnění k zápisu nebo zveřejnění šablon. Odeslání návrhu vyžaduje pouze oprávnění k navrhování šablon.
- Vytvoření a zveřejnění metody rizika jsou samostatná oprávnění a vyžadují rozsah celého tenanta.
- Příkazy rizik vyžadují aktuální online připojení; offline se nic nezařazuje do fronty ani neschvaluje.

## Krok za krokem
1. Prohlédněte si šablony v seznamu. Každý řádek ukazuje stav, revizi **Aktuální** a počet **Suggestions**. Otevřete šablonu.
2. Pro novou šablonu stiskněte **Nová šablona**. Vyplňte **Kód šablony**, **Název**, **Druh práce**, **Rozsah loď / remorkér**, **Odpovědná osoba schválení**, **Důvod změny**, **Required barriers**, **SMS / requirement sources** a **Nebezpečí** a poté stiskněte **Uložit koncept**.
3. U revize ve stavu konceptu stiskněte **Odeslat ke kontrole**. Chybějící položky se zobrazí jako blokující položky.
4. Osoba zveřejňující šablonu zadá **Platné od** a **Důvod** o délce alespoň 10 znaků a stiskne **Zveřejnit (lidský záznam HSEQ)** nebo **Vrátit**.
5. Chcete-li změnit zveřejněnou šablonu, napište důvod a stiskněte **Zahájit novou revizi**; starší revize se nemění.
6. Posádka napíše návrh do **Odeslat návrh** a odešle jej. Oprávněná osoba jej posoudí pomocí **Přijmout** nebo **Zamítnout** a uvede důvod.
7. V **Metody rizika (matice)** použijte **Nová metoda** k zadání **Kód metody**, **Likelihood codes**, **Severity codes** a **Matrix cells**; jiná osoba ji zveřejní pomocí **Zveřejnit metodu**.

## Co znamenají stavy
- **Draft / In review / Returned / Published / Superseded:** životní cyklus revize šablony.
- **Current / Scheduled / Not effective:** pohled na účinnost. Nejnovější zveřejněná revize, jejíž datum účinnosti již nastalo, je **Aktuální**; revize s budoucím datem je **Naplánováno**. Budoucí revize nikdy nenahradí starší revizi předčasně.
- **Neznámé:** pole není vyplněno; prázdná hodnota se nikdy nevyplňuje výchozí hodnotou.
- **No published risk method: ratings stay not assessed.** Systém nenabízí žádnou výchozí matici ani prahovou hodnotu.

## Co nemůžete dělat / upozornění
- Autor revize ani osoba, která ji odeslala, nemůže tutéž revizi zveřejnit; zveřejnění je samostatný lidský záznam (oddělení povinností).
- Zveřejnění není povolení, odborné přijetí rizika ani schválený produkční katalog.
- Šablona se na hodnocení rizik použije pouze jako **kopie**; nenese žádné hodnocení, podpis, potvrzení účastníků, schválení ani potvrzení přijetí. Když se šablona změní, stávající hodnocení se sama nezmění.
- Zveřejněná revize se nikdy neupravuje; ke změně zahajte novou revizi.

## Když se něco pokazí
- **Blokováno, dokud nebudou uvedené položky doplněny.** Doplňte blokující položky a odešlete znovu.
- **This record changed elsewhere.** Stiskněte **Znovu načíst**, abyste viděli aktuální revizi.
- **Tuto akci nesmíte provést (oddělení povinností nebo rozsah).** Zveřejnění musí provést jiná oprávněná osoba, která revizi nevytvořila; pokud chybí oprávnění, kontaktujte administrátora tenanta (13.03 §10).
- **Tato akce již neodpovídá aktuálnímu stavu. Znovu načtěte.** Stav revize se změnil.

## Související stránky
