---
key: certificate-command-center
title: Centrum osvedčení
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Centrum osvedčení uvádza **vyhlásenia** o osvedčeniach vybraného plavidla s ich deklarovanými kalendárnymi stavmi. Odpovedá na otázku: „Ktorým osvedčeniam na tomto plavidle sa blíži koniec platnosti alebo okno prehliadky a ktoré záznamy ešte čakajú na ľudskú kontrolu?“ Zobrazené dátumy sú vyhlásenia. Stránka nikdy nerozhoduje, že osvedčenie je platné alebo overené (13.03 §1, §8).

## Kto ju používa
- **Kapitán, Hlavný strojník / Technický inšpektor:** sledujú kalendár osvedčení plavidla, zadávajú nové koncepty osvedčení a začínajú prípravu obnovenia.
- **Regulatory / Assurance user:** kontroluje, že vyhlásenia stoja na oficiálnom, verziovanom zdroji, a zaznamenáva tvrdenie prideleného audítora (Auditor) (13.03 §8).
- Používatelia iba s oprávnením na čítanie vidia len zoznam a nemôžu ho meniť.

## Skôr než začnete
- Zobrazenie vyžaduje oprávnenie na čítanie osvedčení. Zadávanie konceptov vyžaduje oprávnenie na zápis osvedčení a rozsah úprav pre dané plavidlo; získavanie návrhov z dokumentu vyžaduje čítanie súborov a extrakciu; zaznamenanie ľudského tvrdenia vyžaduje zápis do adresára, oprávnenie na záznam overenia a platné vymenovanie za **Auditor** pre toto plavidlo a osvedčenie. Chránené vyhlásenia posádky vyžadujú navyše čítanie posádky, čítanie osvedčení posádky, čítanie identity posádky a čítanie súborov.
- Najprv vyberte správne plavidlo: stlačte ho v zozname **Vybrať plavidlo** (13.03 §1).
- Stránka vyžaduje živé online načítanie. V režime offline uvidíte „Current certificate sources unavailable offline or hidden.“ a žiadne offline oprávnenie sa nevyvodzuje.

## Krok za krokom
1. Otvorte plavidlo zo zoznamu **Vybrať plavidlo**.
2. Pomocou **Rozsah osvedčenia** vyberte **Osvedčenia plavidla** alebo, ak máte oprávnenie, **Chránené vyhlásenia posádky**. Zoznam zúžte pomocou **Filter deklarovaného kalendára**.
3. Záznam otvoríte stlačením jeho riadku. Riadky nesú označenie **Neoverené vyhlásenie o osvedčení**.
4. Pre nový záznam stlačte **Nový koncept osvedčenia**. Zo zdrojového dokumentu vyplňte **Názov dokumentu**, **Deklarovaný regulačný základ**, **Deklarovaná vlajka**, okno prehliadky a **Deklarovaná dostupnosť** a potom stlačte **Uložiť neoverený koncept**.
5. Ak chcete získať návrhy z prepojeného dokumentu, stlačte **Získať údaje z prepojeného dokumentu** v časti **Preskúmať fakty dokumentu**. Porovnajte každý návrh so zdrojom, zaškrtnite **Skontroloval som tento fakt podľa zdroja** a **Skontroloval som, že tento dokument patrí k tomuto plavidlu a osvedčeniu** a potom stlačte **Potvrdiť vybrané fakty a uložiť koncept**.
6. Ako pridelený audítor v časti **Záznam ľudského tvrdenia** vyplňte **Vybrať skutočné pridelenie** a **Vybrať zaznamenané tvrdenie**, zadajte dôvod a potom stlačte **Zaznamenať neposúdené tvrdenie**.
7. Obnovenie začnete pomocou **Pripraviť obnovenie** v časti **Práca súvisiaca s osvedčením** na zázname. Prejdete tak do postupu [Obnovenie osvedčenia](/app/renewal-workflow).

## Čo znamenajú stavy
- **Deklarované uplynutie platnosti <90 dní / <60 dní / <30 dní / Deklarovaná platnosť uplynula:** kalendárne pozorovanie podľa deklarovaného dátumu skončenia platnosti. Nie je to rozhodnutie o platnosti.
- **Deklarované inšpekčné okno je otvorené / Mimo lehoty:** poloha vzhľadom na deklarované okno prehliadky.
- **Vyžaduje sa posúdenie kalendára:** dátum chýba, je nejednoznačný alebo protichodný. Záznam nie je pripravený, kým ho osoba neskontroluje podľa zdrojového dokumentu.
- **Neznáme:** hodnota nebola deklarovaná. Neznáma hodnota nikdy neznamená „nechýba“ ani „v súlade“; vyžaduje kontrolu.
- **Nízka istota · posúdiť / Nejednoznačné · skontrolovať / Vyžaduje posúdenie:** návrh z dokumentu, ktorý osoba ešte nepotvrdila. Skóre spoľahlivosti nie je overenie.
- „Iba táto stránka. Kalendárne pozorovania sú oddelené od kvalifikácie; nejde o súhrny pripravenosti celej flotily.“

## Čo nemôžete robiť / upozornenie
- Uloženie konceptu osvedčenie neoveruje. Zápis záznamu, potvrdenie návrhov z dokumentu a tvrdenie audítora sú samostatné kroky; Dokončené ≠ Overené.
- Automatická extrakcia iba navrhuje. Chýbajúce hodnoty zostávajú neznáme a koncept aktualizujú iba fakty, ktoré vyberiete a opravíte.
- Samotná rola administrátora neoprávňuje zaznamenať ľudské tvrdenie; vyžaduje sa platné vymenovanie a zdrojový súbor.
- Uložené osvedčenie sa nikdy potichu nezmení. Každé uloženie vytvorí novú revíziu a staršie revízie zostávajú v časti **Zaznamenaná história revízií** (13.03 §1).
- Stránka neudeľuje žiadne schválenie klasifikačnej spoločnosti, vlajky ani úradu a nerozhoduje o spôsobilosti na plavbu. Toto rozhodnutie patrí oprávneným osobám.

## Ak sa niečo pokazí
- **Zdroj osvedčenia alebo prístup nie je dostupný. Nevyvodzuje sa platný, prázdny ani overený inventár.** Stlačte **Zopakovať načítanie zdroja**. Ak problém pretrváva, skontrolujte svoju rolu, plavidlo a rozsah objektov; v prípade potreby sa obráťte na správcu nájomcu (Tenant Admin) (13.03 §10).
- **Výsledok neznámy.** Ponechajte pôvodný príkaz a odošlite rovnakú požiadavku znova pomocou **Zopakovať presný príkaz osvedčenia**.
- **Aktuálny zdroj sa zmenil.** Vaše lokálne polia zostanú zachované; použite **Späť na záznamy**, znova si prečítajte aktuálny záznam a zmenu urobte znova.
- **Automatické čítanie nie je pre tento dokument dostupné:** zadajte hodnoty ručne zo zdrojového dokumentu.
- Ak záznam nemožno overiť, skontrolujte jeho dôkaz a pôvod zdroja a postúpte ho overovateľovi (13.03 §10).

## Súvisiace stránky
