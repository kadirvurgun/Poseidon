---
key: today
title: Dnes
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## K čemu tato stránka slouží
„Dnes“ shromažďuje pro zvolenou loď nebo remorkér **upozornění, která nyní vyžadují vaši pozornost**, do jednoho seznamu. Odpovídá na otázku: „Který záznam na tomto plavidle dnes čeká na rozhodnutí nebo kontrolu a který zdroj to uvádí?“ Stránka o ničem nerozhoduje; zobrazuje upozornění, jeho zdroj a další krok.

## Kdo ji používá
- **Kapitán** a **Kapitán remorkéru:** aby viděli výjimky dne, jejich odpovědnou osobu a další krok (13.03 §2, §3).
- **Vrchní strojník:** aby sledoval upozornění z plánu údržby (13.03 §5).
- **Crew / OOW:** aby viděli práci, která na ně připadá (13.03 §6).

## Než začnete
- V horní kontextové liště ověřte, že je zvolena **správná společnost a plavidlo** (13.03 §1). Upozornění, které vidíte, patří ke zvolenému plavidlu.
- K otevření této stránky potřebujete přístup ke čtení pracovního prostoru.
- Pokud není zvoleno žádné plavidlo, stránka nejprve zobrazí seznam **Vybrat plavidlo**.

## Krok za krokem
1. Zvolte loď nebo remorkér, na kterém pracujete. Zvolené plavidlo se zobrazuje jako **Aktuální plavidlo**; ke změně použijte **Změnit plavidlo**.
2. Podívejte se na sekci **Pokrytí zdrojů** v horní části. Ukazuje, které zdroje (regulatorní, plánovaná údržba, provoz remorkérů) bylo možné načíst.
3. Čtěte karty upozornění odshora dolů. Každá karta pochází z jednoho zdroje a navrhuje další krok (například **Posoudit koncept**, **Posoudit plán údržby**, **Zobrazit zakázku remorkéru**).
4. Stiskněte **Proč?**, abyste viděli důvod karty. Sekce zobrazuje pravidlo, zdrojový záznam, jeho revizi, čas načtení a případné neznámé vstupy.
5. Následujte odkaz karty na vlastní stránku záznamu a jednejte tam v rámci svých oprávnění.
6. Pomocí **Obnovit** načtete data znovu.

## Co znamenají stavy
- **Dostupné (neověřené):** zdroj byl načten. Neznamená to, že jeho obsah byl ověřen.
- **Chybí / V tomto kontextu nedostupné:** zdroj nebylo možné pro toto plavidlo načíst. Upozornění, které nevidíte, neznamená, že je vše v pořádku.
- **Partial view:** některé zdroje nebylo možné načíst. Počet nula neznamená „žádný problém“.
- **Every listed source was read and returned no items:** nevrátilo se žádné upozornění. Toto není bezpečnostní uvolnění.
- **Neznámé:** hodnota ve zdroji chybí nebo ji nebylo možné určit. Neznámé neznamená „Ne“, „Připraveno“ ani „Nepoužitelné“.
- **Vyžaduje posouzení / Potřeba plánovací pozornosti / Doporučení:** priorita karty. Karta vyžadující posouzení čeká, až se na ni podívá oprávněná osoba.
- **Dokončeno ≠ Ověřeno:** dokončení záznamu neznamená, že jej oprávněný ověřovatel potvrdil (13.03 §1).

## Co nemůžete dělat / upozornění
- Tato stránka nečiní žádné rozhodnutí o vyplutí, operaci ani bezpečnosti. Toto rozhodnutí náleží oprávněnému Kapitánovi nebo Kapitánovi remorkéru.
- Z této stránky se nespouští žádný příkaz; akce probíhají na vlastní stránce záznamu.
- Data zobrazená offline jsou stará a neověřená. Podrobnosti **Proč?** se offline neotevřou.
- Čas načtení nebo uložení neprokazuje, že je zdroj správný.

## Když se něco pokazí
- **Offline:** pokračujte ve své kritické práci a sledujte stav synchronizace. Pokud se objeví konflikt, přejděte do centra konfliktů (13.03 §10).
- **Access denied / Vessel unavailable in your scope:** zkontrolujte svou roli a kontext plavidla; v případě potřeby kontaktujte administrátora tenanta.
- **Unexpected alert:** otevřete **Proč?**; pokud se pravidlo nebo zdroj zdají nesprávné, předejte to odpovědné osobě kontroly nebo odborníkovi na danou oblast.
- **Zdroj nedostupný / Too many requests:** chvíli počkejte a použijte **Zkusit znovu**.
- **The server response failed validation:** nic z ní se nezobrazí; nahlaste to podpoře se snímkem obrazovky, časem a svými kroky.

## Související stránky
