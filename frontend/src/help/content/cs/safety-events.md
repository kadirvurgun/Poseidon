---
key: safety-events
title: Bezpečnostní události a skoronehody
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: [1]
reviewed: false
---
## K čemu tato stránka slouží
Stránka Bezpečnostní události a skoronehody umožňuje rychle nahlásit bezpečnostní událost na plavidle a sledovat lidské kroky, které po ní následují: prvotní posouzení, šetření, nezávislé posouzení, nápravná a preventivní opatření, ověření, účinnost a uzavření. Odpovídá na otázku: „Co se stalo, kdo to řeší a zafungovalo opatření skutečně?“ Nic zde automaticky nerozhoduje o závažnosti, hlavní příčině ani uzavření; každé z toho je samostatný lidský záznam. Stránka sleduje řetězec Zjištění → Opatření → Ověření a **uzavřeno neznamená totéž co účinné** (13.03 §4).

## Kdo ji používá
- **Posádka / OOW / strojník a Kapitán:** hlásí skoronehody, incidenty, nehody, únavu, environmentální nebo kybernetické události (13.03 §2, §6).
- **DPA / HSEQ:** jako bezpečnostní most sleduje kritické události a opožděná opatření a řídí šetření a účinnost opatření (13.03 §4).
- Prvotní posouzení, šetření, posouzení, čtení identity, zápis opatření, ověření a posouzení účinnosti jsou samostatná oprávnění; každý uživatel vidí pouze části, ke kterým má oprávnění.

## Než začnete
- Zvolte správné plavidlo (**Vyberte plavidlo**). Bez přístupu uvidíte „K bezpečnostním událostem tohoto plavidla nemáte přístup.“
- Hlášení vyžaduje oprávnění k hlášení; čtení seznamu vyžaduje oprávnění ke čtení.
- Hlášení **Důvěrně** a **Odeslat hlášení** vyžadují připojení. Offline můžete uložit pouze jmenovitý koncept, a to pouze na předem připraveném zařízení.
- Čas se zadává v UTC.

## Krok za krokem
1. Prohlédněte si události plavidla v seznamu **Události**. Pomocí **Pouze moje hlášení** zobrazíte jen svá vlastní.
2. Pro nový záznam stiskněte **Nahlásit událost**. Jako **Způsob hlášení** zvolte **Jmenovitě** nebo **Důvěrně**. U důvěrného hlášení, které vyžaduje další komunikaci, zvolte **Chci soukromý kód pro další komunikaci**.
3. Vyplňte **Typ události**, **Kdy k tomu došlo?**, **Operace**, **Místo na palubě**, **Shrnutí**, **Co se stalo**, **Bylo přijato okamžité opatření?** a volitelně **Váš odhad závažnosti**. Pokud čas není znám, zvolte **Neznámé** a vyplňte **Proč není čas znám?**.
4. Dokud je hlášení neúplné, můžete použít **Uložit koncept**. Když je **Před odesláním je ještě potřeba** prázdné, stiskněte **Odeslat hlášení**. Neodeslaný koncept lze stáhnout pomocí **Stáhnout koncept** s uvedením důvodu.
5. Pokud byl vydán kód pro další komunikaci, ihned si kód z **Váš důvěrný kód pro další komunikaci** soukromě uložte a stiskněte **Kód jsem si uložil(a)**. Zobrazí se pouze jednou. Později jej zadejte jako **Kód pro další komunikaci** v části **Důvěrná další komunikace** a stiskněte **Open**, abyste si přečetli odpovědi týmu.
6. Osoba provádějící prvotní posouzení zaznamená v **Prvotní posouzení** závažnost a **Vyžadováno šetření** s důvodem a stiskne **Zaznamenat třídění**, nebo hlášení vrátí pomocí **Vrátit oznamovateli**.
7. Vyšetřovatel stiskne **Zahájit šetření**, vyplní **Přispívající faktory**, **Hlavní příčiny**, **Bariéry** a **Nejistoty** a poté stiskne **Uložit šetření** a **Odeslat ke kontrole**. V **Nezávislé posouzení** zvolí jiná osoba **Schválit**, **Vrátit** nebo **Zamítnout**.
8. V **Nápravná a preventivní opatření** použijte **Přidat opatření** s odpovědnou osobou, termínem a zjištěním, které opatření řeší. Odpovědná osoba zaznamená **Nahlásit dokončení**, jiná osoba zaznamená **Ověřit** a třetí osoba zaznamená **Posoudit účinnost**.
9. Když je **Překážky uzavření** prázdné, posuzovatel (s oprávněním k posouzení), který není oznamovatelem ani autorem šetření, stiskne **Uzavřít událost**. V **Zprávy** můžete posílat zprávy s viditelností **Pouze tým** nebo **Sdíleno s oznamovatelem**.

