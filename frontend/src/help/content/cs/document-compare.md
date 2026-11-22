---
key: document-compare
title: Porovnání revizí dokumentu
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka zobrazuje pro vybrané plavidlo **dvě revize řízeného dokumentu vedle sebe, pouze pro čtení**. Odpovídá na otázku: „Čím se tyto dvě revize liší na úrovni souboru a metadat a jaká je historie publikace, ověření a úschovy každé z nich?“ Stránka zobrazuje rozdíly; změny neschvaluje, nepublikuje ani nehodnotí. Revizi neuvádí v platnost a nezpůsobuje, že by nahradila dřívější revizi. Originály důkazů se nikdy nemění potichu; když nová revize nahradí dřívější, stav publikace dřívější revize se změní na Nahrazeno a událost se objeví v její historii úschovy (13.03 §1).

## Kdo ji používá
- **Kapitán / Vrchní strojník / Posádka:** porovnávají platnou revizi s předchozí a zaznamenávají potvrzení o přečtení (13.03 §2, §5, §6).
- **DPA / Regulatory and assurance user:** posuzují historii revizí a záznamy ověření (13.03 §4, §8).

## Než začnete
- Pokud není vybráno žádné plavidlo, stránka uvádí **Vyberte plavidlo ve svém rozsahu.** a zobrazí seznam plavidel. Vyberte správné plavidlo.
- K zobrazení stránky potřebujete přístup ke čtení souborů. Omezený obsah a dokumenty omezené ISPS vyžadují navíc vlastní oprávnění.
- Načítání obsahu a zaznamenání potvrzení fungují pouze online.

## Krok za krokem
1. Otevřete dokument ze seznamu **Řízené dokumenty**. V řádku se zobrazují označení klasifikace a **Omezeno ISPS**.
2. V části **Vyberte dvě revize** zvolte revize **Vlevo (základ)** a **Vpravo (posuzovaná)**. Stránka nejprve navrhne nejnovější revizi vpravo a tu před ní vlevo.
3. Stiskněte **Porovnat**. Pomocí **Prohodit strany** je vyměníte.
4. Přečtěte si vyjádření o bajtech a tabulku **Srovnání metadat**: **Typ souboru**, **Velikost (bajty)**, **SHA-256 obsahu**, **Příjem**, **Publikace**, **Ověření** a časová pole.
5. Na každé straně zkontrolujte **Historie schválení a úschovy**.
6. Chcete-li vidět obsah, stiskněte **Načíst obsah (přístup se zaznamená)**. Obsah se zobrazí s vodoznakem přiřazení.
7. Pokud jste po **Load content** přečetli platnou revizi, zaškrtněte **Tuto revizi jsem přečetl(a) a měl(a) přístup k jejímu obsahu.** a stiskněte **Zaznamenat potvrzení**.

## Co znamenají stavy
- **Nepublikováno / Platné / Nahrazeno:** stav publikace revize. Revize, která není platná, ukazuje **Není v provozní platnosti**.
- **Neověřeno / Ověřeno platné / Ověřeno neplatné / Ověření neprůkazné:** záznam ověření revize. Neověřeno neznamená „platné“.
- **Shodné / Změněno / Nesrovnatelné:** výsledek řádku metadat. Nesrovnatelné neznamená „Shodné“.
- **Bajtově shodné soubory:** pouze shodné bajty; nejde o posouzení obsahu.
- **Soubory se liší na úrovni bajtů:** které pravidlo se změnilo a jaký to má význam, musí posoudit osoba.
- **Obsah je pro vaši roli omezen; jen metadata.** obsah se pro tuto roli neotevírá.

## Co nemůžete dělat / upozornění
- Na této stránce nemůžete dokument publikovat, schválit, uvést v platnost ani nahradit.
- Stránka rozdíly v obsahu automaticky nevykládá; o jejich významu rozhoduje oprávněná osoba.
- Potvrzení se zaznamenává pouze pro platnou revizi, jednou na osobu a revizi. Není schválením ani ověřením.
- Vytištěná kopie je neřízená. Obsah se neukládá do mezipaměti a neuchovává se offline.
- V režimu offline je porovnání zastaralé a nový obsah nelze načíst.

## Když se něco pokazí
- **Tento dokument pro vás není dostupný:** zkontrolujte kontext plavidla a oprávnění; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Pro tuto akci nemáte oprávnění:** zkontrolujte svou roli a kontext plavidla (13.03 §10).
- **Záznam se mezitím změnil:** pomocí **Znovu načíst** otevřete nejnovější verzi.
- **Revizi nelze ověřit:** zkontrolujte důkazy a historii původu; obraťte se na oprávněného ověřovatele (13.03 §10).
- **Požadavek selhal:** pošlete podpoře obrazovku, čas a postup (13.03 §10).

## Související stránky
