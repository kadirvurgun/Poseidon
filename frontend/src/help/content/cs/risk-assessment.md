---
key: risk-assessment
title: Hodnocení rizik
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Hodnocení rizik zaznamenává pro práci nebo operaci na plavidle nebezpečí, možné následky, kontrolní opatření, počáteční a zbytkové riziko a podmínky pro zastavení práce. Odpovídá na otázku: „Byla nebezpečí a opatření pro tuto práci zvážena, posouzena a účastníky potvrzena?“ Stránka je **pouze vývojovým cílem**: schválení zde je zaznamenané lidské posouzení, nikoli povolení, odborné přijetí rizika ani splnění brány HSE (13.03 §2, §4).

## Kdo ji používá
- **Kapitán a Kapitán remorkéru:** nechávají připravit hodnocení pro práci na lodi nebo remorkéru a rozhodují o zastavení práce. Rozhodovací pravomoc zůstává u nich (13.03 §2, §3).
- **DPA / HSEQ:** dohlíží na řízení rizik a zaznamenává rozhodnutí o posouzení (13.03 §4).
- **Posádka / OOW / strojník:** jako účastníci potvrzují nebo rozporují schválené hodnocení sami za sebe (13.03 §6).

## Než začnete
- Zvolte plavidlo, abyste otevřeli jeho hodnocení rizik. Čtení vyžaduje oprávnění ke čtení rizik; zápis konceptu vyžaduje oprávnění k zápisu rizik a rozsah úprav na daném plavidle. Rozhodnutí o posouzení vyžaduje oprávnění ke schválení rizik; potvrzení vyžaduje oprávnění k potvrzení rizik.
- Hodnocení vyžadují v tenantu zveřejněnou metodu rizika (matici). Bez ní zůstávají hodnocení **Nehodnoceno**. Vlastník HSEQ tenanta nakonfiguruje matici a jiná osoba ji zveřejní (stránka Knihovna šablon rizik).
- Příkazy rizik vyžadují aktuální online připojení; offline se nic nezařazuje do fronty ani neschvaluje.

## Krok za krokem
1. Zvolte plavidlo a otevřete hodnocení ze seznamu, nebo stiskněte **Nové hodnocení**.
2. Zadejte **Číslo hodnocení**. Volitelně zadejte **Druh práce** a pomocí **Začít ze zveřejněné šablony (pouze kopie)** vyberte odpovídající šablonu; šablona se do nového konceptu pouze zkopíruje.
3. Vyplňte **Název**, **Kontext provozu / práce**, **Druh práce**, **Rozsah práce** a **Metoda rizika**. U **Nutné zastavení práce** zvolte **Ano**, **Ne** nebo **Neznámé** a v případě potřeby napište **Stop-work conditions**.
4. Pomocí **Přidat nebezpečí** zadejte pro každé nebezpečí **Pracovní krok**, **Potential consequences**, **Počáteční riziko** a **Zbytkové riziko** (pravděpodobnost, závažnost, odůvodnění). Pomocí **Přidat opatření** přidejte **Kontrolní opatření**, **Odpovědná osoba opatření** a **Barrier references**.
5. Do **Účastníci** napište na každý řádek jedno ID aktéra a roli. V případě potřeby propojte související záznamy pomocí **Přidat odkaz**.
6. Stiskněte **Uložit koncept** a poté **Odeslat ke kontrole**. **Blokující položky** uvádí, co chybí.
7. Posuzovatel napíše **Důvod** o délce alespoň 10 znaků a stiskne **Schválit (záznam lidské kontroly)** nebo **Vrátit**.
8. Po schválení každý účastník sám za sebe stiskne **Potvrdit** nebo s uvedením důvodu **Zpochybnit**. Je-li potřeba změna, použijte **Změnit (otevře nový koncept)**.

## Co znamenají stavy
- **Draft / In review / Returned / Approved (human record):** životní cyklus hodnocení. Schváleno je záznam lidského posouzení, nikoli povolení.
- **Not assessed / Method not configured:** hodnocení neexistuje. Systém nepoužívá žádnou výchozí matici, násobení ani prahy nízké/střední/vysoké.
- **Stop-work required — Unknown:** dosud nebylo učiněno žádné rozhodnutí; nikdy se nečte jako „Ne“.
- **Acknowledgement not applicable before approval / Acknowledgements pending / All participants acknowledged / Disputed – unresolved:** stav potvrzení účastníků. Rozpor nikdy neuzavře potvrzení někoho jiného.
- **Changed since pinned / Unavailable:** propojený záznam se po připnuté revizi změnil nebo jej již nelze načíst; posuďte jej znovu.

## Co nemůžete dělat / upozornění
- Autor nebo osoba, která odeslala cyklus posouzení, nemůže schválit totéž hodnocení (oddělení povinností).
- Každý potvrzuje pouze sám za sebe a pouze přesně schválenou revizi.
- Schválení zde není povolení, odborné přijetí rizika ani brána HSE. Rozhodnutí o zahájení nebo zastavení práce zůstává u Kapitána nebo Kapitána remorkéru.
- Koncept zahájený ze šablony nenese žádné hodnocení, podpis, potvrzení účastníků ani schválení.
- Offline nelze hodnocení odeslat, schválit ani potvrdit.

## Když se něco pokazí
- **Blokováno, dokud nebudou uvedené položky doplněny.** Doplňte blokující položky a odešlete znovu.
- **This record changed elsewhere.** Stiskněte **Znovu načíst**, abyste viděli aktuální revizi, a poté akci zopakujte.
- **Tuto akci nesmíte provést (oddělení povinností nebo rozsah).** Požádejte o posouzení jinou oprávněnou osobu nebo zkontrolujte svou roli a kontext plavidla; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **Pracovní prostor rizik není s vaším přístupem dostupný.** Zkontrolujte svá oprávnění.
- Pokud hodnocení vypadá neočekávaně, zkontrolujte metodu rizika a její revizi; zeptejte se odpovědné osoby opatření nebo vlastníka HSEQ (13.03 §10).

## Související stránky
