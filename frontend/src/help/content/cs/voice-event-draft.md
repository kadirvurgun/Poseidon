---
key: voice-event-draft
title: Hlasový koncept události
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Hlasový koncept události převádí krátkou mluvenou nebo psanou poznámku na **návrhy konceptu** hlášení bezpečnostní události. Odpovídá na otázku: „Byly typ události, místo, čas, co se stalo a okamžité opatření správně převzaty z toho, co jsem řekl nebo napsal?“ Návrhy pocházejí z pevných místních pravidel a každý je vysledován k vašim vlastním slovům s mírou jistoty. Koncept upravuje a potvrzuje člověk; nic se automaticky neodesílá, neuzavírá ani nehlásí (13.03 §1, §6).

## Kdo ji používá
- **Posádka / OOW / strojník a Kapitán:** v terénu si rychle zapíší poznámku a zahájí hlášení bezpečnostní události (13.03 §2, §6).
- **DPA / HSEQ:** sleduje **Měřená použitelnost (toto plavidlo)**. Tyto údaje jsou provozním pozorováním, nikoli tvrzením o přesnosti.

## Než začnete
- Zvolte plavidlo ve svém rozsahu. Vytvoření konceptu vyžaduje oprávnění k zápisu hlasových konceptů a rozsah úprav na daném plavidle.
- Předání potvrzeného konceptu do konceptu bezpečnostní události vyžaduje také oprávnění k hlášení bezpečnostních událostí.
- Ochrana soukromí: zvuk se neukládá. Řeč využívá pouze místní engine v této instalaci; pokud žádný není nainstalován, poznámku napište nebo vložte. Vyhněte se jménům a osobním zdravotním údajům.
- Offline se nic neodesílá.

## Krok za krokem
1. Stiskněte **Nová hlasová nebo psaná poznámka**.
2. Pokud je nakonfigurován místní engine, stiskněte **Nahrát místním enginem** a poté **Zastavit a přepsat**. Jinak napište krátkou poznámku do **Text poznámky**: co, kde, kdy, co jste udělali.
3. Zkontrolujte **Datum poznámky**. Používá se pouze k vyhodnocení slov jako „dnes“ nebo „včera“.
4. Stiskněte **Vytvořit návrhy konceptu**.
5. Zkontrolujte každé pole: **Typ události**, **Kdy se stalo (místní čas)**, **Místo na palubě**, **Upřesnění místa**, **Co se stalo**, **Okamžité opatření**. Pod každým polem uvidíte míru jistoty návrhu, pravidlo a odpovídající část vaší poznámky. Přečtěte si také **Poznámky pravidel**.
6. Opravte nesprávné návrhy a stiskněte **Uložit úpravy**. Když je **Still required before confirmation:** prázdné, stiskněte **Potvrdit koncept**. Potvrzení zaznamená, že jste zkontrolovali každé pole; událost tím neodesíláte ani neuzavíráte.
7. Pokud byl po potvrzení vytvořen koncept bezpečnostní události, přejděte pomocí **Otevřít koncept bezpečnostní události** na stránku Bezpečnostní události a skoronehody a tam jej doplňte a odešlete. Pokud dosud žádný vytvořen nebyl a máte oprávnění, použijte **Vytvořit nyní koncept bezpečnostní události**.
8. Pokud poznámka není událostí nebo je chybná, zvolte **Důvod**, v případě potřeby doplňte **Poznámka (povinná pro Jiné)** a stiskněte **Zamítnout koncept**.

## Co znamenají stavy
- **Koncept (nepotvrzený):** návrhy dosud žádná osoba nepotvrdila.
- **Potvrzeno osobou:** pole byla zkontrolována; nejde o hlášení bezpečnostní události a nic nebylo odesláno.
- **Zamítnuto:** důvod zamítnutí je zaznamenán pro vyhodnocení.
- **Suggested, unchanged / Changed by you / Entered by you / Empty:** původ pole. Suggested, unchanged je návrh, který musí člověk ještě zkontrolovat.
- **Typed or pasted / Local speech engine:** jak byla poznámka zadána. Úpravou textu z místního enginu se z něj stane psaná poznámka.
- Poznámky pravidel jako **Čas bez dne; datum nebylo předpokládáno** ukazují chybějící informace; systém chybějící hodnotu neodhaduje.
- Poznámka jako **2 missing**: kolik povinných polí konceptu je v seznamu stále prázdných.

## Co nemůžete dělat / upozornění
- Návrhy jsou pouze konceptem; míra jistoty není zárukou správnosti. Před potvrzením sami zkontrolujte každé pole.
- Tato stránka neodesílá, neuzavírá ani nehlásí bezpečnostní událost. Hlášení odesíláte na stránce Bezpečnostní události a skoronehody.
- Pokud jsou zmíněny doklady, přiložte je sami; stránka nepřikládá žádné doklady.
- **Potvrdit koncept** není k dispozici, dokud jsou úpravy neuložené; nejprve je uložte.
- Bez oprávnění k hlášení bezpečnostních událostí se z potvrzeného konceptu nevytvoří žádný koncept bezpečnostní události.

## Když se něco pokazí
- **Speech capability unavailable: no local engine is configured.** nebo **Transcription was not completed.** Poznámku napište nebo vložte.
- **Přístup k mikrofonu nebyl udělen.** Zkontrolujte oprávnění prohlížeče nebo poznámku napište.
- **The record changed meanwhile.** Stiskněte **Znovu načíst** a úpravu proveďte znovu.
- **You are offline. Nothing was sent.** Až budete online, použijte **Zopakovat stejný příkaz**.
- **K této akci nemáte oprávnění.** Zkontrolujte svou roli a kontext plavidla; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).

## Související stránky
