---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## K čemu tato stránka slouží
Stránka Users and permissions spravuje **členství** ve vašem pracovním prostoru. Odpovídá na otázku: „Co může tato osoba vidět a dělat a na kterých plavidlech?“ Samotný účet (uživatelské jméno, heslo, MFA) se nachází v centrálním poskytovateli identit (IdP), nikoli v POSEIDON. Tato stránka tento účet **zve** do členství a uděluje balíčky rolí, oprávnění a rozsah plavidel (13.03 §9). Nový člen je pozván: pozvánka neuděluje nic, dokud se osoba nepřihlásí a pozvánku nepřijme.

## Kdo ji používá
- **Tenant administrator:** přiřazuje rozsah uživatele, role, plavidla a objektu podle principu nejmenších oprávnění, zve nové členy a spravuje delegace a pozastavení (13.03 §9).
- Ostatní uživatelé s přístupem ke čtení členství mohou seznam zobrazit, ale ne měnit.
- **Pozvaná osoba** pouze otevře odkaz pozvánky; tuto stránku nikdy nevidí.

## Než začnete
- Zobrazení vyžaduje čtení členství; změny vyžadují správu členství a navíc správu v rámci celého tenanta. Pouze s přístupem ke čtení uvidíte „Členství můžete zobrazit, ale ne měnit.“
- Osoba potřebuje účet u poskytovatele identit. **Expected IdP subject (optional)** omezuje pozvánku na tento jeden účet; bez něj ji může přijmout kdokoli, kdo se přihlásí pomocí odkazu.
- Naplánujte důvěryhodný kanál pro předání odkazu pozvánky (například vlastní systém zpráv vaší organizace). POSEIDON neodesílá e-maily.
- Můžete udělit pouze oprávnění, která sami máte. Vaše vlastní členství upravuje jiný administrátor.
- Správa členství vyžaduje živé spojení se serverem; offline nelze nic změnit.

## Krok za krokem
1. Prohlédněte si členy v seznamu **Members**. Filtrujte podle **Stav** (například **Invited**) a **Role bundle** a poté stiskněte **Apply**. Pro vyhledávání zadejte předponu ID členství nebo účtu nebo přesný subjekt IdP a stiskněte **Hledat**.
2. Otevřete člena pomocí **Open**. Zkontrolujte **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** a **Delegations**.
3. Chcete-li pozvat člena, stiskněte **Add member**. Zkontrolujte **IdP issuer**, volitelně zadejte **Expected IdP subject (optional)**, zvolte alespoň jeden **Role bundle**, v případě potřeby přidejte **Individual permissions** a nastavte rozsah (celý tenant nebo alespoň jedno plavidlo). Poté stiskněte **Create membership**.
4. Člen se otevře jako **Invited** s **Invitation link**. Stiskněte **Copy invitation link** a odkaz předejte. Odkaz se zobrazí pouze nyní; funguje jednou a po 24 hodinách vyprší.
5. Pokud byl odkaz ztracen nebo vypršel, stiskněte **Create a new invitation link**; předchozí odkaz přestane fungovat. Chcete-li pozvánku stáhnout, stiskněte **Revoke invitation**.
6. Pozvaná osoba otevře odkaz, přihlásí se, zkontroluje, co pozvánka uděluje, a stiskne **Accept invitation**. Po opětovném přihlášení pracuje s oprávněními, která jste nastavili; člen pak ukazuje **Aktivní** a **Invitation accepted**.
7. Chcete-li změnit udělená oprávnění existujícího člena, stiskněte **Edit grant** a poté **Save grant**. Každé uložení vytvoří novou revizi udělení; starší revize zůstávají v **Grant history**.
8. Pro dočasné předání použijte **Delegate from this member** a vyplňte zástupce, **Valid from**, **Platné do**, **Důvod** a **Delegated permissions**.
9. Pro zastavení přístupu použijte **Pozastavit** v **Lifecycle**; **Obnovit** přístup znovu otevře. **Revoke membership** je konečné.

