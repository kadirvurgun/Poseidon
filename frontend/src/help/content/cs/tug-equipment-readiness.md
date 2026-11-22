---
key: tug-equipment-readiness
title: Připravenost vlečného vybavení
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka zobrazuje **prohlášení o vlečném vybavení** zvoleného remorkéru (vlečný naviják, vlečný hák, vlečné lano, nouzové uvolnění, požární vybavení FiFi) a jejich **Ověření vrchního strojníka**. Odpovídá na otázku: „Jaké jsou SWL, data zkoušek, stav a kritičnost jednotlivých položek, kdo je ověřil a co říká kontrola připravenosti?“ Posouzení je pouze doporučením a slouží jako vstup do kontroly připravenosti; není to rozhodnutí GO.

## Kdo ji používá
- **Kapitán remorkéru:** kontroluje naviják, vlečné lano, vlečný hák / řetězovou zarážku, nouzové uvolnění, FiFi a doklady o kritických závadách (13.03 §3).
- **Vrchní strojník:** ověřuje nebo neověřuje prohlášení a zaznamenává vliv kritické závady na připravenost (13.03 §5).

## Než začnete
- Otevřete remorkér z **Provoz remorkérů**; tato obrazovka nevyžaduje žádné přidělení.
- Přidání nebo úprava prohlášení vyžaduje přístup k zápisu remorkéru a zápisu vybavení.
- Zaznamenání ověření vyžaduje jmenování **Vrchní strojník** pro daný remorkér zaznamenané na serveru.

## Krok za krokem
1. Přečtěte si celkový odznak na řádku **Hodnocení**.
2. Na každé kartě vybavení zkontrolujte odznak výsledku, kód důvodu, **SWL (t)**, **Příští zkouška**, **Stav** a **Kritické**.
3. Pro nové prohlášení stiskněte **Přidat vybavení**; vyplňte **Typ vybavení**, **Označení**, **SWL (t)**, **Poslední zkouška**, **Příští zkouška**, **Stav**, **Kritické**, **ID aktiva PMS**, **ID certifikátu** a **Poznámky** a poté stiskněte **Uložit prohlášení**.
4. Chcete-li změnit existující prohlášení, použijte na jeho kartě **Upravit**.
5. Jako Vrchní strojník zvolte své **Jmenování**, napište důvod a pomocí **Ověřeno** nebo **Neověřeno** proveďte **Zaznamenat ověření**. Pro „Neověřeno“ je důvod povinný.
6. Historii ověření s revizí a časem uvidíte v **Ověření vrchního strojníka** a tabulku **Kontroly zdrojů** v dolní části.

## Co znamenají stavy
- **Ready / Conditional / Not ready / Review required:** doporučující posouzení. Ready není rozhodnutí GO.
- **Pass / Fail / Unknown:** výsledek kontroly jedné položky.
- **Serviceable / Degraded / Failed / Unknown:** deklarovaný stav. Pokud není zvolen žádný stav, zůstává „Unknown“; nepočítá se jako provozuschopný.
- **Nehodnoceno:** kritičnost nebyla posouzena. Neznamená to „Ne“.
- **Ověřeno / Neověřeno:** lidský záznam Vrchního strojníka.
- **nahrazeno pozdější revizí:** ověření patřilo ke starší revizi prohlášení; aktuální revize vyžaduje nové ověření.

## Co nemůžete dělat / upozornění
- Uložení zaznamená neověřené prohlášení. Jakákoli změna vyžaduje nové ověření Vrchním strojníkem.
- Dokončení záznamu a jeho ověření nejsou totéž; ověření čeká na oprávněnou osobu (13.03 §1).
- Typ vybavení nelze po uložení změnit; pro jiný typ přidejte nové prohlášení.
- Stránka nikdy neprohlašuje vlečné vybavení za bezpečné; rozhodnutí zůstává u Kapitána remorkéru (13.03 §3).
- Offline jsou příkazy vypnuty.

## Když se něco pokazí
- **Zpráva „The record changed“:** pomocí **Znovu načíst** zkontrolujte aktuální prohlášení.
- **Zpráva „The outcome is uncertain“:** **Zkusit znovu** odešle stejný příkaz.
- **Záznam nelze ověřit:** zkontrolujte doklady a zdroj PMS/certifikátu; kontaktujte ověřovatele (13.03 §10).
- **Bez oprávnění:** zkontrolujte svou roli, remorkér a kontext jmenování; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).

## Související stránky
