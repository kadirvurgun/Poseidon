---
key: pre-survey-checklist
title: Kontrolný zoznam pred prehliadkou
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Kontrolný zoznam pred prehliadkou sleduje prípravné úlohy pre prehliadku osvedčenia so zodpovednou osobou, termínom, dôkazmi a nezávislým ľudským overením. Odpovedá na otázku: „Ktoré prípravné položky pre túto prehliadku sú otvorené, ktoré majú dôkaz a ktoré overila iná osoba?“ Pripravenosť je poradná a nikdy nie je schválením prehliadky (13.03 §1, §8).

## Kto ju používa
- **Kapitán, Hlavný strojník / Technický inšpektor:** začínajú kontrolný zoznam, odpovedajú na položky a pripájajú dôkazy.
- **Overovateľ:** porovnáva položky, na ktoré odpovedal niekto iný, s ich dôkazmi a overuje ich alebo zamieta (13.03 §1).
- Používatelia iba s oprávnením na čítanie vidia „Read-only for your role.“

## Skôr než začnete
- Zobrazenie vyžaduje oprávnenie na čítanie osvedčení. Začatie kontrolného zoznamu a odpovedanie naň vyžaduje plánovanie prehliadok pre dané plavidlo; overovanie vyžaduje posudzovanie prehliadok.
- Vyberte správne plavidlo pomocou **Vyberte plavidlo** (13.03 §1).
- Okno prehliadky osvedčenia by malo byť deklarované v [Centre osvedčení](/app/certificate-command-center).
- Stránka vyžaduje živé načítanie. V režime offline uvidíte „Offline: a current online read is required.“

## Krok za krokom
1. Zoznamy plavidla si pozrite v časti **Kontrolné zoznamy** alebo v [Plánovači prehliadok](/app/survey-planner) stlačte v riadku **Otvoriť kontrolný zoznam** alebo **Začať kontrolný zoznam**.
2. Pre nový zoznam použite **Vyberte certifikát**, vyberte **Šablóna** a stlačte **Vytvoriť kontrolný zoznam**. Šablóna je úvodná šablóna, nie schválený katalóg.
3. V časti **Vstupy plánu prehliadky** vyplňte **Plánovaný začiatok**, **Plánovaný koniec**, **Plánované miesto (text)** a, ak je k dispozícii, **Zastávka v prístave**.
4. V časti **Položky** zadajte pri každej položke **Zodpovedná rola**, **Termín**, **Odpoveď** a v prípade potreby **Zistenie** a **Komentár**. Dôkaz prepojte pomocou **Pridať dôkaz** a potom stlačte **Uložiť**. Každé uloženie vytvorí novú revíziu.
5. Ako overovateľ pri položkách, na ktoré odpovedal niekto iný, vyberte v časti **Rozhodnutie** možnosť **Overiť** alebo **Zamietnuť**, zadajte dôvod a stlačte **Zaznamenať overenie**.
6. Sledujte **Pripravenosť** a **Prekážky**.

## Čo znamenajú stavy
- Odpoveď položky: **Otvorené**, **Pripravené**, **Vyžaduje pozornosť**, **Blokované**, **Neuplatňuje sa**. Položka, ktorej odpoveď je neznáma, zostáva **Otvorené**; nikdy sa nepočíta ako „Neuplatňuje sa“ ani „Pripravené“.
- Overenie: **Čaká**, **Overené (človekom)**, **Zamietnuté**. Zodpovedaná položka zostáva v stave **Čaká**, kým nie je overená.
- Pripravenosť: **Blokované**, **Neúplné**, **Pripravené, čaká na ľudské potvrdenie**. Aj najlepší stav čaká na ľudské potvrdenie; systém nikdy neuvádza „prehliadka schválená“.
- **Kritické / Povinné / Voliteľné:** dôležitosť položky. Nevyriešené kritické a povinné položky sa započítavajú v neprospech pripravenosti a zobrazujú sa v časti **Prekážky**.
- **Plavba: neznáma (chýba modul plavieb):** vhodnosť pre plavbu zatiaľ nemožno posúdiť a zostáva neznáma.

## Čo nemôžete robiť / upozornenie
- Nemôžete overiť položku, na ktorú ste odpovedali: „You answered this item; another person must verify it.“
- Odpoveď **Pripravené** pri položke ju neoveruje; Dokončené ≠ Overené. Každé rozhodnutie o overení je viazané na presnú odpoveď a dôkaz, ktoré vidíte.
- Pripravenosť nie je schválením prehliadky, rezerváciou ani platnosťou osvedčenia. Koordinácia s klasifikačnou spoločnosťou, vlajkou a úradmi je externá a tu sa nesimuluje.
- Položky šablóny nie sú schváleným katalógom; o tom, či sa hodia na plavidlo, rozhoduje oprávnená osoba.

## Ak sa niečo pokazí
- **Tento zoznam sa od otvorenia zmenil. Pred úpravou ho znova načítajte.** Stlačte **Znova načítať aktuálne**.
- **Výsledok zatiaľ nie je známy. Opakovanie odošle rovnakú požiadavku.** Použite **Zopakovať rovnakú požiadavku**.
- **Nezaznamenané položky:** opravte uvedené položky a znova uložte.
- **K tomuto zobrazeniu nemáte prístup.** Skontrolujte svoju rolu, plavidlo a rozsah objektov; v prípade potreby sa obráťte na správcu nájomcu (Tenant Admin) (13.03 §10).
- Ak položku nemožno overiť, skontrolujte jej dôkaz a zdroj a postúpte ju overovateľovi (13.03 §10).

## Súvisiace stránky
