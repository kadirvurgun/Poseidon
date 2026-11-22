---
key: service-engineer-report
title: Servisné a technické správy
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka zaznamenáva a uvádza **správy o návštevách servisu a technikov** na zvolenej lodi. Odpovedá na otázku: „Kto vykonal akú prácu na ktorom zariadení a pre ktorý pracovný príkaz, ktoré diely a merania boli zaznamenané a je správa overená?“ Správa je prepojená s ID zariadenia, pracovným príkazom a poruchami, takže je naviazaná na históriu pracovného príkazu a zariadenia (13.03 §5). Overená správa je ľudský záznam; neuzatvára poruchu ani pracovný príkaz.

## Kto ju používa
- **Hlavný strojník / technický inšpektor:** na prepojenie správ s históriou zariadenia a pracovných príkazov, sledovanie toho, čo chýba, a overovanie správ, ktorých nie sú autormi (13.03 §5).
- **Strojník:** na zadanie správy ako konceptu po servise a jej odoslanie na overenie (13.03 §6).

## Skôr než začnete
- Ak nie je zvolená žiadna loď, stránka zobrazí **Vyberte plavidlo.** a uvedie lode vo vašom rozsahu. Zvoľte správnu loď.
- Na zobrazenie správ potrebujete prístup na čítanie servisných správ a zariadení. Tlačidlo **Nová správa** sa zobrazuje iba s prístupom na zápis a pri online pripojení.
- Majte pripravené ID zariadenia, pracovného príkazu, poruchy a súborov dokladov. Polia pracovného príkazu, poruchy a dokladov sa zobrazujú iba vtedy, ak tieto záznamy môžete čítať.

## Krok za krokom
1. Otvorte správu zo zoznamu **Správy** alebo začnite nový koncept pomocou **Nová správa**.
2. Vyplňte **Č. správy**, **Poskytovateľ služby**, **Strojník**, **Dátum servisu**, **Dôvod a rozsah** a **Vykonaná práca**.
3. V prípade potreby použite **Pridať diel** a **Pridať meranie** na zadanie použitých dielov a meraní. Ak výsledok merania nepoznáte, ponechajte ho ako **Neznáme**.
4. V časti **Väzby** zadajte **ID zariadenia**, **ID pracovného príkazu** a ID prepojených porúch a súborov dokladov (jedno na riadok).
5. Uložte pomocou **Uložiť koncept**. Ak správa zobrazuje zoznam **Chýba pred odoslaním**, najprv doplňte tieto položky.
6. Správu odošlite pomocou **Odoslať na overenie**.
7. Na odoslanej správe, ktorej nie ste autorom, napíšte **Dôvod** s dĺžkou aspoň 10 znakov a použite **Zaznamenať overenie** alebo **Vrátiť autorovi**.
8. Ak chcete zmeniť overenú správu, napíšte dôvod a použite **Opraviť (nová revízia)**. Každá revízia zostáva v **Nemenná história**.

## Čo znamenajú stavy
- **Koncept / Odoslané / Vrátené:** životný cyklus správy. Odoslanie nie je overenie.
- **Overené (zaznamenané ľudské rozhodnutie) / Overené neautorom:** oprávnená osoba, ktorá správu nevytvorila ani neodoslala, zaznamenala overenie. Dokončené a Overené nie sú to isté.
- **Čaká na overenie:** správa bola odoslaná a ešte nie je overená.
- **Overená skoršia revízia, táto nie:** po oprave vyžaduje nová revízia opäť overenie.
- **aktuálna revízia / zmenené od prepojenia / nedostupné:** stav prepojeného zariadenia, pracovného príkazu alebo poruchy v porovnaní s revíziou, ktorá bola prepojená.
- **Prijatý prenos, nie overený dôkaz:** priložený súbor je iba prijatý prenos.
- Výsledok merania **Neznáme** neznamená „V medziach“.

## Čo nemôžete robiť / upozornenie
- Nemôžete overiť správu, ktorú ste vytvorili alebo odoslali; server to odmietne.
- Overená správa neuzatvára poruchu ani pracovný príkaz a neukazuje, že zariadenie je bezpečné.
- Originály dokladov sa nemenia; oprava otvorí novú revíziu a stará revízia zostáva v histórii (13.03 §1).
- Offline sa nič nezaznamenáva. Keď sa pripojenie obnoví, **Zopakovať rovnaký príkaz** odošle ten istý príkaz znova.

## Ak sa niečo pokazí
- **Required data or records are missing:** doplňte zoznam chýbajúcich položiek a uložte znova.
- **Someone else changed this record:** pomocou **Znova načítať** otvorte najnovšiu verziu a potom uložte znova.
- **You do not have the server permission for this action or record:** skontrolujte svoju rolu a kontext lode; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).
- **A report cannot be verified:** skontrolujte prepojené záznamy a súbory dokladov; obráťte sa na oprávneného overovateľa (13.03 §10).
- **The record could not be loaded:** pošlite podpore obrazovku, čas a kroky (13.03 §10).

## Súvisiace stránky
