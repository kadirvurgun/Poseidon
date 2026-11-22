---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## Na čo slúži táto stránka
Stránka Users and permissions (Používatelia a oprávnenia) spravuje **členstvá** vo vašom pracovnom priestore. Odpovedá na otázku: „Čo môže táto osoba vidieť a robiť a na ktorých lodiach?“ Samotný účet (používateľské meno, heslo, MFA) sa nachádza v centrálnom poskytovateľovi identity (IdP), nie v POSEIDON. Táto stránka **pozýva** daný účet do členstva a udeľuje balíky rolí, oprávnenia a rozsah lodí (13.03 §9). Nový člen je pozvaný: pozvánka neudeľuje nič, kým sa osoba neprihlási a pozvánku neprijme.

## Kto ju používa
- **Tenant administrator:** prideľuje rozsah používateľa, roly, lode a objektu s minimálnymi oprávneniami, pozýva nových členov a spravuje delegovania a pozastavenie (13.03 §9).
- Ostatní používatelia s prístupom na čítanie členstiev môžu zoznam zobraziť, ale nie meniť.
- **Pozvaná osoba** otvára iba odkaz pozvánky; túto stránku nikdy nevidí.

## Skôr než začnete
- Zobrazenie vyžaduje čítanie členstiev; zmeny vyžadujú správu členstiev a navyše správu v rozsahu celého nájomcu (tenant). Iba s prístupom na čítanie uvidíte „You can view but not change memberships.“
- Osoba potrebuje účet u poskytovateľa identity. **Expected IdP subject (optional)** obmedzuje pozvánku na tento jeden účet; bez neho ju môže prijať ktokoľvek, kto sa s odkazom prihlási.
- Naplánujte dôveryhodný kanál na odovzdanie odkazu pozvánky (napríklad vlastný systém správ vašej organizácie). POSEIDON neposiela e-maily.
- Môžete udeliť iba oprávnenia, ktoré sami máte. Vaše vlastné členstvo upravuje iný administrátor.
- Správa členstiev vyžaduje živé spojenie so serverom; offline nie je možné nič zmeniť.

## Krok za krokom
1. Členov si pozrite v zozname **Members**. Filtrujte podľa **Stav** (napríklad **Invited**) a **Role bundle**, potom stlačte **Apply**. Ak chcete vyhľadávať, zadajte predponu ID členstva alebo účtu alebo presný IdP subject a stlačte **Hľadať**.
2. Otvorte člena pomocou **Otvoriť**. Skontrolujte **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** a **Delegations**.
3. Ak chcete pozvať člena, stlačte **Add member**. Skontrolujte **IdP issuer**, voliteľne zadajte **Expected IdP subject (optional)**, zvoľte aspoň jeden **Role bundle**, v prípade potreby pridajte **Individual permissions** a nastavte rozsah (celý nájomca alebo aspoň jedna loď). Potom stlačte **Create membership**.
4. Člen sa otvorí ako **Invited** s **Invitation link**. Stlačte **Copy invitation link** a odkaz odovzdajte. Odkaz sa zobrazí iba teraz; funguje raz a platnosť mu vyprší po 24 hodinách.
5. Ak sa odkaz stratil alebo mu vypršala platnosť, stlačte **Create a new invitation link**; predchádzajúci odkaz prestane fungovať. Ak chcete pozvánku stiahnuť, stlačte **Revoke invitation**.
6. Pozvaná osoba otvorí odkaz, prihlási sa, skontroluje, čo pozvánka udeľuje, a stlačí **Accept invitation**. Po opätovnom prihlásení pracuje s udelením, ktoré ste nastavili; člen potom zobrazuje **Active** a **Invitation accepted**.
7. Ak chcete zmeniť udelenie existujúceho člena, stlačte **Edit grant**, potom **Save grant**. Každé uloženie vytvorí novú revíziu udelenia; staršie revízie zostávajú v **Grant history**.
8. Na dočasné odovzdanie použite **Delegate from this member** a vyplňte delegáta, **Valid from**, **Platné do**, **Dôvod** a **Delegated permissions**.
9. Na zastavenie prístupu použite **Suspend** v **Lifecycle**; **Resume** ho znova otvorí. **Revoke membership** je konečné.

