---
key: offline-conflict-center
title: Centrum podstatných konfliktů
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Centrum podstatných konfliktů uvádí pro vybrané plavidlo nebo remorkér **podstatné konflikty mezi vašimi offline změnami a aktuálním záznamem na serveru** a umožňuje osobě je vyřešit odůvodněným rozhodnutím. Odpovídá na otázku: „Které z mých offline změn se střetly s novějším záznamem na serveru a jak, kým a s jakým odůvodněním budou vyřešeny?“ Nic se zde nepřepisuje ani neřeší automaticky.

## Kdo ji používá
- **Posádka / Kapitán remorkéru / Kapitán / Vrchní strojník:** po práci offline vidí a řeší své vlastní konfliktní záznamy. Když se objeví konflikt, nepředpokládejte, že váš vlastní záznam automaticky vyhraje (13.03 §6).
- Podstatný konflikt vyžaduje lidské vyřešení; tato stránka je cílem postupu „Podstatný konflikt → Centrum konfliktů“ v 13.03 §10.

## Než začnete
- Stránka uvádí pouze konflikty, které jste zaznamenali vy, a vyžaduje přístup ke čtení alespoň jednoho čitelného zdroje (například přidělení posádky, operace remorkérů, denní záznam, doklady k pracovnímu příkazu (se čtením vybavení a údržby), koncept obnovy osvědčení). Stránku otevře kterýkoli jeden čitelný zdroj; uvedená oprávnění jsou alternativy.
- Seznam a rozhodnutí vyžadují aktuální připojení k serveru. V režimu offline zůstávají vaše místní změny uchovány v zařízení.
- Vyberte plavidlo nebo remorkér ze seznamu **Plavidlo**.

## Krok za krokem
1. Vyberte plavidlo v poli **Plavidlo**. Otevře se tabulka se sloupci **Zdroj**, **Záznam**, **Verze**, **Zachyceno**, **Přijato serverem** a **Stav**.
2. Pokud se zobrazí poznámka „Nezahrnuto:“, zdroje, pro které nemáte oprávnění ke čtení, nejsou v seznamu.
3. U řádku ve stavu **Otevřeno — vyžaduje rozhodnutí** stiskněte **Posoudit**.
4. V části **Posouzení konfliktu** porovnejte u každého pole hodnoty **Před zachycením**, **Zachyceno** a **Server**. Žádná volba není předem vybrána.
5. U každé hodnoty výslovně zvolte **Ponechat hodnotu serveru** nebo **Použít zachycenou hodnotu**, vyplňte **Důvod řešení (povinný)** a stiskněte **Podat řešení**.
6. Chcete-li odstranit pouze místní kopii v tomto zařízení, zaškrtněte potvrzení v části **Zahodit mou místní změnu** a stiskněte **Zahodit místní změnu**.
7. Pomocí **Zavřít posouzení** se vrátíte na seznam.

## Co znamenají stavy
- **Otevřeno — vyžaduje rozhodnutí:** konflikt čeká na lidské rozhodnutí.
- **Nahrazeno novějším konfliktem:** pro stejný záznam existuje novější konflikt; posuďte ten nový.
- **Vyřešeno výslovným rozhodnutím:** někdo ho vyřešil odůvodněným rozhodnutím. Nejde o provozní schválení.
- **Zadrženo / Neuvedeno / Neznámé:** hodnota je skryta z důvodu oprávnění, nebyla zaznamenána nebo ji nebylo možné přečíst. Neznámá hodnota se nikdy nepovažuje za prázdnou ani za „Ne“.
- **Potvrzení serveru o přijetí vašeho výslovného rozhodnutí zaznamenáno:** server vaše rozhodnutí přijal. Toto potvrzení o přijetí není provozním schválením.

## Co nemůžete dělat / upozornění
- Konflikty se nikdy neslučují ani neřeší automaticky; každá hodnota vyžaduje výslovnou lidskou volbu a odůvodnění (13.03 §1).
- Centrum přímo řeší konflikty přidělení posádky. U jiných zdrojů zobrazuje „Vyřešte tento konflikt z offline panelu vlastního záznamu.“; například záznamy remorkérů se řeší v části **Offline operace remorkérů**.
- Zahození místní změny odstraní pouze kopii v zařízení; záznam na serveru zůstává beze změny a konflikt zůstává zaznamenán až do odůvodněného rozhodnutí.
- Změnu zachycenou v jiném zařízení je třeba vyřešit v tom zařízení; nic se nezahazuje.
- Podání znovu zkontroluje váš přístup a verze záznamu; neuděluje provozní schválení.

## Když se něco pokazí
- **Zpráva „Výsledek podání je neznámý“:** před dalším podáním použijte **Zkontrolovat výsledek podání**.
- **Zpráva „Záznam na serveru se po vašem posouzení změnil“ / „Záznam na serveru se znovu změnil“:** vaše rozhodnutí nebylo použito; posuďte konflikt znovu.
- **„Podstatné konflikty nebylo možné načíst. Nic nebylo vyřešeno.“:** stiskněte **Zkusit znovu**.
- **„Nemáte přístup k centru konfliktů.“ / „Přístup ke konfliktům není pro toto plavidlo k dispozici.“:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).

## Související stránky
