---
key: defects
title: Poruchy a ochranné opatrenia
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka porúch a dočasných opatrení uchováva **zaznamenané poruchy, obmedzenia a deklarované dočasné opatrenia** vybraného plavidla. Odpovedá na otázku: „Aká kritická je táto porucha, aké ochranné opatrenie alebo prevádzkové obmedzenie je zaznamenané, bolo nápravné opatrenie overené a čo ešte blokuje uzavretie?“ Každý krok je ľudský záznam; stránka neudeľuje žiadne bezpečnostné ani prevádzkové oprávnenie.

## Kto ju používa
- **Hlavný strojník / Technický inšpektor:** zaznamenávajú ochranné opatrenie alebo obmedzenie a vplyv kritickej poruchy na pripravenosť a sledujú nápravné opatrenie (13.03 §5).
- **Kapitán:** vidí vplyv poruchy na prevádzkové obmedzenia (13.03 §2).
- **Strojník / posádka:** deklarujú poruchu, ktorú zistili (13.03 §6).

## Skôr než začnete
- Čítanie vyžaduje `defect.read` a `equipment.read`. Vyhlásenia a záznamy životného cyklu vyžadujú `defect.write`.
- Overenie vyžaduje `defect.verify`, účinnosť `defect.effectiveness` a uzavretie `defect.close`, v každom prípade s prihláseným ľudským používateľom.
- Vyberte správne plavidlo v zozname **Vybrať plavidlo alebo remorkér**.

## Krok za krokom
1. Vyberte plavidlo. V prípade potreby vyfiltrujte **Zaznamenané vyhlásenia** pomocou **Filtrovať vrátené pozorovania uplynutia** a otvorte vyhlásenie.
2. Pre novú poruchu stlačte **Nové vyhlásenie poruchy**, vyplňte polia a stlačte **Uložiť neposúdené vyhlásenie**. Neúplné koncepty možno uložiť.
3. Na uloženom vyhlásení stlačte **Začať životný cyklus** v časti **Životný cyklus poruchy**.
4. Pomocou **Zaznamenať posúdenie** vyberte kritickosť a napíšte vyjadrenie (aspoň 10 znakov).
5. Použite **Zaznamenať opatrenie alebo obmedzenie** a ako **Typ opatrenia** vyberte **Ochranné opatrenie** alebo **Prevádzkové obmedzenie**. Pri dočasnom opatrení zadajte čas uplynutia platnosti (UTC).
6. Pomocou **Zaznamenať nápravné opatrenie** opíšte vykonanú prácu.
7. Oprávnená osoba, ktorá nezaznamenala žiadne nápravné opatrenie, zaznamená **Zaznamenať overenie** a potom **Zaznamenať účinnosť**.
8. Keď nezostávajú žiadne **Prekážky uzavretia**, oprávnená osoba, ktorá tiež nezaznamenala žiadne nápravné opatrenie, zaznamená **Uzavrieť a vrátiť do prevádzky**. Záznamy sa zobrazia v časti **Záznamy životného cyklu**.

## Čo znamenajú stavy
- **Zistenie / Posúdenie / Ochranné opatrenie / Opatrenie / Overenie / Účinnosť:** fáza životného cyklu.
- **Uzavreté (zaznamenané ľudské rozhodnutie):** uzavretie je ľudské rozhodnutie; nie je to zelený výsledok „opravené“.
- **Platí pravidlo kritickosti:** kritickú poruchu nemožno uzavrieť bez ochranného opatrenia.
- **Aktívne / Vypršalo — blokuje / Zrušené / Ukončené uzavretím:** stav ochranného opatrenia alebo obmedzenia. Dočasné opatrenie, ktorého platnosť vypršala, sa musí obnoviť alebo zrušiť.
- **Kritická porucha bez aktívneho opatrenia / Kritickosť neznáma a žiadne opatrenie:** upozornenia. Tieto poruchy sa zobrazujú aj v časti **Upozornenia na stav** v [centre PMS](/app/pms-command-center).
- **Neznáme:** kritickosť nebola posúdená. Neznáme neznamená „Nízka“ ani „Nekritická“.

## Čo nemôžete robiť / upozornenie
- Ak ste zaznamenali nápravné opatrenie, nemôžete zaznamenať overenie, účinnosť ani návrat do prevádzky; tie vyžadujú vlastné oprávnenie a inú osobu.
- Overené, Účinné a Uzavreté sú samostatné ľudské záznamy. Deklarovaná fáza alebo opatrenie neoprávňuje na prevádzku, neoveruje účinnosť ani nepovoľuje uzavretie.
- Ak sa vyhlásenie po overení zmení, uzavretie sa zablokuje a záznam sa vráti na posúdenie.
- Kritickosť zariadenia sa nekopíruje ako posúdenie tejto poruchy.
- V režime offline nie sú aktuálne zdroje porúch dostupné; offline úložisko neexistuje.

## Ak sa niečo pokazí
- **Niekto iný zmenil tento záznam:** pomocou **Znova načítať** si prečítajte aktuálny záznam a zaznamenajte znova.
- **Výsledok uloženia je neznámy:** **Zopakovať rovnaký príkaz** odošle ten istý príkaz.
- **Nemáte serverové oprávnenie na túto akciu alebo záznam:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Prekážky uzavretia:** doplňte chýbajúci záznam v zozname. Ak záznam nemožno overiť, skontrolujte dôkaz s overovateľom (13.03 §10).

## Súvisiace stránky
