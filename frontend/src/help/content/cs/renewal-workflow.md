---
key: renewal-workflow
title: Obnova osvědčení
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Obnova osvědčení zaznamenává krok za krokem přípravu obnovy osvědčení a interní postup: přípravu, žádost, čekání na úřad, vydanou náhradu, ověření a dokončení. Odpovídá na otázku: „V jaké fázi je obnova tohoto osvědčení a kdo provede další krok?“ Jde pouze o záznam interního postupu; nezakládá právní platnost ani schválení úřadem (13.03 §1, §8).

## Kdo ji používá
- **Kapitán, Vrchní strojník / Technický inspektor:** připravují koncept obnovy a zaznamenávají požadavky a žádost.
- **Přidělený vlastník obnovy:** posouvá postup dál. Jde o roli, kterou mají členové s oprávněním vlastníka obnovy; bez ní nelze postup otevřít ani validovat.
- **Ověřovatel a dokončovatel:** porovnává vydanou náhradu s jejím zdrojem a ověřuje ji a jako jiná osoba obnovu dokončuje (13.03 §1, §8).

## Než začnete
- Zobrazení vyžaduje oprávnění ke čtení osvědčení. Příprava konceptu vyžaduje oprávnění k zápisu osvědčení a k zápisu obnovy pro dané plavidlo; ověření vyžaduje oprávnění k ověření obnovy; dokončení vyžaduje oprávnění k dokončení obnovy.
- Nejprve vyberte správné plavidlo v **Vybrat plavidlo** (13.03 §1).
- Obnovované osvědčení musí mít aktuální uloženou revizi. Nahrazené osvědčení nelze obnovit.
- Kroky postupu vyžadují živé připojení. V režimu offline uvidíte „Jste offline. Kroky nejsou dostupné, dokud se připojení neobnoví.“

## Krok za krokem
1. Otevřete **Záznam obnovy** ze seznamu **Zaznamenané koncepty obnovy**. Pro novou obnovu použijte **Vybrat osvědčení k obnově**, čímž přejdete do [Centra osvědčení](/app/certificate-command-center), a u záznamu stiskněte **Připravit obnovu**.
2. Vyplňte **Formulář obnovy osvědčení**: **Deklarovaný termín obnovy**, interval prohlídky, **Plánované místo**, **Přidělená role vlastníka obnovy** a **Prohlášení o nutné prohlídce**, **Prohlášení o požadovaných dokumentech**, **Prohlášení o požadovaných akcích**. Hodnotu, kterou neznáte, ponechte jako **Neznámé**.
3. Koncept uložte pomocí **Uložit neověřený koncept**.
4. V části **Proces obnovy** zvolte z **Dostupné kroky** krok otevřený pro vaši roli, například **Potvrdit splnění požadavků**, **Zaznamenat žádost** nebo **Čekat na rozhodnutí úřadu**. Vyplňte povinná pole a stiskněte **Zaznamenat krok**.
5. Když úřad vydá nové osvědčení, použijte **Zaznamenat vydanou náhradu** a vyberte ho. Jeho vlastní dokument se použije jako doklad.
6. Ověřovatel použije **Ověřit náhradu** a porovnáním se zdrojem nastaví **Vydavatel se shoduje**, **Číslo certifikátu se shoduje**, **Předmět se shoduje**, **Data se shodují** a **Rozsah se shoduje**.
7. Jiná osoba obnovu dokončí pomocí **Dokončit a nahradit**. Pokud úřad žádost zamítl, použijte **Zaznamenat zamítnutí úřadem**; pokud je příprava opuštěna, použijte **Zrušit přípravu**.

## Co znamenají stavy
- **Koncept / V přípravě:** obnova se připravuje; formulář lze upravovat.
- **Připraveno k žádosti / Žádost zaznamenána / Čeká na úřad:** fáze žádosti. Rozhodnutí úřadu probíhá mimo systém.
- **Vydáno, neověřeno:** náhrada je zaznamenána, ale osoba ji zatím neporovnala s jejím zdrojem.
- **Ověřeno kontrolorem:** ověřovatel zaznamenal porovnání; obnova zatím není dokončena.
- **Dokončeno:** interní postup je uzavřen a náhrada nahrazuje originál. Nejde o rozhodnutí o právní platnosti osvědčení.
- **Zrušeno / Zamítnuto:** postup se zastavil.
- Požadavek může být **Splněno / Nevyžaduje se / Neznámé**. **Neznámé** neznamená „Nevyžaduje se“; server při neznámém požadavku postup neposune.

## Co nemůžete dělat / upozornění
- Osoba, která zaznamenala vydanou náhradu nebo která toto náhradní osvědčení zadala, ji nemůže ověřit. Dokončovatel se musí lišit od ověřovatele, od osoby, která zaznamenala vydanou náhradu, a od autora náhradního osvědčení.
- Ověření a dokončení jsou výhradně lidská rozhodnutí; Dokončeno ≠ Ověřeno.
- Plánovací deklarace nezakládají provedené prohlídky, schválené žádosti, způsobilost k plavbě ani ověření.
- Propojení náhrady v konceptu nenahrazuje originál. Staré osvědčení se uzavře pouze krokem dokončení se sledovatelným propojením nahrazení (13.03 §1).
- Každý krok se zaznamenává jako neměnná revize; historie zůstává v **Zaznamenaná historie revizí**.
- Bez výslovně přiděleného vlastníka systém nikdy automaticky nepoužije administrátora.

## Když se něco pokazí
- **Pro vaši roli není v tomto stavu dostupný žádný krok.** Další krok patří jiné roli; zkontrolujte své oprávnění a jmenování a v případě potřeby se obraťte na Tenant Admin (13.03 §10).
- **Obnova se po načtení změnila.** nebo **Toto není aktuální revize.** Stiskněte **Znovu načíst** a zkontrolujte aktuální stav.
- **Výsledek není znám. Opakování odešle stejný požadavek znovu.** Použijte **Opakovat stejný požadavek**.
- **Odmítnuto kvůli oddělení povinností: tento krok musí provést jiná osoba. Zaznamenali, sepsali nebo ověřili jste dřívější krok této obnovy, nebo dosud není zaznamenáno ověření jinou osobou.** Záznam se nezměnil; opětovné načtení nepomůže. Krok přenechte jiné oprávněné osobě.
- **Krok nebyl zaznamenán. Zkontrolujte údaje a zkuste to znovu.** Zkontrolujte povinná pole a výběr shod.
- Pokud náhradu nelze ověřit, zkontrolujte dokument a původ zdroje a předejte ji ověřovateli (13.03 §10).

## Související stránky