## Co znamenají stavy
- **Draft / Submitted / Returned / Triaged / Investigating / Action tracking / Closed / Withdrawn:** životní cyklus události.
- **Severity — Not triaged / Unassessed:** dosud žádná lidská klasifikace. **Odhad oznamovatele** je pouze návrh. **Severity policy: not configured – human classification only** ukazuje, že systém žádnou závažnost neodvozuje.
- **Potřeba šetření je UNKNOWN:** nebylo učiněno žádné rozhodnutí; neznamená to „ne“ a blokuje to uzavření.
- **Action — Open / Completed / Verified:** Dokončeno není Ověřeno a Ověřeno není Účinné.
- **Effectiveness — Pending / Effective / Not effective / Inconclusive / No actions / Not visible:** účinnost je lidské posouzení pro každé opatření a je oddělená od uzavření. **Žádná opatření** neznamená účinné.
- Souhrn **Otevřené bezpečnostní události**: **Otevřené události s vysokou závažností**, **Vyžaduje posouzení** nebo **Žádná otevřená událost s vysokou závažností**. Dokud nejsou nakonfigurovány zásady závažnosti, zobrazuje se místo tohoto stavu „vše v pořádku“ **Zásady závažnosti nejsou nakonfigurovány – vyžadováno posouzení**, protože události s vysokou závažností nelze vyloučit. Je to pouze upozornění; nedává žádné rozhodnutí o připravenosti.
- **Důvěrné (chráněno):** identita oznamovatele je v omezeném trezoru a čtenářům se nezobrazuje.
- **Captured offline / Waiting to sync / Saved on this device. Not yet sent.** Zařazeno do fronty neznamená odesláno.

## Co nemůžete dělat / upozornění
- Oznamovatel nemůže provést prvotní posouzení vlastního hlášení. Autor šetření, osoba, která je odeslala, ani oznamovatel nemohou provést nezávislé posouzení.
- Dokončení hlásí pouze odpovědná osoba opatření. Ověřit nemůže ani odpovědná osoba, ani ten, kdo opatření dokončil; účinnost nemůže posoudit nikdo z odpovědné osoby, toho, kdo dokončil, ani ověřovatele. Jde o samostatné záznamy různých osob.
- Hlavní příčiny jsou lidská tvrzení; systém žádnou nikdy nenavrhuje.
- Odeslané hlášení nemůže jeho autor nikdy stáhnout ani smazat.
- U důvěrného hlášení se identita odkrývá pouze na základě nezbytné potřeby, a to osobou s oprávněním ke čtení identity, která zvolí **Účel**; každé čtení se zaznamená spolu s účelem. Anonymní hlášení vyžaduje veřejný příjmový kanál, který není nakonfigurován.
- Posouzení s výsledkem Inconclusive lze zopakovat; Effective a Not effective jsou konečné; opatření, které nezafungovalo, vyžaduje nové navazující opatření.

## Když se něco pokazí
- **The server refused the command:** doplňte chybějící nebo blokující položky uvedené pod hlášením.
- **Záznam se mezitím změnil. Před dalším pokusem jej znovu načtěte a zkontrolujte.** Stiskněte **Znovu načíst**.
- **Výsledek je neznámý (spojení ztraceno).** Použijte **Zopakovat stejný příkaz**; nebude použit dvakrát.
- **Tato akce vám není povolena.** Zkontrolujte svou roli, plavidlo a kontext objektu; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **The device key is unavailable** nebo **Online re-authorisation is needed:** zachycená data zůstávají zachována; po připojení se znovu ověřte. U chyby aplikace kontaktujte podporu s ID korelace, obrazovkou, časem a kroky (13.03 §10).

## Související stránky
