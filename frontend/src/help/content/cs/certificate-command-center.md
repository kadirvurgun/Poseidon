---
key: certificate-command-center
title: Centrum osvědčení
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: [1, 2]
reviewed: false
---
## K čemu tato stránka slouží
Centrum osvědčení zobrazuje **prohlášení** o osvědčeních vybraného plavidla s jejich deklarovanými kalendářními stavy. Odpovídá na otázku: „Kterým osvědčením na tomto plavidle se blíží konec platnosti nebo interval prohlídky a které záznamy stále čekají na lidské posouzení?“ Zobrazená data jsou deklarace. Stránka nikdy nerozhoduje o tom, že je osvědčení platné nebo ověřené (13.03 §1, §8).

## Kdo ji používá
- **Kapitán, Vrchní strojník / Technický inspektor:** sledují kalendář osvědčení plavidla, zadávají nové koncepty osvědčení a zahajují přípravu obnovy.
- **Regulatory / Assurance user:** kontroluje, že deklarace stojí na oficiálním, verzovaném zdroji, a zaznamenává tvrzení přiděleného Auditora (13.03 §8).
- Uživatelé pouze s oprávněním ke čtení vidí jen seznam a nemohou ho měnit.

## Než začnete
- Zobrazení vyžaduje oprávnění ke čtení osvědčení. Zadávání konceptů vyžaduje oprávnění k zápisu osvědčení a rozsah úprav pro dané plavidlo; získávání návrhů z dokumentu vyžaduje oprávnění ke čtení souborů a k extrakci; zaznamenání lidského tvrzení vyžaduje oprávnění k zápisu do adresáře, oprávnění k záznamu ověření a platné jmenování **Auditor** pro toto plavidlo a osvědčení. Chráněná prohlášení posádky navíc vyžadují oprávnění ke čtení posádky, ke čtení osvědčení posádky, ke čtení identity posádky a ke čtení souborů.
- Nejprve vyberte správné plavidlo: stiskněte ho v seznamu **Vybrat plavidlo** (13.03 §1).
- Stránka vyžaduje živé čtení online. V režimu offline uvidíte „Current certificate sources unavailable offline or hidden.“ a žádné oprávnění offline se nevyvozuje.

## Krok za krokem
1. Otevřete plavidlo ze seznamu **Vybrat plavidlo**.
2. Pomocí **Rozsah osvědčení** zvolte **Osvědčení plavidla** nebo, pokud máte oprávnění, **Chráněná prohlášení posádky**. Seznam zužte pomocí **Filtr deklarovaného kalendáře**.
3. Záznam otevřete stisknutím jeho řádku. Řádky nesou označení **Neověřené prohlášení o osvědčení**.
4. Pro nový záznam stiskněte **Nový koncept osvědčení**. Vyplňte ze zdrojového dokumentu **Název dokumentu**, **Deklarovaný regulatorní základ**, **Deklarovaná vlajka**, interval prohlídky a **Deklarovaná dostupnost** a pak stiskněte **Uložit neověřený koncept**.
5. Chcete-li získat návrhy z propojeného dokumentu, stiskněte **Získat z propojeného dokumentu** v části **Posoudit fakta dokumentu**. Porovnejte každý návrh se zdrojem, zaškrtněte **Ověřil(a) jsem tento fakt oproti zdroji** a **Ověřil(a) jsem, že tento dokument patří k tomuto plavidlu a osvědčení** a pak stiskněte **Potvrdit vybraná fakta a uložit koncept**.
6. Jako přidělený Auditor vyplňte v části **Záznam lidského tvrzení** pole **Vybrat skutečné přidělení** a **Vybrat zaznamenané tvrzení**, zadejte důvod a pak stiskněte **Zaznamenat neposouzené tvrzení**.
7. Pro zahájení obnovy použijte u záznamu **Připravit obnovu** v části **Práce související s osvědčením**. Tím přejdete do postupu [Obnova osvědčení](/app/renewal-workflow).

## Co znamenají stavy
- **Deklarované vypršení <90 dnů / <60 dnů / <30 dnů / Deklarované datum vypršení uplynulo:** kalendářní pozorování podle deklarovaného data konce platnosti. Nejde o rozhodnutí o platnosti.
- **Deklarovaný interval prohlídky je otevřený / Za termínovým intervalem:** poloha vůči deklarovanému intervalu prohlídky.
- **Vyžaduje posouzení kalendáře:** datum chybí, je nejednoznačné nebo rozporné. Záznam není připraven, dokud ho osoba nezkontroluje oproti zdrojovému dokumentu.
- **Neznámé:** hodnota nebyla deklarována. Neznámá hodnota nikdy neznamená „nechybí“ ani „vyhovuje“; vyžaduje posouzení.
- **Nízká míra jistoty · posoudit / Nejednoznačné · zkontrolovat / Vyžaduje posouzení:** návrh z dokumentu, který zatím osoba nepotvrdila. Skóre jistoty není ověření.
- „Pouze tato stránka. Kalendářní pozorování jsou oddělena od kvalifikace; nejde o souhrny připravenosti za celou flotilu.“

## Co nemůžete dělat / upozornění
- Uložení konceptu osvědčení neověřuje. Zápis záznamu, potvrzení návrhů z dokumentu a tvrzení Auditora jsou samostatné kroky; Dokončeno ≠ Ověřeno.
- Automatická extrakce pouze navrhuje. Chybějící hodnoty zůstávají neznámé a koncept aktualizují pouze fakta, která vyberete a opravíte.
- Samotná role administrátora neumožňuje zaznamenat lidské tvrzení; je nutné platné jmenování a zdrojový soubor.
- Uložené osvědčení se nikdy nemění potichu. Každé uložení vytvoří novou revizi a starší revize zůstávají v **Zaznamenaná historie revizí** (13.03 §1).
- Stránka neuděluje žádné schválení klasifikační společnosti, vlajky ani úřadu a nerozhoduje o způsobilosti k plavbě. Toto rozhodnutí náleží oprávněným osobám.

## Když se něco pokazí
- **Zdroj osvědčení nebo přístup není dostupný. Nevyvozuje se platný, prázdný ani ověřený seznam.** Stiskněte **Zopakovat čtení zdroje**. Pokud problém přetrvává, zkontrolujte svou roli, plavidlo a rozsah objektů; v případě potřeby se obraťte na Tenant Admin (13.03 §10).
- **Výsledek neznámý.** Ponechte původní příkaz a odešlete stejný požadavek znovu pomocí **Zopakovat přesný příkaz osvědčení**.
- **Aktuální zdroj se změnil.** Vaše místní pole zůstanou zachována; použijte **Zpět na záznamy**, přečtěte aktuální záznam znovu a svou změnu proveďte znovu.
- **Automatické čtení není pro tento dokument dostupné:** zadejte hodnoty ručně ze zdrojového dokumentu.
- Pokud záznam nelze ověřit, zkontrolujte jeho důkazy a původ zdroje a předejte ho ověřovateli (13.03 §10).

## Související stránky
