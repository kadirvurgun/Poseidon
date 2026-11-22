---
key: risk-template-library
title: Knižnica šablón rizík
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Knižnica šablón rizík spravuje s revíziami šablóny hodnotenia rizík používané v celom nájomcovi (tenant) (druh práce, nebezpečenstvá, predvolené opatrenia, požadované bariéry, zdroje SMS a požiadaviek) a metódy rizika (maticu). Odpovedá na otázku: „Ktorá zverejnená šablóna sa vzťahuje na tento druh práce a ktorá revízia je účinná?“ Stránka je **iba vývojovým cieľom**: zverejnenie tu je zaznamenaný ľudský záznam HSEQ, nie povolenie, odborné prijatie rizika ani splnenie brány HSE (13.03 §4).

## Kto ju používa
- **DPA / HSEQ:** v rámci riadenia rizík pripravujú, posudzujú a zverejňujú revízie šablón, bariéry a maticu rizík (13.03 §4).
- **Kapitán, kapitán remorkéra a posádka:** čítajú zverejnené šablóny a posielajú k šablóne **Návrhy posádky**.

## Skôr než začnete
- Čítanie vyžaduje oprávnenie na čítanie šablón. Zápis a zverejňovanie šablón vyžadujú rozsah celého nájomcu a oprávnenie na zápis alebo zverejnenie šablón. Odoslanie návrhu vyžaduje iba oprávnenie na navrhovanie k šablóne.
- Vytvorenie a zverejnenie metódy rizika sú samostatné oprávnenia a vyžadujú rozsah celého nájomcu.
- Príkazy k rizikám vyžadujú aktuálne online spojenie; offline sa nič nezaraďuje do frontu ani neschvaľuje.

## Krok za krokom
1. Pozrite si šablóny v zozname. Každý riadok zobrazuje stav, **Aktuálne** revíziu a počet **Suggestions**. Otvorte šablónu.
2. Pre novú šablónu stlačte **Nová šablóna**. Vyplňte **Kód šablóny**, **Názov**, **Druh práce**, **Rozsah loď / remorkér**, **Zodpovedná osoba schválenia**, **Dôvod zmeny**, **Required barriers**, **SMS / requirement sources** a **Nebezpečenstvá**, potom stlačte **Uložiť koncept**.
3. Na revízii v stave konceptu stlačte **Odoslať na posúdenie**. Chýbajúce položky sú uvedené ako blokujúce položky.
4. Zverejňovateľ zadá **Platné od** a **Dôvod** s dĺžkou aspoň 10 znakov a stlačí **Zverejniť (ľudský záznam HSEQ)** alebo **Vrátiť**.
5. Ak chcete zmeniť zverejnenú šablónu, napíšte dôvod a stlačte **Začať novú revíziu**; staršia revízia sa nemení.
6. Posádka napíše návrh v **Odoslať návrh** a odošle ho. Oprávnená osoba ho posúdi pomocou **Prijať** alebo **Zamietnuť** s uvedením dôvodu.
7. V **Metódy rizika (matica)** pomocou **Nová metóda** zadajte **Kód metódy**, **Likelihood codes**, **Severity codes** a **Matrix cells**; iná osoba ju zverejní pomocou **Zverejniť metódu**.

## Čo znamenajú stavy
- **Koncept / V posudzovaní / Vrátené / Zverejnené / Nahradené:** životný cyklus revízie šablóny.
- **Aktuálne / Naplánované / Neúčinné:** pohľad na účinnosť. Najnovšia zverejnená revízia, ktorej dátum účinnosti už nastal, je **Aktuálne**; revízia s budúcim dátumom je **Naplánované**. Budúca revízia nikdy predčasne nenahradí staršiu.
- **Neznáme:** pole nie je vyplnené; prázdna hodnota sa nikdy nevyplní predvolenou hodnotou.
- **Nie je zverejnená žiadna metóda rizika: hodnotenia zostávajú nehodnotené.** Systém neponúka žiadnu predvolenú maticu ani prah.

## Čo nemôžete robiť / upozornenie
- Autor revízie ani osoba, ktorá ju odoslala, nemôže zverejniť tú istú revíziu; zverejnenie je samostatný ľudský záznam (oddelenie povinností).
- Zverejnenie nie je povolenie, odborné prijatie rizika ani schválený produkčný katalóg.
- Šablóna sa na hodnotenie rizík použije iba ako **kópia**; nenesie žiadne hodnotenie, podpis, potvrdenie účastníkov, schválenie ani potvrdenie prevzatia. Keď sa šablóna zmení, existujúce hodnotenia sa samy nezmenia.
- Zverejnená revízia sa nikdy neupravuje; na jej zmenu začnite novú revíziu.

## Ak sa niečo pokazí
- **Blokované, kým sa uvedené položky nedoplnia.** Doplňte blokujúce položky a odošlite znova.
- **This record changed elsewhere.** Stlačte **Znova načítať**, aby ste videli aktuálnu revíziu.
- **Túto akciu nesmiete vykonať (oddelenie povinností alebo rozsah).** Zverejnenie musí vykonať iná oprávnená osoba, ktorá nie je autorom revízie; ak chýba oprávnenie, kontaktujte administrátora nájomcu (13.03 §10).
- **Táto akcia už nezodpovedá aktuálnemu stavu. Znova načítajte.** Stav revízie sa zmenil.

## Súvisiace stránky
