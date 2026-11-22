---
key: role-uat
title: Záznamy UAT podle rolí
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Záznamy UAT podle rolí zaznamenává běhy uživatelského akceptačního testování (UAT) s rolemi lodi a remorkéru, výsledky jejich scénářů a zjištění jako **lidské důkazy** a porovnává je s dohodnutým prahem. Odpovídá na otázku: „Splňují potvrzené důkazy UAT pro každou roli a každý scénář práh, na kterém se lidé dohodli?“ Platí upozornění v horní části: „Zaznamenává pouze lidské důkazy UAT. Systém nikdy nepřijímá UAT a nikdy nerozhoduje o GO pilotu; výsledky automatických testů jsou přílohy, nikoli PASS.“

## Kdo ji používá
- **Facilitátor UAT:** otevírá běh, zaznamenává pozorované výsledky scénářů a zjištění účastníka a odesílá běh k potvrzení. Role účastníků na lodi zahrnují Kapitána, DPA, Vrchního strojníka a Posádku (13.03 §2, §4, §5, §6).
- **Potvrzovatel:** běh potvrzuje jiná osoba než facilitátor.
- **Vlastníci UAT / Product Owner:** zaznamenávají dohodnutý práh a používají výsledek na stránce **Přezkum go/no-go pilotu**.

## Než začnete
- Zobrazení vyžaduje čtení v rámci celého tenanta a čtení UAT. Zaznamenávání běhů a výsledků, zaznamenání prahu a potvrzování vyžadují každé vlastní oprávnění.
- Vlastníci UAT se musí na prahu dohodnout předem; systém jej nikdy nestanoví.
- Zvolte pro účastníka pseudonym. Nikdy nepoužívejte jméno, e-mail ani číslo člena posádky.
- Zaznamenávání vyžaduje živé připojení; offline je vypnuto.

## Krok za krokem
1. Pomocí tlačítek **Provozní režim** zvolte **Loď** nebo **Remorkér**.
2. V **Důkazy UAT vůči dohodnutému prahu** zkontrolujte stav, počty **Otevřené blokátory**, **Otevřená závažná zjištění**, **Potvrzené běhy** a **Čeká na potvrzení** a tabulku scénářů.
3. Pokud práh neexistuje, vyplňte v **Zaznamenat dohodnutý práh** pole **Minimální úspěšnost scénáře**, **Maximum otevřených závažných zjištění**, **Shoda s designem**, **Odkaz na dohodu** a **Odůvodnění** a stiskněte **Zaznamenat**.
4. V **Běhy UAT** použijte formulář **Nový běh UAT** k zadání **Role**, **Prostředí**, **Commit sestavení** a **Pseudonym účastníka** a poté stiskněte **Zaznamenat**.
5. Otevřete běh. Pomocí **Zaznamenat výsledek** zvolte **Scénář** a **Výsledek**; je-li to relevantní, doplňte **Poskytnuta pomoc** a **Odkaz na automatický důkaz**. Pro problém použijte **Zaznamenat zjištění** se **Závažnost** a **Shrnutí zjištění**.
6. Když máte scénáře zaznamenané, stiskněte **Odeslat běh k potvrzení** (pouze facilitátor). Scénáře bez výsledku se počítají jako **Neprovedeno**. Jiná osoba než facilitátor napíše v **Potvrdit běh** **Prohlášení o potvrzení** a stiskne **Zaznamenat**.
7. V **Otevřená zjištění** uzavřete zjištění s uvedením **Řešení** a **Poznámka k řešení** a poté stiskněte **Vyřešit**.

## Co znamenají stavy
- **Práh nenastaven:** „Není zaznamenán žádný práh dohodnutý lidmi. UAT nemůže projít, dokud nebude zaznamenán.“
- **Criteria not met / Evidence incomplete:** potvrzené důkazy nesplňují práh nebo nejsou pokryty všechny role a scénáře.
- **Kritéria splněna; čeká se na lidské rozhodnutí:** nejlepší možný stav. Není to přijetí UAT ani GO pilotu; rozhodnutí je lidské.
- **Neprovedeno:** pro scénář neexistuje žádný výsledek. Scénář, který nebyl proveden, se nikdy nepočítá jako úspěšný.
- **Open / Submitted / Attested:** stav běhu. Do prahu se počítají pouze potvrzené běhy.
- **Passed / Failed / Blocked / Not run:** pozorovaný výsledek scénáře u účastníka.
- **Blocker / Major / Minor:** závažnost zjištění. Otevřený blokátor znamená nesplnění brány.
- **Fiktivní data** – syntetický vzorový záznam; nejsou to skutečné důkazy UAT.

## Co nemůžete dělat / upozornění
- Nemůžete potvrdit běh, který jste facilitovali: „Potvrzení musí zaznamenat jiný člověk než facilitátor.“
- Nemůžete vyřešit zjištění, které jste nahlásili. Blokátor se uzavírá pouze jako **Vyřešeno (opraveno a znovu otestováno)**; **Riziko přijato** se nenabízí.
- Automatický výsledek (Playwright) je pouze přiložený důkaz; výsledkem je pozorovaný výsledek účastníka.
- Systém nikdy nepřijímá UAT, nikdy nestanovuje práh a nikdy nevytváří smyšlená potvrzení ani podpisy.

## Když se něco pokazí
- **K těmto záznamům nemáte přístup.** Zkontrolujte svou roli a rozsah tenanta a kontaktujte Tenant Admin (13.03 §10).
- **Záznam nebyl ve vašem tenantovi nalezen.** Zkontrolujte odkaz na běh a zvolený režim.
- **Záznam se změnil nebo již byl použit. Znovu načtěte.** Otevřete aktuální stav pomocí **Znovu načíst**.
- **Výsledek je nejistý. Opakování odešle stejný příkaz.** Pomocí **Zkusit znovu** bezpečně znovu odešlete stejný příkaz.
- **Odmítnuto pravidly.** Například scénář nepatří k roli běhu nebo blokátor nelze uzavřít jako přijaté riziko.
- **Nepovoleno.** Například jste tento běh facilitovali a nemůžete jej potvrdit, nebo jste toto zjištění nahlásili a nemůžete jej vyřešit.
- **Jste offline. Záznam je vypnut, dokud se připojení neobnoví.** Až se připojení obnoví, stiskněte **Obnovit**.

## Související stránky
