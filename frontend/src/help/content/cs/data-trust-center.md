---
key: data-trust-center
title: Centrum důvěryhodnosti dat
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Centrum důvěryhodnosti dat zobrazuje **signály kvality dat** pro kontroly, důkazy a pole profilu vybraného plavidla. Odpovídá na otázku: „Které záznamy na tomto plavidle chybí, jsou zastaralé, rozporné, neověřené nebo osiřelé a jaké informace o zdroji a aktuálnosti to uvádějí?“ Signál důvěryhodnosti není ověřením podkladového záznamu. Stránka nikdy nic neoznačí jako připravené.

## Kdo ji používá
- **Regulatory / Assurance user:** aby v řetězci Požadavek → Kontrola → Důkaz → Ověření nezůstaly osiřelé ani neúplné záznamy (13.03 §8).
- **DPA:** sleduje mezery v důkazech a ověřeních (13.03 §4).
- **Technický inspektor / Kapitán:** vidí signály, které drží posouzení připravenosti ve stavu vyžadujícím kontrolu (13.03 §2, §5).

## Než začnete
- V horním kontextovém pruhu zkontrolujte, že je vybrána **správná společnost** (13.03 §1).
- Stránka vyžaduje přístup ke čtení kontrol, důkazů, souborů, použitelnosti a regulatorních konceptů tenanta, a to všech současně, s rozsahem čtení pro celý tenant.
- Pokud není vybráno žádné plavidlo, stránka nejprve zobrazí seznam „Vyberte plavidlo“.

## Krok za krokem
1. Vyberte plavidlo ze seznamu „Vyberte plavidlo“.
2. V části **Shrnutí** si přečtěte celkový stav („Vyžaduje kontrolu“ nebo „Žádný podstatný signál“) a počty **Předměty podle signálu**.
3. V tabulce **Kontroly a důkazy** zkontrolujte u každého předmětu sloupce „Zdroj / původ“, „Aktuálnost“, „Ověření“ a „Signály“.
4. Poznamenejte si kód důvodu vedle odznaku signálu a záznam zkontrolujte na jeho vlastní stránce.
5. Signály polí profilu najdete v tabulce **Pole profilu plavidla**.
6. Pro další předměty použijte **Předchozí** / **Další**.
7. Pokud se zobrazí „Tyto signály mohou být zastaralé.“, stiskněte **Znovu načíst**.

## Co znamenají stavy
- **Chybí / Zastaralé / Rozporné / Neověřeno / Osiřelé:** druh signálu u předmětu. Signál je ukazatel kvality; nedokazuje, že je záznam správný nebo nesprávný.
- **Žádný signál:** u tohoto předmětu nebyl nalezen žádný signál. To neznamená, že je záznam ověřen.
- **Vyžaduje kontrolu:** existuje podstatný signál. Dokud podstatné signály existují, připravenost je držena ve stavu vyžadujícím kontrolu.
- **Žádný podstatný signál:** nejde o prohlášení o připravenosti.
- **Ověřeno člověkem:** oprávněná osoba ověřila důkaz. Tato informace pochází z vlastního záznamu důkazu; centrum nic neověřuje.
- **Neověřeno / Ověření vypršelo / Ověření zamítnuto:** stav ověření důkazu.
- **Pro kontroly neplatí:** zobrazuje se v řádcích kontrol; stav ověření se týká řádků důkazů.
- **Čas pozorování neznámý / Bez zásady aktuálnosti / Datováno do budoucna:** aktuálnost nebylo možné stanovit nebo je datum v budoucnosti. Nic z toho neznamená „aktuální“.

## Co nemůžete dělat / upozornění
- Záznamy se na této stránce neopravují, neověřují ani neuzavírají; to provádí oprávněná osoba na vlastní stránce záznamu.
- Pokud byly posouzeny jen první předměty, shrnutí zůstává ve stavu vyžadujícím kontrolu.
- Předměty, které nemůžete číst, se započítávají, ale nezobrazují, a shrnutí pak zůstává ve stavu Vyžaduje kontrolu. Záznam, který nevidíte, neznamená, že žádný problém neexistuje.
- V režimu offline nelze signály důvěryhodnosti dat obnovit.

## Když se něco pokazí
- **Nemáte oprávnění číst signály důvěryhodnosti dat:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Záznam nelze ověřit:** zkontrolujte důkaz a jeho původ; předejte věc oprávněnému ověřovateli.
- **Neočekávaný signál:** předejte kód důvodu vlastníkovi kontroly nebo odbornému expertovi.
- **Signály důvěryhodnosti dat nejsou dostupné:** použijte „Zkusit znovu“; pokud problém přetrvává, nahlaste ho podpoře se snímkem obrazovky, časem a postupem.

## Související stránky
