---
key: towage-plan
title: Plán vleku
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Plán vleku zaznamenává pro odeslanou zakázku remorkéru **role a konfiguraci remorkérů, výměnu informací, provozní limity, komunikaci, nouzové uvolnění, pohotovostní opatření a opatření pro zónu zpětného rázu lana (snap-back)**. Odpovídá na otázku: „Jak bude tato zakázka provedena, v jakých limitech a s jakými nouzovými opatřeními?“ Schválení plánu je lidský záznam jmenovaného Kapitána nebo Kapitána remorkéru; není to rozhodnutí GO ani povolení. Limity jsou vstupy plánu uvedené se svým zdrojem; systém nedodává žádné výchozí hodnoty.

## Kdo ji používá
- **Koordinátor provozu remorkérů / plánovač:** připravuje plán a odesílá jej ke schválení (13.03 §7).
- **Kapitán remorkéru / Kapitán:** potvrzuje konfiguraci, lano, BP/schopnosti, komunikaci, limity, pohotovostní opatření a podmínky zastavení práce a plán schvaluje nebo vrací (13.03 §3).

## Než začnete
- Otevřete remorkér z **Provoz remorkérů** a zvolte zakázku ve **Vyberte odeslanou zakázku remorkéru**.
- Úprava plánu vyžaduje přístup k zápisu remorkéru a zápisu plánu. Remorkéry v plánu se volí z remorkérů s aktuálním návrhem přidělení pro tuto zakázku.
- Schválení nebo vrácení vyžaduje **Jmenování** (Kapitán remorkéru nebo Kapitán) pro koordinující remorkér zaznamenané na serveru.

## Krok za krokem
1. Přečtěte si **Stav** a **Revize** a poté seznam **Chybí před odesláním**.
2. V **Role a konfigurace remorkérů** použijte pro každý remorkér **Přidat**; zadejte **Poloha**, **Způsob vleku**, **Koordinující (vedoucí) remorkér**, **Zdroj vlečného lana**, **Bod připojení**, **ID vybavení vlečného lana** a **ID vybavení nouzového uvolnění**.
3. Napište text **Výměna informací kapitán lodi / lodivod / kapitán remorkéru**.
4. V **Provozní limity (vstupy plánu)** zadejte pro každý **Parametr** **Hodnota** a **Zdroj limitu**. Nový limit začíná prázdný.
5. Vyplňte **Komunikační plán** (primární a záložní kanál, pracovní jazyk, postup při ztrátě komunikace), **Nouzové uvolnění**, **Pohotovostní opatření** a **Opatření pro zónu zpětného rázu lana (snap-back)**.
6. Uložte pomocí **Vytvořit koncept** nebo **Uložit koncept**; jakmile nic nechybí, stiskněte **Odeslat ke schválení**.
7. Schvalovatel zvolí **Jmenování**, napíše **Důvod** o délce alespoň 10 znaků a stiskne **Schválit** nebo **Vrátit**.
8. Pokud se musí schválený plán změnit, napište důvod a použijte **Upravit**. Seznamy **Záznamy o schválení** a **Revize** ukazují historii.

## Co znamenají stavy
- **Koncept:** plán lze upravovat.
- **V kontrole:** plán byl odeslán ke schválení; čeká se na rozhodnutí.
- **Vráceno:** schvalovatel vrátil plán s uvedením důvodu; opravte jej a odešlete znovu.
- **Schváleno:** Kapitán nebo Kapitán remorkéru schválil tuto revizi plánu. Není to rozhodnutí GO; kontrola připravenosti a briefing jsou stále vyžadovány.
- **Neznámé:** pro pole nebyla zvolena žádná hodnota. „Neznámé“ u zón zpětného rázu lana nikdy nezastupuje „Ne“.
- **Požadavek na remorkér se změnil po uložení tohoto plánu.:** plán vychází ze starší revize požadavku a musí být posouzen.

## Co nemůžete dělat / upozornění
- Osoba, která plán vytvořila nebo odeslala, jej nemůže schválit.
- Plán s chybějícími povinnými sekcemi nelze odeslat; neúplný koncept lze uložit.
- Systém nenavrhuje žádnou hodnotu limitu, kanálu ani prahu; každá hodnota pochází od plánovače spolu s jejím zdrojem.
- Schválený plán není povolením k zahájení; zahájení vyžaduje aktuální potvrzení briefingu a záznam GO Kapitána remorkéru (13.03 §3).
- Offline jsou příkazy vypnuty.

## Když se něco pokazí
- **Zpráva „Required sections are missing.“:** doplňte kódy v **Chybí před odesláním**.
- **Zpráva „The record changed“:** pomocí **Znovu načíst** zkontrolujte aktuální plán.
- **Zpráva „The outcome is unknown“:** **Zkusit znovu** odešle stejný požadavek znovu.
- **Bez oprávnění:** zkontrolujte svou roli, remorkér a kontext jmenování; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).

## Související stránky
