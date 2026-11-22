---
key: industry-assurance
title: Odvětvové zajištění (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Odvětvové zajištění importuje výsledky inspekce SIRE 2.0 pro tanker ze sešitu, předkládá změny k lidské kontrole, propojuje zjištění s bezpečnostními opatřeními a zaznamenává doklady z terénu. Odpovídá na otázku: „Která zjištění této inspekce jsou otevřená, ke kterým je propojeno opatření a kde jsou doklady?“ Importy jsou koncepty: každou změněnou položku přijímá jiná osoba. Zjištění zůstávají otevřená a neověřená; tato stránka je nikdy neuzavírá ani neověřuje. Řetězec je Zjištění → Opatření → Ověření a ověření a účinnost jsou samostatné lidské záznamy u opatření v části Bezpečnostní události a skoronehody (13.03 §4, §8).

## Kdo ji používá
- **DPA / HSEQ:** propojuje zjištění z inspekce s opatřeními a sleduje řetězec Zjištění → Opatření → Ověření (13.03 §4).
- **Regulatory / Assurance user:** spravuje mapování sloupců a připravuje nebo kontroluje importy (13.03 §8).
- **Kapitán a lodní personál:** zaznamenávají doklady z terénu (fotografie) ke zjištění.

## Než začnete
- Vyberte plavidlo. SIRE 2.0 se vztahuje pouze na tankery. Pokud typ lodi není v profilu plavidla deklarován, importy zůstávají ve stavu **vyžaduje kontrolu**.
- Čtení vyžaduje oprávnění ke čtení inspekcí. Import, přijetí importu, zápis dokladů, propojení zjištění a zápis mapování jsou samostatná oprávnění. Propojení opatření vyžaduje také oprávnění ke čtení bezpečnostních událostí; zaznamenání dokladu vyžaduje také oprávnění ke čtení souborů.
- Schéma sloupců je syntetické; licencovaný formát OCIMF se nepoužívá.
- V režimu offline se v tomto zařízení řadí do fronty pouze pořízené fotografie; importy a rozhodnutí vyžadují připojení.

## Krok za krokem
1. **Vyberte plavidlo** a zkontrolujte řádek **Použitelnost**.
2. V případě potřeby zadejte v části **Mapování sloupců** **Klíč mapování**, napište jeden řádek na sloupec (záhlaví=pole) a stiskněte **Uložit jako novou verzi**.
3. V části **Připravit import** zvolte **Verze mapování**, **Reference inspekce**, **Datum inspekce** a **Sešit (.xlsx)** a pak stiskněte **Předat ke kontrole**.
4. Jiná osoba v části **Kontrola importu** zvolí u každé změněné položky **Přijmout** nebo **Ponechat stávající**, napíše **Důvod** o délce nejméně 10 znaků a stiskne **Přijmout import** nebo **Zamítnout import**.
5. Otevřete inspekci ze seznamu **Inspekce**. Zkontrolujte **Položky** a **Zjištění**.
6. Chcete-li propojit zjištění s opatřením, zadejte **ID bezpečnostního opatření** a stiskněte **Propojit opatření**.
7. Pro doklad z terénu zadejte **Médium**, **ID čistého souboru**, **Revize souboru** a **Kontrolní součet souboru (SHA-256)** a stiskněte **Zaznamenat doklad z terénu**. V režimu offline se tlačítko změní na **Uložit v tomto zařízení**; po připojení ho odešlete pomocí **Odeslat nyní**.

## Co znamenají stavy
- **Tanker: použitelné / Nepoužije se / Typ plavidla neznámý: nutná kontrola:** při neznámém typu lodi zůstává výsledek **Vyžaduje kontrolu**; nikdy se nepředpokládá, že se nepoužije.
- **Čeká na kontrolu / Přijato / Zamítnuto:** stav importu.
- **Nové / Změněno / Beze změny / Není v tomto importu:** položka porovnaná s dříve přijatým importem. Položka, která není v tomto importu, je **Ponecháno (nikdy se nemaže)**.
- **Negativní pozorování / Pozitivní pozorování / Bez pozorování / Neznámé:** pozorování u položky.
- **Otevřené / Neověřeno:** zjištění se zde vždy zobrazují jako otevřená a neověřená.
- **Žádné propojené opatření / Opatření propojeno:** zda je zjištění propojeno s bezpečnostním opatřením. Propojené opatření neznamená, že je zjištění ověřeno nebo že je opatření účinné.
- **Pořízeno online / Pořízeno offline:** jak byl doklad pořízen.

## Co nemůžete dělat / upozornění
- Osoba, která import připravila, ho nemůže zkontrolovat: „Tento import jste připravil(a) vy; zkontrolovat jej musí někdo jiný.“
- Tato stránka zjištění neuzavírá ani neověřuje. Ověřeno není Účinné; účinnost je samostatné lidské posouzení u opatření.
- Videodoklady nejsou dostupné, dokud provozovatel nepovolí typ souboru skenovaného videa.
- Položka, která v pozdějším importu chybí, se nikdy nemaže; historie se uchovává.

## Když se něco pokazí
- **Požadavek byl odmítnut:** vyřešte důvody uvedené pod ním.
- **Záznam se mezitím změnil. Načtěte jej znovu a zkontrolujte.** Stiskněte **Znovu načíst**.
- **Výsledek je neznámý (spojení ztraceno).** Použijte **Zopakovat stejný požadavek**.
- **Nemáte oprávnění číst záznamy odvětvového zajištění.** nebo **K tomu nemáte oprávnění.** Zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce tenanta (13.03 §10).
- Pokud doklad nelze ověřit, zkontrolujte ID souboru, revizi a kontrolní součet SHA-256; v případě potřeby se obraťte na ověřovatele (13.03 §10).

## Související stránky
