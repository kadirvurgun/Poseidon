---
key: renewal-workflow
title: Obnovenie osvedčenia
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Obnovenie osvedčenia zaznamenáva krok za krokom prípravu obnovenia osvedčenia a interný pracovný postup: prípravu, žiadosť, čakanie na úrad, vydanú náhradu, overenie a dokončenie. Odpovedá na otázku: „Kde sa nachádza obnovenie tohto osvedčenia a kto robí ďalší krok?“ Je to iba záznam interného pracovného postupu; nepreukazuje právnu platnosť ani schválenie úradom (13.03 §1, §8).

## Kto ju používa
- **Kapitán, Hlavný strojník / Technický inšpektor:** pripravujú koncept obnovenia a zaznamenávajú požiadavky a žiadosť.
- **Pridelený vlastník obnovenia:** posúva pracovný postup ďalej. Je to rola, ktorú majú členovia s oprávnením vlastníka obnovenia; bez neho pracovný postup nemožno otvoriť ani overiť.
- **Overovateľ a osoba dokončujúca obnovenie:** porovnávajú vydanú náhradu s jej zdrojom a overujú ju a ako iná osoba obnovenie dokončujú (13.03 §1, §8).

## Skôr než začnete
- Zobrazenie vyžaduje oprávnenie na čítanie osvedčení. Príprava konceptu vyžaduje zápis osvedčení a zápis obnovení pre dané plavidlo; overenie vyžaduje overovanie obnovení; dokončenie vyžaduje dokončovanie obnovení.
- Najprv vyberte správne plavidlo v časti **Vybrať plavidlo** (13.03 §1).
- Osvedčenie, ktoré sa má obnoviť, musí mať aktuálnu uloženú revíziu. Nahradené osvedčenie nemožno obnoviť.
- Kroky pracovného postupu vyžadujú živé pripojenie. V režime offline uvidíte „You are offline. Workflow steps are unavailable until the connection returns.“

## Krok za krokom
1. Otvorte **Záznam obnovenia** v časti **Zaznamenané koncepty obnovenia**. Pre nové obnovenie použite **Vybrať osvedčenie na obnovenie**, čím prejdete do [Centra osvedčení](/app/certificate-command-center), a na zázname stlačte **Pripraviť obnovenie**.
2. Vyplňte **Formulár obnovenia osvedčenia**: **Deklarovaný dátum splatnosti obnovenia**, okno prehliadky, **Plánované miesto**, **Pridelená rola vlastníka obnovenia** a **Vyhlásenie o potrebe inšpekcie**, **Vyhlásenie o potrebe dokumentov**, **Vyhlásenie o potrebe činností**. Hodnotu, ktorú nepoznáte, ponechajte ako **Neznáme**.
3. Koncept uložte pomocou **Uložiť neoverený koncept**.
4. V časti **Proces obnovy** vyberte z **Dostupné kroky** krok otvorený pre vašu rolu, napríklad **Potvrdiť splnenie požiadaviek**, **Zaznamenať žiadosť** alebo **Čakať na rozhodnutie úradu**. Vyplňte povinné polia a stlačte **Zaznamenať krok**.
5. Keď úrad vydá nové osvedčenie, použite **Zaznamenať vydanú náhradu** a vyberte ho. Ako dôkaz sa použije jeho vlastný dokument.
6. Overovateľ použije **Overiť náhradu** a porovnaním so zdrojom nastaví **Vydavateľ sa zhoduje**, **Číslo certifikátu sa zhoduje**, **Predmet sa zhoduje**, **Dátumy sa zhodujú** a **Rozsah sa zhoduje**.
7. Iná osoba dokončí obnovenie pomocou **Dokončiť a nahradiť**. Ak úrad žiadosť zamietol, použite **Zaznamenať zamietnutie úradom**; ak sa od prípravy upustí, použite **Zrušiť prípravu**.

## Čo znamenajú stavy
- **Koncept / V príprave:** obnovenie sa pripravuje; formulár možno upravovať.
- **Pripravené na žiadosť / Žiadosť zaznamenaná / Čaká na úrad:** fáza žiadosti. Rozhodnutie úradu prebieha mimo systému.
- **Vydané, neoverené:** náhrada je zaznamenaná, ale osoba ju ešte neporovnala s jej zdrojom.
- **Overené kontrolórom:** overovateľ zaznamenal porovnanie; obnovenie ešte nie je dokončené.
- **Dokončené:** interný pracovný postup je uzavretý a náhrada nahrádza originál. Nie je to rozhodnutie o právnej platnosti osvedčenia.
- **Zrušené / Zamietnuté:** postup sa zastavil.
- Požiadavka môže byť **Splnené / Nevyžaduje sa / Neznáme**. **Neznáme** neznamená „Nevyžaduje sa“; server pri neznámej požiadavke postup neposunie.

## Čo nemôžete robiť / upozornenie
- Osoba, ktorá zaznamenala vydanú náhradu alebo ktorá zadala toto náhradné osvedčenie, ju nemôže overiť. Osoba dokončujúca obnovenie sa musí líšiť od overovateľa, od osoby, ktorá zaznamenala vydanú náhradu, aj od autora náhradného osvedčenia.
- Overenie a dokončenie sú výlučne ľudské rozhodnutia; Dokončené ≠ Overené.
- Plánovacie vyhlásenia nepreukazujú dokončené prehliadky, schválené žiadosti, spôsobilosť na plavbu ani overenie.
- Prepojenia na náhradu v koncepte originál nenahrádzajú. Staré osvedčenie sa uzatvára iba krokom dokončenia so sledovateľným prepojením nahradenia (13.03 §1).
- Každý krok sa zaznamenáva ako nemenná revízia; história zostáva v časti **Zaznamenaná história revízií**.
- Bez výslovne prideleného vlastníka systém nikdy predvolene nepoužije administrátora.

## Ak sa niečo pokazí
- **Pre vašu rolu nie je v tomto stave dostupný žiadny krok.** Ďalší krok patrí inej role; skontrolujte svoje oprávnenie a vymenovanie a v prípade potreby sa obráťte na správcu nájomcu (Tenant Admin) (13.03 §10).
- **Obnovenie sa po načítaní zmenilo.** alebo **Toto nie je aktuálna revízia.** Stlačte **Znova načítať** a posúďte aktuálny stav.
- **Výsledok nie je známy. Opakovanie odošle rovnakú požiadavku znova.** Použite **Opakovať rovnakú požiadavku**.
- **Odmietnuté pre oddelenie povinností: zaznamenali ste skorší krok tejto obnovy, preto tento krok musí vykonať iná osoba.** Záznam sa nezmenil; opätovné načítanie nepomôže. Krok prenechajte inej oprávnenej osobe.
- **Krok nebol zaznamenaný. Skontrolujte údaje a skúste to znova.** Skontrolujte povinné polia a výbery zhody.
- Ak náhradu nemožno overiť, skontrolujte dokument a pôvod zdroja a postúpte ju overovateľovi (13.03 §10).

## Súvisiace stránky