## Čo znamenajú stavy
- **Invited:** členstvo čaká na pozvanú osobu. Neudeľuje nič a nikde si účet nerezervuje, ani v žiadnej inej organizácii.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** stav najnovšieho odkazu pozvánky. Odkaz, ktorému vypršala platnosť, ktorý bol nahradený alebo odvolaný, nie je možné nikdy prijať.
- **Active / Suspended / Revoked:** životný cyklus členstva. Ďalšia požiadavka pozastaveného alebo odvolaného člena je zamietnutá.
- **Grant source — Managed here:** udelenie bolo dané na tejto stránke a je možné ho tu zmeniť.
- **Grant source — Operator configuration:** udelenie pochádza z konfigurácie servera (bootstrap/break-glass) a nie je možné ho tu upraviť.
- **Grant source — No grant:** záznam existuje, ale neudeľuje nič.
- **Not held by you / You do not hold every permission of this bundle:** toto oprávnenie alebo balík nemôžete udeliť, pretože ho nemáte.
- **Saved. The change takes effect on the member's next request.** Prístup pozvaného člena sa začína pri ďalšom prihlásení po prijatí.

## Čo nemôžete robiť / upozornenie
- Nemôžete udeliť oprávnenie, loď ani prístup v rozsahu celého nájomcu, ktoré nemáte.
- Nemôžete zmeniť svoje vlastné členstvo; posledného aktívneho administrátora nie je možné odstrániť.
- Účet môže mať prijaté členstvo iba v jednej organizácii. Pozvánka nikdy nepresunie účet, ktorý už patrí inam; pozvaná osoba potom vidí iba to, že pozvánku nie je možné prijať.
- S odkazom pozvánky zaobchádzajte ako s kľúčom: ktokoľvek, kto ho má a prihlási sa, ho môže prijať, pokiaľ ste nezadali očakávaný subject. Nezverejňujte ho v zdieľaných kanáloch. Vždy odovzdajte celý odkaz: tajnú časť za **#** číta iba prehliadač a nikdy sa nedostane na server ani do jeho záznamov.
- Oprávnenia na rozhodovanie, overovanie a schvaľovanie nikdy nie sú v balíku. Udeliť ich môže iba administrátor, ktorý ich má, a to jednotlivo. Akcie viazané na menovanie (napríklad rozhodnutia o remorkéri alebo odchode) vyžadujú aj menovanie operátorom. Udelenie balíka rolí nikoho nemenuje za kapitána ani overovateľa.
- Nemôžete schváliť delegovanie, v ktorom ste zdrojom alebo delegátom. Delegovanie nikdy nepresahuje aktuálnu právomoc zdroja a nikdy nie je v rozsahu celého nájomcu.
- POSEIDON neukladá žiadne heslá, uchováva iba odtlačok odkazu pozvánky a nikdy nezobrazuje IdP subjects späť v zozname.

## Ak sa niečo pokazí
- **This identity is configured by the operator, already a member here or already invited here.** Namiesto toho otvorte existujúceho člena alebo najprv odvolajte starú pozvánku.
- **Pozvaná osoba vidí „This invitation cannot be accepted“:** odkazu vypršala platnosť, bol použitý, nahradený alebo odvolaný, patrí inému účtu, alebo jej účet už patrí niektorej organizácii. Skontrolujte stav **Invitation** a v prípade potreby stlačte **Create a new invitation link**.
- **Odkaz sa neskopíroval:** označte pole odkazu a skopírujte ho ručne.
- **Exceeds your own authority:** zvoľte užší balík alebo rozsah, alebo požiadajte administrátora, ktorý má dané oprávnenie.
- **Záznam sa medzitým zmenil:** stlačte **Reload current state** a urobte zmenu znova.
- **The outcome is unknown. Retry with the same request or reload.** Ten istý príkaz môžete bezpečne odoslať znova.
- **Not the trusted identity provider:** skontrolujte vydavateľa (issuer). Ak účet pochádza od iného IdP, kontaktujte systémového administrátora (13.03 §10).

## Súvisiace stránky
