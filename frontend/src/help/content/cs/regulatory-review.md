---
key: regulatory-review
title: Regulatorní přezkum SME
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Regulatorní přezkum SME zaznamenává kampaně, ve kterých lidský odborník posuzuje zdrojové položky a položky použitelnosti lodi nebo remorkéru. Odpovídá na otázku: „Které zdrojové položky a položky požadavků pro toto plavidlo potvrdil oprávněný kontrolor a které jsou stále otevřené a sledované?“ Jde pouze o záznam lidského posouzení: systém neschvaluje žádný požadavek, neaktivuje žádné pravidlo ani povolení a neuvádí žádné schválení klasifikační společností ani typové schválení (13.03 §8).

## Kdo ji používá
- **Regulatory / Assurance user:** otevírá kontrolní kampaně, zaznamenává výroky k položkám a uzavírá kampaň (13.03 §8).
- **Jmenovaný kontrolor (SME):** potvrzuje nebo sleduje položky v rámci své vlastní autority zdroje.
- **DPA / HSEQ:** sleduje otevřené a nesledované položky (13.03 §4).

## Než začnete
- Zobrazení vyžaduje oprávnění ke čtení regulatorního přezkumu. Otevírání a uzavírání kampaní vyžaduje oprávnění ke správě; zaznamenávání výroků vyžaduje oprávnění k výroku a platné jmenování kontrolorem pro tento rozsah.
- Na kartách **Rozsah kontroly** zvolte **Loď** nebo **Remorkér** a pak otevřete správné plavidlo v **Vyberte plavidlo** (13.03 §1).
- Zdroje by měly být zaregistrovány jako oficiální a verzované na stránce [Koncepty zdrojů](/app/sources) (13.03 §8).
- V režimu offline mohou být data zastaralá a příkazy jsou vypnuty.

## Krok za krokem
1. Zvolte rozsah a plavidlo. Pokud neexistuje žádná kampaň, stiskněte **Otevřít kontrolní kampaň**.
2. V části **Matice vlastnictví zdrojů** zkontrolujte u každé autority zdroje kategorii, počty otevřených a potvrzených položek a odpovědné role.
3. Vyberte položku v části **Položky kontroly**.
4. Ve formuláři **Zaznamenat výrok kontroly** vyplňte **Jmenování kontrolora**, **Výrok**, **Autorita zdroje** a **Odpovědná role**. Pokud položka nemá zaregistrovaný zdroj, zadejte **Odkaz na autoritativní zdroj** a **Verze / vydání zdroje**. U položky, která zůstává otevřená, zadejte **ID sledovaného úkolu**.
5. Do pole **Poznámka kontrolora** napište důvod o délce 10–2000 znaků a stiskněte **Zaznamenat**.
6. Když jsou všechny položky vyřešeny, zvolte v části **Uzavřít kampaň** **Výsledek uzavření**, napište **Důvod uzavření** a stiskněte **Uzavřít kampaň**.

## Co znamenají stavy
- Kampaň: **Otevřeno**, **V kontrole**, **Uzavřena s otevřenými položkami**, **Uzavřena**.
- Výrok: **Potvrzeno**, **Nepoužitelné (potvrzeno)**, **Vyžaduje kontrolu**, **Nejednoznačné**. Když jsou kritická data neznámá, položka zůstává **Vyžaduje kontrolu**; neznámá položka se nikdy nepočítá jako „Nepoužitelné“.
- **Kandidát (neověřený):** zdroj zatím není svázán s ověřeným oficiálním zdrojem.
- **Zastaralé — vazba změněna** a **Profil plavidla se změnil:** zdroj nebo profil plavidla se po výroku změnil; položku je třeba posoudit znovu.
- **Nesledované otevřené položky:** otevřené položky bez sledovaného úkolu. Dokud je jejich počet vyšší než nula, kampaň nelze uzavřít.
- Kategorie **Zákon**, **Pravidlo klasifikační společnosti**, **SMS společnosti** a **Pokyn — není zákon** se nikdy vzájemně nenahrazují (13.03 §8).

## Co nemůžete dělat / upozornění
- Osoba, která kampaň otevřela, v ní nemůže zaznamenávat výroky; bez jmenování kontrolorem jsou výroky pouze pro čtení.
- Kontrolor, jehož kvalifikace není ověřena, může otevřené položky sledovat, ale nemůže je potvrdit. Zastaralou položku nelze potvrdit.
- Potvrzení vyžaduje zaregistrovaný zdroj nebo odkaz na autoritativní zdroj s jeho vydáním. V řetězci Požadavek → Kontrola → Důkaz → Ověření nenechávejte žádný osiřelý záznam ani záznam bez zdroje (13.03 §8).
- **Uzavřena** vyžaduje, aby byla potvrzena každá položka; jinak uzavřete s otevřenými položkami, z nichž každá je sledovaná.
- V poznámce neuvádějte schválení klasifikační společností ani typové schválení. V rozsahu remorkérů není žádný limit vlečení bez řízeného zdroje právním výchozím stavem.
- Výroky zaznamenávají pouze lidští uživatelé; účty AI a automatizace je zaznamenat nemohou.

## Když se něco pokazí
- **Záznam se změnil. Před pokračováním jej znovu načtěte a zkontrolujte.** Stiskněte **Znovu načíst**.
- **Výsledek je nejistý. Opakování odešle stejný příkaz.** Použijte **Zkusit znovu**.
- **Pravidla kontroly tento příkaz odmítla.** Zkontrolujte, zda je položka zastaralá, odkaz na zdroj a sledovaný úkol.
- **K regulační kontrole tohoto plavidla nemáte přístup.** nebo **K této akci nemáte oprávnění.** Zkontrolujte svou roli a rozsah; v případě potřeby se obraťte na Tenant Admin (13.03 §10).
- Pokud použitelnost vypadá neočekávaně, zkontrolujte zdroj a kontextové vstupy a obraťte se na vlastníka kontroly nebo odborníka (SME) (13.03 §10).

## Související stránky
