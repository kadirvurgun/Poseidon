---
key: towage-risk-briefing
title: Riziko vleku a briefing
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka pripravuje a vydáva **záznam o riziku vleku a briefingu** pre odoslanú zákazku remorkéra a zhromažďuje **potvrdenia** účastníkov. Odpovedá na otázku: „Ktoré podmienky, nebezpečenstvá, preventívne a zmierňujúce bariéry, právomoc zastaviť prácu a núdzové opatrenia boli predmetom briefingu, aký je stav prepojeného posúdenia rizík a potvrdil každý účastník vydanú revíziu?“ Vydanie briefingu je ľudský záznam menovaného kapitána alebo kapitána remorkéra; nie je to rozhodnutie GO ani povolenie. Neúplný briefing blokuje READY.

## Kto ju používa
- **Kapitán remorkéra / kapitán:** na prípravu a vydanie briefingu a na zabezpečenie, aby sa žiadne rozhodnutie READY neprijalo pred dokončením rizika a briefingu (13.03 §3).
- **Posádka a ďalší účastníci (lodivod, dôstojník, palubná posádka, strojník):** na potvrdenie alebo spochybnenie vydaného briefingu v správnom kontexte roly (13.03 §6).
- **Koordinátor prevádzky remorkérov:** na sledovanie stavu briefingu a potvrdení (13.03 §7).

## Skôr než začnete
- Otvorte remorkér z **Prevádzka remorkérov** a zvoľte zákazku z **Vyberte odoslanú zákazku remorkéra**.
- **Plán vleku** zákazky musí byť schválený a prepojené posúdenie rizík schválené; inak briefing nie je možné vydať.
- Vydanie vyžaduje **Menovanie** zaznamenané na serveri (kapitán remorkéra alebo kapitán) pre koordinujúci remorkér. Vydanie vyžaduje aj prístup na čítanie posúdenia rizík a zápis remorkérov na koordinujúcom remorkéri. Potvrdenie je iba pre osoby v zozname účastníkov.

## Krok za krokom
1. V hornom riadku si prečítajte **Stav**, **Revízia**, **Potvrdenie prevzatia**, **Stav rizika** a **Stav plánu**.
2. V **Vstup pripravenosti (neúplný briefing blokuje READY)** uvidíte výsledky a kódy dôvodov odoslané do kontroly pripravenosti.
3. Vyplňte **Podmienky potvrdené na briefingu** (počasie, prúd, viditeľnosť), **Nebezpečenstvá**, **Preventívne bariéry**, **Zmierňujúce bariéry**, **Právomoc zastaviť prácu** a **Núdzové opatrenia**.
4. Zadajte **ID prepojeného posúdenia rizík**; v **Účastníci** použite **Pridať** na zadanie **ID osoby** a **Rola** každého účastníka.
5. Uložte pomocou **Vytvoriť koncept** alebo **Uložiť koncept**; keď je **Chýba pred odoslaním** prázdne, zvoľte **Menovanie** a stlačte **Vydať briefing**.
6. Ako účastník si prečítajte vydanú revíziu a stlačte **Potvrdiť prevzatie**. Ak nesúhlasíte, napíšte **Dôvod** s dĺžkou aspoň 10 znakov a stlačte **Spochybniť**.
7. Ak sa briefing musí zmeniť, napíšte dôvod a použite **Upraviť**. Zoznamy **Potvrdenia prevzatia** a **Revízie** zobrazujú históriu.

## Čo znamenajú stavy
- **Koncept / Vydané:** stav briefingu. Vydané znamená, že sa môžu začať potvrdenia; samo osebe to nie je výsledok pripravenosti.
- **Nevydané / Čaká / Dokončené / Spochybnené:** stav potvrdení účastníkov. Stáva sa **Dokončené** iba vtedy, keď každý účastník potvrdil vydanú revíziu.
- **Schválené a potvrdené / Pozor / Blokované / Vyžaduje posúdenie:** stav prepojeného posúdenia rizík.
- **Vyhovuje / Nevyhovuje / Neznáme:** výsledok kontroly odoslaný do kontroly pripravenosti. Chýbajúci alebo nevydaný briefing a čakajúce potvrdenia sú **Nevyhovuje**, čo blokuje READY.

## Čo nemôžete robiť / upozornenie
- Žiadne rozhodnutie READY sa neprijme pred dokončením rizika a briefingu a zaznamenaním potvrdení účastníkov (13.03 §3).
- Spochybnenie zmení stav potvrdení na **Spochybnené** a odošle do kontroly pripravenosti Nevyhovuje, takže READY aj GO sú blokované. Dôvod sa zobrazí v **Potvrdenia prevzatia**. Potvrdenie nie je možné zmeniť pre tú istú revíziu; na odstránenie spochybnenia sa briefing upraví (nová revízia), znova vydá a každý účastník ho znova potvrdí.
- Ak sa plán alebo posúdenie rizík po briefingu zmení, briefing sa musí znova posúdiť; skoršie potvrdenia sa pre novú revíziu nepočítajú.
- Nikto nepotvrdzuje v mene niekoho iného; každý účastník zaznamenáva vlastné potvrdenie (13.03 §6).
- Offline sa z tejto stránky neodosiela žiadny príkaz. Potvrdenie je možné zaradiť do frontu v **Offline operácie remorkérov**.

## Ak sa niečo pokazí
- **Správa „Chýbajú povinné sekcie.“:** doplňte chýbajúce kódy; skontrolujte, či sú plán a posúdenie rizík schválené.
- **Správa „The record changed“:** pomocou **Načítať znova** skontrolujte aktuálny briefing.
- **Správa „The outcome is unknown“:** **Skúsiť znova** odošle rovnakú požiadavku znova.
- **Bez oprávnenia:** skontrolujte svoju rolu, remorkér a kontext menovania; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).

## Súvisiace stránky
