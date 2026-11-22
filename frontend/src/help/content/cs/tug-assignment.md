---
key: tug-assignment
title: Přidělení remorkéru
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka zobrazuje **návrhy přidělení remorkérů** pro odeslanou zakázku remorkéru a jejich **konflikty** s jinými zakázkami. Odpovídá na otázku: „Který remorkér byl pro tuto zakázku navržen, s jakou rolí a časovým oknem, je v konfliktu s jinou prací a přijal Kapitán remorkéru návrh?“ Pobřeží navrhuje, Kapitán remorkéru potvrzuje; stránka nečiní žádné rozhodnutí o vyslání ani uvolnění. Upozornění v horní části to uvádí: „Systém nikdy nevydává GO.“

## Kdo ji používá
- **Koordinátor provozu remorkérů / pobřežní provoz:** po kontrole konfliktů a připravenosti na přehledu zakázek vytváří návrh přidělení (13.03 §7).
- **Kapitán remorkéru:** potvrzuje návrh pro svůj vlastní remorkér v kontextu a přijímá jej nebo odmítá (13.03 §3).

## Než začnete
- Otevřete remorkér z **Provoz remorkérů**; pokud není zvolen žádný remorkér, stránka vás tam odkáže.
- Navrhování vyžaduje přístup k zápisu remorkéru a zápisu plánu; zakázka musí být **Odesláno**.
- Odpověď vyžaduje **Jmenování** (Kapitán remorkéru nebo Kapitán) pro daný remorkér zaznamenané na serveru. Osoba, která návrh vytvořila, na něj nemůže odpovědět.

## Krok za krokem
1. Otevřete číslo zakázky z **Vyberte odeslanou zakázku remorkéru**. Tuto stránku otevírá také odkaz **Přidělení a připravenost** v detailu zakázky.
2. U existujících návrhů si přečtěte **Stav**, **Potvrzení kapitána remorkéru**, **Role**, **Plánovaný začátek** a **Plánovaný konec**.
3. V **Konflikty** uvidíte časové a zdrojové konflikty s jinými zakázkami, jejich odznak výsledku a kód důvodu.
4. Pro nový návrh zvolte ve formuláři **Navrhnout přidělení** položky **Remorkér**, **Plánovaný začátek**, **Plánovaný konec** a **Role** (Lead, Support, Escort) a stiskněte **Navrhnout přidělení**.
5. Jako Kapitán remorkéru zvolte své **Jmenování**, v případě potřeby doplňte **Důvod** a stiskněte **Přijmout** nebo **Zamítnout**. Pro zamítnutí je důvod povinný.
6. Chcete-li návrh odstranit, zadejte důvod a stiskněte **Stáhnout návrh**.
7. Pomocí odkazů **Kontrola provozní připravenosti** a **Posádka a vlečná způsobilost** pod každým návrhem přejdete na obrazovky připravenosti.

## Co znamenají stavy
- **Proposed / Withdrawn:** životní cyklus návrhu. Navrženo neznamená, že byl remorkér vyslán.
- **Pending / Accepted / Rejected:** odpověď Kapitána remorkéru na návrh. Přijato potvrzuje rozsah návrhu; není to rozhodnutí GO.
- **Pass / Fail / Unknown:** výsledek kontroly konfliktů. Unknown ukazuje, že pro kontrolu nebylo dost zdrojových dat; nikdy neznamená „žádný konflikt“.
- **Žádný konflikt nezjištěn; vstupy mohou být neúplné.:** žádný konflikt nebyl zjištěn, ale není to úplný výsledek způsobilosti.
- **Požadavek se po tomto návrhu změnil; je nutný nový návrh.:** požadavek zakázky se změnil po revizi, ze které návrh vycházel.

## Co nemůžete dělat / upozornění
- Návrh přidělení z pobřeží nikdy nepřevažuje nad bezpečnostním rozhodnutím Kapitána remorkéru na palubě (13.03 §3, §7).
- Navrhovatel nemůže přijmout svůj vlastní návrh; odpověď náleží pouze osobě jmenované pro daný remorkér.
- Pro zakázku ve stavu konceptu nebo zrušenou zakázku nelze vytvořit žádný návrh.
- Offline jsou příkazy vypnuty.

## Když se něco pokazí
- **Zpráva „The outcome is uncertain“:** **Zkusit znovu** odešle stejný příkaz; nevytvářejte nový návrh.
- **Zpráva „The record changed“:** pomocí **Znovu načíst** zkontrolujte aktuální záznam a poté jednejte znovu.
- **„You are not authorized for this action.“ / „You do not have access to this record.“:** zkontrolujte svou roli, remorkér a kontext jmenování; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **Neočekávaný konflikt:** poznamenejte si kód důvodu a projděte jej s vlastníkem druhé zakázky.

## Související stránky
