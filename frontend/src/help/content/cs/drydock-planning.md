---
key: drydock-planning
title: Plánování suchého doku
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Plánování suchého doku uchovává **balíčky prací v suchém doku** vybraného plavidla (projekt, loděnice, plánovaná data a položky rozsahu prací). Odpovídá na otázku: „Ke kterému pracovnímu příkazu, závadě, osvědčení nebo kontrolnímu seznamu prohlídky je každá položka rozsahu prací v suchém doku propojena a jsou tato propojení aktuální?“ Rozsah prací je propojen s těmito záznamy (13.03 §5). **Připravenost propojení** vyjadřuje pouze úplnost propojení; není výsledkem prohlídky klasifikační společnosti ani vlajky a není prohlášením o způsobilosti k plavbě.

## Kdo ji používá
- **Technický inspektor:** připravuje balíček prací v suchém doku, propojuje rozsah prací se záznamy, plánuje ho a dokončuje (13.03 §5).
- **Vrchní strojník:** sleduje, jak je rozsah prací propojen se záznamy závad a pracovních příkazů, a s oprávněním ověřuje balíček, který dokončil někdo jiný (13.03 §5).

## Než začnete
- Vyberte správné plavidlo. Pokud není vybráno žádné plavidlo, stránka uvádí **Pro pokračování vyberte plavidlo v pracovním prostoru.**
- K zobrazení balíčků potřebujete přístup ke čtení suchého doku. Vytváření, úpravy a změna stavu vyžadují přístup k zápisu; ověření vyžaduje přístup k ověřování.
- Mějte připravené identifikátory a revize záznamů pracovních příkazů, závad, osvědčení a kontrolních seznamů prohlídek, které budete propojovat.

## Krok za krokem
1. Otevřete balíček ze seznamu, nebo vytvořte prázdný koncept pomocí **Vytvořit balíček prací v suchém doku**.
2. Vyplňte **Projekt suchého doku**, **Loděnice**, **Plánovaný začátek** a **Plánované dokončení**.
3. Položky přidejte pomocí **Přidat položku rozsahu prací**; zadejte typ **Položka rozsahu prací**, **Název**, **Pracovní balíček**, **Požadavek klasifikační společnosti**, **Zákonný požadavek** a **Priorita**.
4. U každé položky použijte **Přidat odkaz** a zadejte **Typ záznamu**, **Identifikátor záznamu** a **Připnutá revize**.
5. Pro dokončení přidejte v části **Doklad o dokončení** referenci na důkaz pomocí **Přidat**. Pak stiskněte **Uložit**.
6. Zkontrolujte seznam **Položky blokující plánování** nebo **Položky blokující dokončení**. V části **Změnit stav** zvolte **Akce** (**Naplánovat**, **Zpět do konceptu**, **Zahájit suchý dok**, **Dokončit suchý dok**, **Zrušit balíček**) a potvrďte ji. Zrušení vyžaduje poznámku. Nabízejí se pouze akce platné pro aktuální stav; balíček lze zrušit pouze ve stavu Koncept nebo Naplánováno.
7. U dokončeného balíčku, který jste nedokončili vy, napište **Důvod** o délce nejméně 10 znaků a použijte **Ověřit záznam** nebo **Zamítnout ověření**.
8. Plánovaná a skutečná data a každou revizi sledujte na **Časová osa**.

## Co znamenají stavy
- **Koncept / Naplánováno / Probíhá / Dokončeno / Ověřeno / Zrušeno:** životní cyklus balíčku. Dokončeno a Ověřeno není totéž.
- **Propojení aktuální / Pozor / Vyžaduje kontrolu / Neúplné:** připravenost propojení. Ani s aktuálními propojeními nejde o výsledek prohlídky.
- **Propojeno / Nepropojeno / Po termínu / Vyžaduje kontrolu:** stav propojení položky rozsahu prací (Vyžaduje kontrolu, pokud je kterékoli propojení Zastaralé nebo Nedostupné).
- **Aktuální / Zastaralé / Nedostupné:** propojený záznam porovnaný s jeho připnutou revizí. Zastaralé propojení posuďte a pomocí **Znovu připnout na** ho připněte k nové revizi.
- **Neznámé / nenastaveno:** nebyla zadána žádná priorita. Neznámé neznamená „Normální“ ani „Nízká“.

## Co nemůžete dělat / upozornění
- Stránka nerozhoduje o tom, že je suchý dok bezpečný ani že je plavidlo způsobilé k plavbě.
- Balíček, který jste dokončili, nemůžete ověřit. Zamítnutí ověření vrátí balíček do stavu **Probíhá**.
- Náklady, měna, dodavatel a rozpočet jsou pouze externí reference; nic se zde nesčítá, neschvaluje ani neobjednává.
- Dokončený, ověřený nebo zrušený balíček nelze upravit.
- V režimu offline se příkazy neodesílají.

## Když se něco pokazí
- **Server uvedl chybějící nebo blokující položky:** vyřešte seznam překážek a zkuste to znovu.
- **Záznam se změnil nebo je příkaz v konfliktu:** použijte **Načíst nejnovější** a zkontrolujte.
- **Výsledek je neznámý:** **Zkusit znovu** odešle znovu identický příkaz.
- **K tomuto záznamu nemáte přístup:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Tyto typy propojených záznamů nemůžete číst:** ověřte oprávnění u vlastníka záznamu nebo správce svého tenanta.

## Související stránky
