---
key: risk-assessment
title: Hodnotenie rizík
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Hodnotenie rizík zaznamenáva pre prácu alebo operáciu na lodi nebezpečenstvá, možné následky, kontrolné opatrenia, počiatočné a zvyškové riziko a podmienky zastavenia práce. Odpovedá na otázku: „Boli nebezpečenstvá a opatrenia pre túto prácu zvážené, posúdené a potvrdené účastníkmi?“ Stránka je **iba vývojovým cieľom**: schválenie tu je zaznamenané ľudské posúdenie, nie povolenie, odborné prijatie rizika ani splnenie brány HSE (13.03 §2, §4).

## Kto ju používa
- **Kapitán a kapitán remorkéra:** dávajú pripraviť hodnotenie pre prácu na lodi alebo remorkéri a rozhodujú o zastavení práce. Rozhodovacia právomoc zostáva u nich (13.03 §2, §3).
- **DPA / HSEQ:** dohliadajú na riadenie rizík a zaznamenávajú rozhodnutie o posúdení (13.03 §4).
- **Posádka / OOW / strojník:** ako účastníci potvrdzujú alebo spochybňujú schválené hodnotenie za seba (13.03 §6).

## Skôr než začnete
- Zvoľte loď, aby ste otvorili jej hodnotenia rizík. Čítanie vyžaduje oprávnenie na čítanie rizík; zápis konceptu vyžaduje oprávnenie na zápis rizík a rozsah úprav na danej lodi. Rozhodnutie o posúdení vyžaduje oprávnenie na schválenie rizík; potvrdenie vyžaduje oprávnenie na potvrdenie rizík.
- Hodnotenia vyžadujú zverejnenú metódu rizika (maticu) v rámci nájomcu (tenant). Bez nej hodnotenia zostávajú **Nehodnotené**. Maticu nastavuje zodpovedná osoba HSEQ nájomcu a zverejňuje ju iná osoba (stránka Knižnica šablón rizík).
- Príkazy k rizikám vyžadujú aktuálne online spojenie; offline sa nič nezaraďuje do frontu ani neschvaľuje.

## Krok za krokom
1. Zvoľte loď a otvorte hodnotenie zo zoznamu, alebo stlačte **Nové hodnotenie**.
2. Zadajte **Číslo hodnotenia**. Voliteľne zadajte **Druh práce** a pomocou **Začať zo zverejnenej šablóny (iba kópia)** vyberte zodpovedajúcu šablónu; šablóna sa do nového konceptu iba skopíruje.
3. Vyplňte **Názov**, **Kontext prevádzky / práce**, **Druh práce**, **Rozsah práce** a **Metóda rizika**. Pri **Potrebné zastavenie práce** zvoľte **Áno**, **Nie** alebo **Neznáme** a v prípade potreby napíšte **Stop-work conditions**.
4. Pomocou **Pridať nebezpečenstvo** zadajte pre každé nebezpečenstvo **Pracovný krok**, **Potential consequences**, **Počiatočné riziko** a **Zvyškové riziko** (pravdepodobnosť, závažnosť, odôvodnenie). Pomocou **Pridať opatrenie** pridajte **Kontrolné opatrenia**, **Zodpovedná osoba opatrenia** a **Barrier references**.
5. V **Účastníci** napíšte na každý riadok jedno ID aktéra a rolu. V prípade potreby prepojte súvisiace záznamy pomocou **Pridať prepojenie**.
6. Stlačte **Uložiť koncept**, potom **Odoslať na posúdenie**. **Blokujúce položky** uvádza, čo chýba.
7. Posudzovateľ napíše **Dôvod** s dĺžkou aspoň 10 znakov a stlačí **Schváliť (záznam ľudskej kontroly)** alebo **Vrátiť**.
8. Po schválení každý účastník za seba stlačí **Potvrdiť** alebo s uvedením dôvodu **Spochybniť**. Ak je potrebná zmena, použite **Zmeniť (otvorí nový koncept)**.

## Čo znamenajú stavy
- **Koncept / V posudzovaní / Vrátené / Schválené (ľudský záznam):** životný cyklus hodnotenia. Schválené je záznam ľudského posúdenia, nie povolenie.
- **Nehodnotené / Metóda nie je nastavená:** hodnotenie neexistuje. Systém nepoužíva žiadnu predvolenú maticu, násobenie ani prah nízke/stredné/vysoké.
- **Potrebné zastavenie práce — Neznáme:** zatiaľ nebolo prijaté žiadne rozhodnutie; nikdy sa nečíta ako „Nie“.
- **Potvrdenie pred schválením sa neuplatňuje / Potvrdenia čakajú / Všetci účastníci potvrdili / Namietané – nevyriešené:** stav potvrdenia účastníkov. Námietka sa nikdy neuzavrie potvrdením niekoho iného.
- **Zmenené od pripnutia / Nedostupné:** prepojený záznam sa zmenil po pripnutej revízii alebo ho už nie je možné prečítať; posúďte ho znova.

## Čo nemôžete robiť / upozornenie
- Autor alebo ten, kto odoslal cyklus posúdenia, nemôže schváliť to isté hodnotenie (oddelenie povinností).
- Každý potvrdzuje iba za seba a iba presne schválenú revíziu.
- Schválenie tu nie je povolenie, odborné prijatie rizika ani brána HSE. Rozhodnutie o začatí alebo zastavení práce zostáva u kapitána alebo kapitána remorkéra.
- Koncept začatý zo šablóny nenesie žiadne hodnotenie, podpis, potvrdenie účastníkov ani schválenie.
- Offline nie je možné hodnotenie odoslať, schváliť ani potvrdiť.

## Ak sa niečo pokazí
- **Blokované, kým sa uvedené položky nedoplnia.** Doplňte blokujúce položky a odošlite znova.
- **This record changed elsewhere.** Stlačte **Znova načítať**, aby ste videli aktuálnu revíziu, a potom akciu zopakujte.
- **Túto akciu nesmiete vykonať (oddelenie povinností alebo rozsah).** Požiadajte o posúdenie inú oprávnenú osobu alebo skontrolujte svoju rolu a kontext lode; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).
- **Pracovný priestor rizík nie je s vaším prístupom dostupný.** Skontrolujte svoje oprávnenia.
- Ak hodnotenie pôsobí neočakávane, skontrolujte metódu rizika a jej revíziu; obráťte sa na zodpovednú osobu opatrenia alebo zodpovednú osobu HSEQ (13.03 §10).

## Súvisiace stránky
