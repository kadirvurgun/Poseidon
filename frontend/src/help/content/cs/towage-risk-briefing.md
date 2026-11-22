---
key: towage-risk-briefing
title: Riziko vleku a briefing
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka připravuje a vydává **záznam o riziku vleku a briefingu** pro odeslanou zakázku remorkéru a shromažďuje **potvrzení** účastníků. Odpovídá na otázku: „Jaké podmínky, nebezpečí, preventivní a zmírňující bariéry, pravomoc zastavit práci a nouzová opatření byly na briefingu probrány, v jakém stavu je propojené posouzení rizik a potvrdil každý účastník vydanou revizi?“ Vydání briefingu je lidský záznam jmenovaného Kapitána nebo Kapitána remorkéru; není to rozhodnutí GO ani povolení. Neúplný briefing blokuje READY.

## Kdo ji používá
- **Kapitán remorkéru / Kapitán:** připravuje a vydává briefing a zajišťuje, aby nebylo učiněno žádné rozhodnutí READY dříve, než jsou riziko a briefing úplné (13.03 §3).
- **Posádka a další účastníci (lodivod, důstojník, palubní posádka, strojník):** potvrzují nebo rozporují vydaný briefing ve správném kontextu role (13.03 §6).
- **Koordinátor provozu remorkérů:** sleduje stav briefingu a potvrzení (13.03 §7).

## Než začnete
- Otevřete remorkér z **Provoz remorkérů** a zvolte zakázku ve **Vyberte odeslanou zakázku remorkéru**.
- **Plán vleku** zakázky musí být schválen a propojené posouzení rizik schváleno; jinak briefing nelze vydat.
- Vydání vyžaduje **Jmenování** (Kapitán remorkéru nebo Kapitán) pro koordinující remorkér zaznamenané na serveru. Vydání vyžaduje také přístup ke čtení posouzení rizik a zápis remorkéru na koordinujícím remorkéru. Potvrzení je určeno pouze osobám ze seznamu účastníků.

## Krok za krokem
1. Na horním řádku si přečtěte **Stav**, **Revize**, **Potvrzení**, **Stav rizika** a **Stav plánu**.
2. V **Vstup připravenosti (neúplný briefing blokuje READY)** uvidíte výsledky a kódy důvodů odeslané do kontroly připravenosti.
3. Vyplňte **Podmínky potvrzené na briefingu** (počasí, proud, viditelnost), **Nebezpečí**, **Preventivní bariéry**, **Zmírňující bariéry**, **Pravomoc zastavit práci** a **Nouzová opatření**.
4. Zadejte **ID propojeného posouzení rizik**; v **Účastníci** použijte **Přidat** k zadání **ID osoby** a **Role** každého účastníka.
5. Uložte pomocí **Vytvořit koncept** nebo **Uložit koncept**; když je **Chybí před odesláním** prázdné, zvolte **Jmenování** a stiskněte **Vydat briefing**.
6. Jako účastník si přečtěte vydanou revizi a stiskněte **Potvrdit**. Pokud nesouhlasíte, napište **Důvod** o délce alespoň 10 znaků a stiskněte **Zpochybnit**.
7. Pokud se musí briefing změnit, napište důvod a použijte **Upravit**. Seznamy **Potvrzení převzetí** a **Revize** ukazují historii.

## Co znamenají stavy
- **Draft / Issued:** stav briefingu. Vydáno znamená, že mohou začít potvrzení; samo o sobě to není výsledek připravenosti.
- **Not issued / Pending / Complete / Disputed:** stav potvrzení účastníků. Stav **Dokončeno** nastane pouze tehdy, když vydanou revizi potvrdil každý účastník.
- **Approved and acknowledged / Attention / Blocked / Review required:** stav propojeného posouzení rizik.
- **Pass / Fail / Unknown:** výsledek kontroly odeslaný do kontroly připravenosti. Chybějící nebo nevydaný briefing a čekající potvrzení jsou **Nevyhovuje**, což blokuje READY.

## Co nemůžete dělat / upozornění
- Žádné rozhodnutí READY nepadne dříve, než jsou riziko a briefing úplné a potvrzení účastníků zaznamenána (13.03 §3).
- Rozpor změní stav potvrzení na **Zpochybněno** a odešle Fail do kontroly připravenosti, takže READY a GO jsou blokovány. Důvod se zobrazí v **Potvrzení převzetí**. Potvrzení nelze pro tutéž revizi změnit; k odstranění rozporu se briefing upraví (nová revize), znovu vydá a znovu potvrdí každým účastníkem.
- Pokud se plán nebo posouzení rizik po briefingu změní, musí být briefing znovu posouzen; dřívější potvrzení se pro novou revizi nepočítají.
- Nikdo nepotvrzuje za jiného; každý účastník zaznamenává své vlastní potvrzení (13.03 §6).
- Offline se z této stránky neodesílá žádný příkaz. Potvrzení lze zařadit do fronty v **Offline operace remorkérů**.

## Když se něco pokazí
- **Zpráva „Required sections are missing.“:** doplňte chybějící kódy; zkontrolujte, že plán a posouzení rizik jsou schváleny.
- **Zpráva „The record changed“:** pomocí **Znovu načíst** zkontrolujte aktuální briefing.
- **Zpráva „The outcome is unknown“:** **Zkusit znovu** odešle stejný požadavek znovu.
- **Bez oprávnění:** zkontrolujte svou roli, remorkér a kontext jmenování; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).

## Související stránky