## Co znamenají stavy
- **Invited:** členství čeká na pozvanou osobu. Neuděluje nic a nikde neblokuje účet, ani v žádné jiné organizaci.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** stav posledního odkazu pozvánky. Vypršelý, nahrazený nebo odvolaný odkaz nelze nikdy přijmout.
- **Active / Suspended / Revoked:** životní cyklus členství. Další požadavek pozastaveného nebo odvolaného člena je zamítnut.
- **Grant source — Managed here:** udělení bylo provedeno na této stránce a lze jej zde změnit.
- **Grant source — Operator configuration:** udělení pochází z konfigurace serveru (bootstrap/break-glass) a zde jej nelze upravit.
- **Grant source — No grant:** záznam existuje, ale nic neuděluje.
- **Not held by you / You do not hold every permission of this bundle:** toto oprávnění nebo balíček nemůžete udělit, protože jej nemáte.
- **Saved. The change takes effect on the member's next request.** Přístup pozvaného člena začíná při prvním přihlášení po přijetí.

## Co nemůžete dělat / upozornění
- Nemůžete udělit oprávnění, plavidlo ani přístup v rámci celého tenanta, které sami nemáte.
- Nemůžete změnit své vlastní členství; posledního aktivního administrátora nelze odebrat.
- Účet může mít přijaté členství pouze v jedné organizaci. Pozvánka nikdy nepřesune účet, který již patří jinam; pozvaná osoba pak uvidí pouze to, že pozvánku nelze přijmout.
- S odkazem pozvánky zacházejte jako s klíčem: kdokoli, kdo jej má a přihlásí se, jej může přijmout, pokud jste nezadali očekávaný subjekt. Nezveřejňujte jej ve sdílených kanálech. Vždy předávejte celý odkaz: tajnou část za **#** čte pouze prohlížeč a nikdy se nedostane na server ani do jeho protokolů.
- Oprávnění k rozhodování, ověřování a schvalování nejsou nikdy součástí balíčku. Udělit je může pouze administrátor, který je sám má, a to jednotlivě. Akce podmíněné jmenováním (například rozhodnutí o remorkéru nebo o vyplutí) vyžadují také jmenování operátorem. Udělení balíčku rolí nikoho nejmenuje Kapitánem ani ověřovatelem.
- Nemůžete schválit delegaci, ve které jste zdrojem nebo zástupcem. Delegace nikdy nepřekračuje aktuální pravomoc zdroje a nikdy se nevztahuje na celý tenant.
- POSEIDON neukládá žádná hesla, uchovává pouze otisk odkazu pozvánky a subjekty IdP v seznamu nikdy zpětně nezobrazuje.

## Když se něco pokazí
- **This identity is configured by the operator, already a member here or already invited here.** Místo toho otevřete existujícího člena, nebo nejprve odvolejte starou pozvánku.
- **Pozvaná osoba vidí „This invitation cannot be accepted“:** odkaz vypršel, byl použit, nahrazen nebo odvolán, patří jinému účtu, nebo její účet již patří nějaké organizaci. Zkontrolujte stav **Invitation** a v případě potřeby stiskněte **Create a new invitation link**.
- **Odkaz nebyl zkopírován:** označte pole s odkazem a zkopírujte jej ručně.
- **Exceeds your own authority:** zvolte užší balíček nebo rozsah, nebo požádejte administrátora, který dané oprávnění má.
- **The record changed meanwhile:** stiskněte **Reload current state** a proveďte svou změnu znovu.
- **The outcome is unknown. Retry with the same request or reload.** Stejný příkaz můžete bezpečně odeslat znovu.
- **Not the trusted identity provider:** zkontrolujte vydavatele. Pokud účet pochází z jiného IdP, kontaktujte systémového administrátora (13.03 §10).

## Související stránky
