---
key: tug-readiness
title: Kontrola připravenosti remorkéru
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Kontrola připravenosti posuzuje provozní připravenost přidělení remorkéru ze zdrojových kontrol **pouze jako doporučení** a umožňuje jmenovanému Kapitánovi remorkéru nebo Kapitánovi zaznamenat odůvodněné **rozhodnutí GO/NO-GO**. Odpovídá na otázku: „Co říkají kontroly posádky, vybavení, PMS, certifikátů, komunikace, počasí, rezervy tahu na háku, přidělení a briefingu a jaké blokující položky existují?“ Posouzení není rozhodnutím: „Systém nikdy nevydává GO; rozhodnutí GO/NO-GO s důvodem zaznamenává pouze jmenovaný Kapitán nebo Kapitán remorkéru.“

## Kdo ji používá
- **Kapitán remorkéru / Kapitán:** posuzuje hodnocení a blokující položky a zaznamenává rozhodnutí GO nebo NO-GO (13.03 §3).
- **Koordinátor provozu remorkérů / pobřežní provoz:** sleduje připravenost a blokující položky; nerozhoduje (13.03 §7).

## Než začnete
- Otevřete stránku z přidělení: na **Přidělení a konflikty** použijte odkaz **Kontrola provozní připravenosti** pod návrhem. Bez zvoleného přidělení stránka zobrazí „Vyberte přidělení ze seznamu přidělení zakázky.“
- Zaznamenání rozhodnutí vyžaduje přístup k zápisu remorkéru a **Jmenování** (Kapitán remorkéru nebo Kapitán) pro daný remorkér zaznamenané na serveru a přidělení musí být stále ve stavu Proposed.
- Bez briefingu neexistuje READY: **Riziko vleku a briefing** musí být vydán a potvrzen každým účastníkem (13.03 §3).

## Krok za krokem
1. Přečtěte si odznak stavu na řádku **Hodnocení**. Poznámka vedle něj uvádí „Doporučení, nikoli rozhodnutí GO.“
2. Zkontrolujte **Požadovaný tah na háku (t)**, **Ověřený dostupný tah na háku (t)** a **Rezerva tahu na háku (t)**. Chybějící hodnota se zobrazí jako „Neznámé“.
3. Zkontrolujte kódy v **Kritické blokátory**, **Důvody přezkumu** a **Podmínky**.
4. V tabulce kategorií uvidíte výsledek každé kategorie (posádka, způsobilost k vlečení, vybavení, vlečné vybavení, PMS a závady, certifikáty, komunikace, počasí, rezerva tahu na háku, přidělení, riziko vleku a briefing). Pro podrobnosti si přečtěte kódy důvodů v tabulce **Kontroly zdrojů**.
5. Pokud máte rozhodovací pravomoc, zvolte ve formuláři **Zaznamenat rozhodnutí kapitána** **NO-GO** nebo **GO**, zadejte své **Jmenování** a **Důvod** a stiskněte **Zaznamenat**.
6. Zaznamenaná rozhodnutí uvidíte v seznamu **Rozhodnutí kapitána**.

## Co znamenají stavy
- **Ready (READY):** všechny zdrojové kontroly vyhovují (žádná blokující položka, neznámá hodnota ani podmínka). Je to informace, nikoli rozhodnutí; rozhodnutí GO stále zaznamenává Kapitán remorkéru nebo Kapitán.
- **Conditional (CONDITIONAL):** platí podmínky uvedené v **Podmínky**. GO je vypnuto a stránka vysvětluje: „Toto posouzení je CONDITIONAL. GO vyžaduje posouzení READY: žádný zdroj nedefinuje, které podmínky by GO dovolovaly, proto jej server odmítá. Vyřešte podmínky a posuďte znovu, nebo zaznamenejte NO-GO.“
- **Not ready (NOT_READY):** existuje alespoň jedna kritická blokující položka.
- **Review required (REVIEW_REQUIRED):** některé zdroje jsou neznámé nebo čekají na posouzení. Neznámá kontrola se nikdy nepočítá jako „Ready“.
- **Pass / Fail / Unknown:** výsledek jedné kategorie nebo kontroly.
- **GO / NO-GO:** lidské rozhodnutí a důvod zaznamenané Kapitánem remorkéru nebo Kapitánem.

## Co nemůžete dělat / upozornění
- GO lze zaznamenat pouze proti posouzení READY; server odmítá GO u CONDITIONAL, NOT_READY nebo REVIEW_REQUIRED. Stránka povoluje GO pouze u posouzení READY; jinak uvádí „GO lze zaznamenat pouze proti posouzení READY.“ NO-GO lze s důvodem zaznamenat vždy.
- Neexistuje READY, dokud jsou riziko a briefing neúplné nebo chybí potvrzení účastníků; volba GO je pak vypnuta (13.03 §3).
- Návrh přidělení od pobřežního uživatele nikdy nepřevažuje nad bezpečnostním rozhodnutím Kapitána remorkéru na palubě (13.03 §7).
- Pokud se posouzení před vaším rozhodnutím změní, rozhodnutí je odmítnuto; znovu zkontrolujte aktuální posouzení.
- Offline jsou příkazy vypnuty.

## Když se něco pokazí
- **Zpráva „The assessment changed“:** použijte **Znovu načíst**, zkontrolujte aktuální posouzení a rozhodněte znovu.
- **Zpráva „The outcome is uncertain“:** **Zkusit znovu** odešle stejný příkaz.
- **Neočekávaná připravenost:** otevřete tabulku kategorií a důvody zdrojových kontrol; projděte je s odpovědnou osobou kontroly nebo odborníkem na danou oblast (13.03 §10).
- **Bez oprávnění:** zkontrolujte svou roli, remorkér a kontext jmenování; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).

## Související stránky
