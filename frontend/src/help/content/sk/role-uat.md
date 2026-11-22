---
key: role-uat
title: Záznamy UAT podľa rolí
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Záznamy UAT podľa rolí zaznamenáva behy používateľského akceptačného testovania (UAT) s rolami lode a remorkéra, výsledky ich scenárov a zistenia ako **ľudské dôkazy** a porovnáva ich s dohodnutým prahom. Odpovedá na otázku: „Spĺňajú potvrdené dôkazy UAT pre každú rolu a každý scenár prah, na ktorom sa ľudia dohodli?“ Platí upozornenie v hornej časti: „Zaznamenáva iba ľudské dôkazy UAT. Systém nikdy neakceptuje UAT a nikdy nerozhoduje o GO pilotu; výsledky automatických testov sú prílohy, nie PASS.“

## Kto ju používa
- **Facilitátor UAT:** otvorí beh, zaznamená pozorované výsledky scenárov účastníka a zistenia a odošle beh na potvrdenie. Roly účastníkov na lodi zahŕňajú kapitána, DPA, hlavného strojníka a posádku (13.03 §2, §4, §5, §6).
- **Potvrdzovateľ:** beh potvrdzuje iná osoba než facilitátor.
- **Vlastníci UAT / Product Owner:** zaznamenávajú dohodnutý prah a výsledok používajú na stránke **Preskúmanie go/no-go pilotu**.

## Skôr než začnete
- Zobrazenie vyžaduje čítanie v rozsahu celého nájomcu (tenant) a čítanie UAT. Zaznamenávanie behov a výsledkov, zaznamenanie prahu a potvrdzovanie vyžadujú každé vlastné oprávnenie.
- Vlastníci UAT sa musia na prahu dohodnúť vopred; systém ho nikdy nenastavuje.
- Pre účastníka zvoľte pseudonym. Nikdy nepoužívajte meno, e-mail ani číslo člena posádky.
- Zaznamenávanie vyžaduje živé spojenie; offline je vypnuté.

## Krok za krokom
1. Tlačidlami **Prevádzkový režim** zvoľte **Loď** alebo **Remorkér**.
2. V **Dôkazy UAT voči dohodnutému prahu** skontrolujte stav, počty **Otvorené blokátory**, **Otvorené závažné zistenia**, **Potvrdené behy** a **Čaká na potvrdenie** a tabuľku scenárov.
3. Ak prah neexistuje, vyplňte v **Zaznamenať dohodnutý prah** polia **Minimálna úspešnosť scenára**, **Maximum otvorených závažných zistení**, **Zhoda s dizajnom**, **Odkaz na dohodu** a **Odôvodnenie** a stlačte **Zaznamenať**.
4. V **Behy UAT** zadajte vo formulári **Nový beh UAT** položky **Rola**, **Prostredie**, **Commit zostavenia** a **Pseudonym účastníka**, potom stlačte **Zaznamenať**.
5. Otvorte beh. Pomocou **Zaznamenať výsledok** zvoľte **Scenár** a **Výsledok**; ak je to relevantné, pridajte **Poskytnutá pomoc** a **Odkaz na automatický dôkaz**. Pri probléme použite **Zaznamenať zistenie** so **Závažnosť** a **Zhrnutie zistenia**.
6. Keď ste scenáre zaznamenali, stlačte **Odoslať beh na potvrdenie** (iba facilitátor). Scenáre bez výsledku sa počítajú ako **Nevykonané**. Iná osoba než facilitátor napíše **Vyhlásenie o potvrdení** v **Potvrdiť beh** a stlačí **Zaznamenať**.
7. V **Otvorené zistenia** uzavrite zistenie s **Riešenie** a **Poznámka k riešeniu**, potom stlačte **Vyriešiť**.

## Čo znamenajú stavy
- **Prah nenastavený:** „Nie je zaznamenaný žiadny prah dohodnutý ľuďmi. UAT nemôže prejsť, kým nebude zaznamenaný.“
- **Kritériá nesplnené / Dôkazy neúplné:** potvrdené dôkazy nespĺňajú prah alebo nie je pokrytá každá rola a každý scenár.
- **Kritériá splnené; čaká sa na ľudské rozhodnutie:** najlepší možný stav. Nie je to akceptácia UAT ani GO pilotu; rozhodnutie je ľudské.
- **Nevykonané:** pre scenár neexistuje žiadny výsledok. Scenár, ktorý nebol spustený, sa nikdy nepočíta ako úspešný.
- **Otvorené / Odoslané / Potvrdený:** stav behu. Do prahu sa započítavajú iba potvrdené behy.
- **Prešlo / Zlyhalo / Blokované / Nevykonané:** pozorovaný výsledok scenára účastníka.
- **Blokátor / Závažné / Drobné:** závažnosť zistenia. Otvorený blokátor spôsobí nesplnenie brány.
- **Fiktívne údaje:** syntetický vzorový záznam; nie je to skutočný dôkaz UAT.

## Čo nemôžete robiť / upozornenie
- Nemôžete potvrdiť beh, ktorý ste facilitovali: „Potvrdenie musí zaznamenať iný človek než facilitátor.“
- Nemôžete vyriešiť zistenie, ktoré ste nahlásili. Blokátor sa uzatvára iba ako **Vyriešené (opravené a znova otestované)**; **Riziko prijaté** sa neponúka.
- Automatický výsledok (Playwright) je iba priložený dôkaz; výsledkom je pozorovaný výsledok účastníka.
- Systém nikdy neakceptuje UAT, nikdy nenastavuje prah a nikdy nevytvára falošné potvrdenia ani schválenia.

## Ak sa niečo pokazí
- **K týmto záznamom nemáte prístup.** Skontrolujte svoju rolu a rozsah nájomcu a kontaktujte Tenant Admin (13.03 §10).
- **Záznam sa vo vašom tenante nenašiel.** Skontrolujte odkaz na beh a zvolený režim.
- **Záznam sa zmenil alebo už bol použitý. Znova načítajte.** Aktuálny stav otvoríte pomocou **Znova načítať**.
- **Výsledok je neistý. Opakovanie odošle rovnaký príkaz.** Pomocou **Skúsiť znova** bezpečne odošlete ten istý príkaz znova.
- **Odmietnuté pravidlami.** Napríklad scenár nepatrí k role behu alebo blokátor nie je možné uzavrieť ako prijaté riziko.
- **Nie je povolené.** Napríklad ste tento beh facilitovali a nemôžete ho potvrdiť, alebo ste toto zistenie nahlásili a nemôžete ho vyriešiť.
- **Ste offline. Záznam je vypnutý, kým sa pripojenie neobnoví.** Keď sa obnoví, stlačte **Obnoviť**.

## Súvisiace stránky
