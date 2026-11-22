---
key: tug-daily-log
title: Denní deník remorkéru
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## K čemu tato stránka slouží
Denní deník remorkéru zaznamenává pro jedno datum a hlídku **zakázky, počítadla provozních hodin motorů, palivo a závady** a porovnává je s vlečeními daného dne pro **odsouhlasení**. Odpovídá na otázku: „Kolik zakázek bylo v tomto dni/hlídce dokončeno, odpovídají hodiny počítadel časům provedení, kolik paliva bylo spotřebováno a jaké závady byly nahlášeny?“ Odsouhlasení je pouze doporučující; posouzení Kapitánem je samostatný lidský záznam.

## Kdo ji používá
- **Posádka / strážní služba:** vede záznam hlídky v reálném čase a v pořádku (13.03 §6).
- **Kapitán remorkéru / Kapitán:** posuzuje odeslaný deník a ověřuje jej nebo vrací (13.03 §3).

## Než začnete
- Otevřete remorkér z **Provoz remorkérů**.
- Vytváření a úprava deníku vyžadují přístup k zápisu remorkéru.
- Posouzení vyžaduje **Jmenování** (Kapitán remorkéru nebo Kapitán) pro daný remorkér zaznamenané na serveru. Kdo deník napsal nebo odeslal, nemůže jej posoudit.

## Krok za krokem
1. Otevřete existující deník ze seznamu, nebo zvolte **Datum deníku** a **Hlídka** a stiskněte **Vytvořit denní deník**.
2. Zadejte **Časové pásmo (IANA)**.
3. V **Počítadla provozních hodin motorů** zadejte pro každé **Počítadlo** **Počáteční stav**, **Konečný stav** a v případě potřeby **Doklad o vynulování nebo přetečení**.
4. V **Spotřebované palivo** zadejte hodnotu, jednotku a **Odkaz na zdroj údajů o palivu**.
5. V **Vlečení během dne** zaškrtněte provedení, která patří k tomuto deníku; vyplňte **Linked defect identifiers** a **Denní souhrn** (souhrn alespoň 20 znaků).
6. Stiskněte **Uložit**. Na řádku **Odsouhlasení** zkontrolujte dokončené zakázky, hodiny provedení, hodiny počítadel a nahlášené závady.
7. Když je seznam **Překážky** prázdný, stiskněte **Odeslat ke kontrole kapitánovi**.
8. Jako Kapitán zvolte své **Jmenování**, napište důvod a stiskněte **Kapitán: ověřit deník** nebo **Kapitán: vrátit deník**.

## Co znamenají stavy
- **Draft / Submitted / Returned / Verified:** stav deníku.
- **Master review — Pending / Attention / Reviewed:** Pending = čeká na posouzení; Attention = vráceno, nebo odesláno, zatímco odsouhlasení není Reconciled; Reviewed = ověřeno Kapitánem. Odeslaný deník není ověřený deník.
- **Reconciled / Review required / Incomplete:** doporučující výsledek porovnání. Kódy důvodů jsou uvedeny pod ním.
- **otevřený interval:** provedení stále probíhá; hodiny provedení ještě nejsou konečné.
- **Safety events — Unknown:** tato stránka nepočítá bezpečnostní události; nevyvozuje se žádný závěr „žádné události“.
- **Synced / Waiting to sync / Conflict / Failed:** stav synchronizace. Waiting to sync ukazuje, že změna se ještě nedostala na server.

## Co nemůžete dělat / upozornění
- Kdo deník napsal nebo jej odeslal ke kontrole kapitánovi, nemůže jej ověřit.
- Výsledek odsouhlasení není schválení; posouzení provádí Kapitán.
- Dokončení záznamu a jeho ověření nejsou totéž (13.03 §1).
- Offline se jedna neuložená změna uchovává v paměti této stránky a po obnovení připojení se odešle se svým původním klíčem; nechte stránku otevřenou. Odeslání a posouzení probíhají online.
- Pokud se objeví konflikt, nepředpokládejte, že váš vlastní záznam automaticky vyhraje (13.03 §6).

## Když se něco pokazí
- **Zpráva „The server listed missing or blocking items.“:** odstraňte kódy v **Překážky**.
- **Zpráva „The record changed or the command conflicts.“:** pomocí **Načíst nejnovější** zkontrolujte aktuální deník; uchovaná místní změna se zahodí.
- **Zpráva „The outcome is unknown.“:** **Zkusit znovu** odešle stejný příkaz znovu.
- **Konflikt:** změna uchovaná na této stránce se nikdy neslučuje; použijte **Načíst nejnovější**. Denní záznamy zachycené v **Offline operace remorkérů**, které jsou v konfliktu, se řeší tam; jsou také uvedeny v **Centrum podstatných konfliktů** (13.03 §10).

## Související stránky
