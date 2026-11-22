---
key: pilot-program
title: Pilotní program: týdny a zpráva o hodnotě
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Pilotní program uchovává na jednom místě pilotní týdny pro loď (a volitelně remorkér), položky kontrolního seznamu každého týdne, problémy pilotu, denní zpětnou vazbu a zprávu o hodnotě. Odpovídá na otázku: „Byly položky každého pilotního týdne splněny skutečnými záznamy a jak se změřené hodnoty změnily oproti výchozímu stavu?“ Platí upozornění nahoře: „Software nemůže pilot dokončit. Položka kontrolního seznamu je splněna jen vaším propojením se skutečným záznamem ověřeným serverem; chybějící výchozí hodnota zůstává Neznámá; týden uzavírá a rozhodnutí programu zapisuje pouze jmenovaný Product Owner.“

## Kdo ji používá
- **Vlastník produktu:** uzavírá týdny a zaznamenává rozhodnutí programu (Pokračovat, Opravit, Zastavit, Rozšířit). To vyžaduje platné jmenování vlastníkem produktu.
- **Pilotní tým:** propojuje záznamy s položkami kontrolního seznamu, hlásí problémy, zaznamenává zpětnou vazbu a měření.
- **Vlastník triáže:** provádí triáž problémů a řeší je.
- 13.03 pro tuto stránku nedefinuje samostatnou roli; platí zásady správy tenanta (13.03 §9).

## Než začnete
- Zobrazení vyžaduje čtení pro celý tenant a čtení pilotního programu. Vytváření programů, zaznamenávání, triáž a rozhodování vyžadují každé své vlastní oprávnění; uzavření týdnů a rozhodnutí vyžadují také jmenování vlastníkem produktu.
- Záznamy, které propojujete (například záznam osvědčení, posádky nebo pracovního příkazu), musí již existovat na svých vlastních stránkách.
- Do zpětné vazby nikdy nezadávejte jména, e-mailové adresy, telefonní čísla ani čísla průkazů; používejte pouze pseudonym a roli.
- Zaznamenání vyžaduje živé připojení.

## Krok za krokem
1. Otevřete program ze seznamu **Pilotní programy**. Pro nový program vyplňte v části **Vytvořit pilotní program** **Název programu**, **Loď**, **Remorkér (volitelné)**, **Datum začátku 1. týdne** a **Časové pásmo kalendáře (IANA)** a stiskněte **Zaznamenat**.
2. V každé části **Týden** zkontrolujte sloupce **Položka kontrolního seznamu**, **Kritérium (zdroj)**, **Oblast**, **Stav** a **Propojené záznamy**.
3. Chcete-li položku splnit, zadejte v části **Propojit záznam** **Položka kontrolního seznamu**, **Typ záznamu** a **Identifikátor záznamu** a stiskněte **Propojit záznam**. Chybné propojení odstraňte pomocí **Stáhnout propojení** poté, co napíšete **Důvod stažení**.
4. V části **Problémy pilotu** použijte **Nahlásit problém** a zadejte oblast, **Kategorie**, závažnost a **Shrnutí problému**. Vlastník triáže použije **Triáž** s **Poznámka k triáži** a pak problém vyřeší.
5. V části **Denní zpětná vazba** použijte **Zaznamenat zpětnou vazbu** a zadejte **Shrnutí** a **Hodnocení**.
6. V části **Zpráva o hodnotě** použijte **Zaznamenat měření** a zadejte **Metrika**, **Fáze** (Výchozí hodnota nebo Pilotní období), **Hodnota**, **Velikost vzorku**, **Období od**, **Období do**, **Odkaz na doklad** a **Metoda a vzorek**.
7. Pokud jste vlastník produktu, zaznamenejte v části **Uzavřít týden** výsledek a odůvodnění. Na konci programu vyplňte v části **Zaznamenat rozhodnutí programu** rozhodnutí, **Jmenování** a **Odůvodnění** a stiskněte **Zaznamenat**.

## Co znamenají stavy
- **Propojeno a ověřeno:** s položkou je propojen skutečný záznam ověřený serverem. Položky **Propojení již neplatí** a **Chybí** nejsou splněny.
- **Vyžaduje se dokončení:** propojený záznam musí být ve svém dokončeném stavu. „Auditní stopa dokládá, že záznam napsala osoba; nedokládá, že záznam nebo postup je dokončen.“
- **Připraven k uzavření člověkem / Nepřipraveno:** doporučení pro týden; uzavírá ho pouze vlastník produktu jako **Přijato** nebo **Nepřijat**.
- **Triáž týž den / Triáž opožděně / Triáž očekávána dnes / Po termínu:** časování triáže problémů P0 vůči kalendářnímu dni programu. U problémů se stavem **Zdroj neuvádí cíl téhož dne** si systém žádnou lhůtu nevymýšlí.
- **Změřeno / Neznámé:** když výchozí nebo pilotní hodnota chybí, zpráva zůstává **Neznámé**; nikdy se nepočítá jako nula ani jako zlepšení. Hodnoty **Nesrovnatelné** se neporovnávají.
- **Telemetrie (deklarovaná, neověřená):** deklarovaná hodnota telemetrie.
- **Rozšíření možné; čeká se na rozhodnutí člověka / Doporučena oprava:** doporučení programu, nikoli rozhodnutí.

## Co nemůžete dělat / upozornění
- „Přijato je možné jen tehdy, když je každá položka ověřena a žádná triáž P0 není otevřená, čekající ani opožděná.“
- „Rozšíření je možné jen tehdy, když je každý týden přijat a hodnoty, UAT, vady a P0 jsou vyřešeny.“
- Pokud jste autorem dokladů, na kterých týden nebo rozhodnutí stojí (propojení, zpětná vazba, měření), nemůžete tento týden uzavřít ani toto rozhodnutí zaznamenat.
- Pro fiktivní testovací program nelze zaznamenat uzavření týdne ani rozhodnutí.
- Systém nikdy nedokončuje pilot, nikdy neuzavírá týden a nikdy nevytváří rozhodnutí ani schválení.

## Když se něco pokazí
- **Nemáte platné jmenování Product Ownera; uzavření a rozhodnutí jsou jen pro čtení.** Kvůli jmenování se obraťte na Tenant Admin (13.03 §10).
- **Sběr zpětné vazby není dostupný, dokud operátor nenastaví zásadu uchovávání.** Požádejte správce systému o nastavení zásady uchovávání.
- **Záznam se změnil nebo již byl použit. Znovu načtěte.** Aktuální stav otevřete pomocí **Znovu načíst**.
- **Výsledek je nejistý. Opakování odešle stejný příkaz.** Pomocí **Zkusit znovu** bezpečně odešlete stejný příkaz znovu.
- **Odmítnuto pravidly.** Propojený záznam například nemusí existovat, může být mimo rozsah nebo nemusí být dokončen.
- **Nepovoleno.** Jste autorem propojení, zpětné vazby nebo měření, na kterých toto uzavření týdne nebo rozhodnutí stojí; musí ho zaznamenat jiná osoba.
- Týden, který ještě nezačal, nelze uzavřít (zobrazí se jako „Záznam se změnil nebo již byl použit. Znovu načtěte.“).
- **K těmto záznamům nemáte přístup.** Zkontrolujte svou roli a rozsah tenanta (13.03 §10).

## Související stránky
