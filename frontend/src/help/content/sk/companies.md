---
key: companies
title: Spoločnosti
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## Na čo slúži táto stránka
Stránka Spoločnosti uvádza **záznamy spoločností** vášho pracovného priestoru, ktoré máte oprávnenie vidieť. Odpovedá na otázku: „Ktoré spoločnosti sú v tomto pracovnom priestore definované a aký majú kód a názov?“ Záznamy lodí a remorkérov patria spoločnosti, preto je správna spoločnosť prvým krokom pri výbere vášho kontextu.

## Kto ju používa
- **Tenant Admin:** vytvára a upravuje záznamy spoločností podľa zásady najnižších oprávnení (13.03 §9).
- **Pobrežné operácie:** hľadajú správny kontext spoločnosti.

## Skôr než začnete
- Na zobrazenie tejto stránky potrebujete oprávnenie na čítanie spoločností. Bez neho sa zobrazí správa „You do not have access to this workspace.“
- Vytvorenie záznamu vyžaduje oprávnenie na vytváranie. To, či môžete záznam upraviť, závisí od oprávnenia, ktoré server pre daný záznam udelí.

## Krok za krokom
1. V zozname si pozrite kód a názov každej spoločnosti. Na ďalšie strany sa dostanete pomocou **Predchádzajúce** / **Ďalšie**.
2. Ak chcete vyhľadávať, píšte do poľa **Vyhľadať podľa kódu alebo názvu** a stlačte **Hľadať**.
3. Vyberte názov spoločnosti, čím otvoríte časť **Podrobnosti**.
4. Pre novú spoločnosť stlačte **Nový záznam**, zadajte kód a názov a stlačte **Uložiť**. Všetky polia sú povinné.
5. Ak chcete zmeniť existujúci záznam, použite v podrobnostiach **Upraviť**.
6. Otvorte **Zobraziť záznam právnickej osoby**, ak chcete vidieť údaje o právnickej osobe; táto časť je iba na čítanie.

## Čo znamenajú stavy
- **No records found:** pre vaše vyhľadávanie alebo rozsah oprávnení sa nevrátil žiadny záznam.
- **Právnická osoba: Koncept – neúplný, neaktívny / Aktívna / Neaktívna:** stav záznamu právnickej osoby. Koncept znamená, že pred aktiváciou ešte chýbajú hodnoty.
- **Neznáme:** hodnota v zázname nie je. Neznáme neznamená „Nie“ ani „platné“.
- **Saved:** záznam bol zapísaný na server. Tento záznam nevytvára žiadne prevádzkové schválenie.

## Čo nemôžete robiť / upozornenie
- Vytvorenie alebo úprava záznamu spoločnosti nie je prevádzkové schválenie, schválenie súladu ani bezpečnostné schválenie.
- Hodnoty právnickej osoby sa na tejto stránke nemenia; menia sa iba prostredníctvom auditovaných personálnych (HR) príkazov. Identifikátory z registra sa zobrazujú maskované.
- Ten istý kód nemôžu používať dve spoločnosti.
- Ak opustíte stránku s neuloženým konceptom, budete vyzvaní, či ho chcete zahodiť alebo pokračovať v úprave.

## Ak sa niečo pokazí
- **Tento záznam sa zmenil v inej operácii:** váš koncept zostane zachovaný. Zahoďte koncept, aby sa načítal aktuálny záznam, a potom zmenu urobte znova.
- **Tento kód sa už používa:** zadajte iný kód.
- **Výsledok nebolo možné potvrdiť:** polia zostanú zamknuté, kým nebude výsledok známy; bezpečne zopakujte ten istý príkaz.
- **Vaše oprávnenie sa mohlo zmeniť / Bez prístupu:** skontrolujte svoju rolu a kontext pracovného priestoru; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Služba nie je dostupná:** použite **Skúsiť znova**; ak problém pretrváva, nahláste ho s referenciou podpory.

## Súvisiace stránky
