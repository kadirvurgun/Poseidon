---
key: industry-assurance
title: Odvetvové zabezpečenie (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Odvetvové zabezpečenie importuje výsledky inšpekcií SIRE 2.0 pre tanker zo zošita, predkladá zmeny na ľudskú kontrolu, prepája zistenia s bezpečnostnými opatreniami a zaznamenáva doklady z terénu. Odpovedá na otázku: „Ktoré zistenia tejto inšpekcie sú otvorené, ktoré majú prepojené opatrenie a kde je dôkaz?“ Importy sú koncepty: každú zmenenú položku prijíma iná osoba. Zistenia zostávajú otvorené a neoverené; táto stránka ich nikdy neuzatvára ani neoveruje. Reťazec je Zistenie → Opatrenie → Overenie a overenie a účinnosť sú samostatné ľudské záznamy na opatrení v časti Bezpečnostné udalosti a takmer nehody (13.03 §4, §8).

## Kto ju používa
- **DPA / HSEQ:** prepája zistenia z inšpekcií s opatreniami a sleduje reťazec Zistenie → Opatrenie → Overenie (13.03 §4).
- **Regulatory / Assurance user:** spravuje mapovania stĺpcov a pripravuje alebo kontroluje importy (13.03 §8).
- **Kapitán a lodný personál:** zaznamenávajú doklady z terénu (fotografie) k zisteniu.

## Skôr než začnete
- Vyberte plavidlo. SIRE 2.0 sa vzťahuje iba na tankery. Ak typ lode nie je deklarovaný v profile plavidla, importy zostávajú v stave **vyžaduje kontrolu**.
- Čítanie vyžaduje oprávnenie na čítanie inšpekcií. Import, prijatie importu, zápis dôkazov, prepájanie zistení a zápis mapovaní sú samostatné oprávnenia. Prepojenie opatrenia vyžaduje aj oprávnenie na čítanie bezpečnostných udalostí; zaznamenanie dôkazu vyžaduje aj čítanie súborov.
- Schéma stĺpcov je syntetická; licencovaný formát OCIMF sa nepoužíva.
- V režime offline sa v tomto zariadení zaraďujú do frontu iba zachytené fotografie; importy a rozhodnutia vyžadujú pripojenie.

## Krok za krokom
1. **Vyberte plavidlo** a skontrolujte riadok **Uplatniteľnosť**.
2. V prípade potreby v časti **Mapovania stĺpcov** zadajte **Kľúč mapovania**, napíšte jeden riadok na stĺpec (hlavička=pole) a stlačte **Uložiť ako novú verziu**.
3. V časti **Pripraviť import** vyberte **Verzia mapovania**, **Referencia inšpekcie**, **Dátum inšpekcie** a **Zošit (.xlsx)** a potom stlačte **Odovzdať na kontrolu**.
4. Iná osoba v časti **Kontrola importu** vyberie pri každej zmenenej položke **Prijať** alebo **Ponechať súčasné**, napíše **Dôvod** s aspoň 10 znakmi a stlačí **Prijať import** alebo **Zamietnuť import**.
5. Otvorte inšpekciu zo zoznamu **Inšpekcie**. Skontrolujte **Položky** a **Zistenia**.
6. Ak chcete prepojiť zistenie s opatrením, zadajte **ID bezpečnostného opatrenia** a stlačte **Prepojiť opatrenie**.
7. Pre doklad z terénu zadajte **Médium**, **ID čistého súboru**, **Revízia súboru** a **Kontrolný súčet súboru (SHA-256)** a stlačte **Zaznamenať doklad z terénu**. V režime offline sa tlačidlo zmení na **Uložiť v tomto zariadení**; po pripojení ho odošlite pomocou **Odoslať teraz**.

## Čo znamenajú stavy
- **Tanker: použiteľné / Neuplatňuje sa / Typ plavidla neznámy: potrebná kontrola:** pri neznámom type lode zostáva výsledok **Vyžaduje kontrolu**; nikdy sa nepredpokladá, že sa neuplatňuje.
- **Čaká na kontrolu / Prijaté / Zamietnuté:** stav importu.
- **Nové / Zmenené / Bez zmeny / Nie je v tomto importe:** položka v porovnaní s predtým prijatým importom. Položka, ktorá nie je v tomto importe, je **Ponechané (nikdy sa nemaže)**.
- **Negatívne pozorovanie / Pozitívne pozorovanie / Bez pozorovania / Neznáme:** pozorovanie pri položke.
- **Otvorené / Neoverené:** zistenia sa tu vždy zobrazujú ako otvorené a neoverené.
- **Žiadne prepojené opatrenie / Opatrenie prepojené:** či je zistenie prepojené s bezpečnostným opatrením. Prepojené opatrenie neznamená, že zistenie je overené alebo že opatrenie je účinné.
- **Zaznamenané online / Zaznamenané offline:** ako bol dôkaz zachytený.

## Čo nemôžete robiť / upozornenie
- Osoba, ktorá import pripravila, ho nemôže kontrolovať: „You staged this import; someone else must review it.“
- Táto stránka neuzatvára ani neoveruje zistenia. Overené nie je Účinné; účinnosť je samostatné ľudské posúdenie na opatrení.
- Video dôkazy nie sú dostupné, kým prevádzkovateľ nepovolí typ skenovaného video súboru.
- Položka, ktorá chýba v neskoršom importe, sa nikdy neodstráni; história sa uchováva.

## Ak sa niečo pokazí
- **Požiadavka bola odmietnutá:** vyriešte dôvody uvedené pod ňou.
- **Záznam sa medzitým zmenil. Načítajte ho znova a skontrolujte.** Stlačte **Znova načítať**.
- **Výsledok je neznámy (spojenie stratené).** Použite **Zopakovať rovnakú požiadavku**.
- **Nemáte oprávnenie čítať záznamy odvetvového zabezpečenia.** alebo **Na to nemáte oprávnenie.** Skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- Ak dôkaz nemožno overiť, skontrolujte ID súboru, revíziu a kontrolný súčet SHA-256; v prípade potreby kontaktujte overovateľa (13.03 §10).

## Súvisiace stránky
