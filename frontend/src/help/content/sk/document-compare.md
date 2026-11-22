---
key: document-compare
title: Porovnanie revízií dokumentu
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka zobrazuje pre vybrané plavidlo **dve revízie riadeného dokumentu vedľa seba, iba na čítanie**. Odpovedá na otázku: „Čím sa tieto dve revízie líšia na úrovni súboru a metadát a aká je história publikácie, overenia a úschovy každej z nich?“ Stránka zobrazuje rozdiely; zmeny neschvaľuje, nepublikuje ani neposudzuje. Revíziu neuvádza do platnosti a nespôsobí, že nahradí skoršiu. Originály dôkazov sa nikdy potichu nemenia; keď nová revízia nahradí skoršiu, stav publikácie skoršej revízie sa zmení na Nahradené a udalosť sa objaví v jej histórii úschovy (13.03 §1).

## Kto ju používa
- **Kapitán / Hlavný strojník / Posádka:** porovnávajú platnú revíziu s predchádzajúcou a zaznamenávajú potvrdenie o prečítaní (13.03 §2, §5, §6).
- **DPA / používateľ regulácie a zabezpečenia (Regulatory / Assurance):** posudzujú históriu revízií a záznamy overenia (13.03 §4, §8).

## Skôr než začnete
- Ak nie je vybrané žiadne plavidlo, stránka uvádza **Vyberte plavidlo vo svojom rozsahu.** a zobrazí zoznam plavidiel. Vyberte správne plavidlo.
- Na zobrazenie stránky potrebujete prístup na čítanie súborov. Obmedzený obsah a dokumenty s obmedzením ISPS vyžadujú aj vlastné oprávnenie.
- Načítanie obsahu a zaznamenanie potvrdenia fungujú iba online.

## Krok za krokom
1. Otvorte dokument zo zoznamu **Riadené dokumenty**. V riadku sa zobrazujú označenia klasifikácie a **Obmedzené ISPS**.
2. V časti **Vyberte dve revízie** vyberte revíziu **Vľavo (základ)** a **Vpravo (posudzovaná)**. Stránka najprv navrhne najnovšiu revíziu vpravo a revíziu pred ňou vľavo.
3. Stlačte **Porovnať**. Pomocou **Vymeniť strany** ich prehodíte.
4. Prečítajte si vyjadrenie o bajtoch a tabuľku **Porovnanie metadát**: **Typ súboru**, **Veľkosť (bajty)**, **SHA-256 obsahu**, **Príjem**, **Publikácia**, **Overenie** a časové polia.
5. Skontrolujte **História schválenia a úschovy** na každej strane.
6. Ak chcete vidieť obsah, stlačte **Načítať obsah (prístup sa zaznamená)**. Obsah sa zobrazí s vodoznakom priradenia.
7. Po **Load content**, ak ste platnú revíziu prečítali, zaškrtnite **Túto revíziu som prečítal(a) a mal(a) prístup k jej obsahu.** a stlačte **Zaznamenať potvrdenie**.

## Čo znamenajú stavy
- **Nepublikované / Účinné / Nahradené:** stav publikácie revízie. Revízia, ktorá nie je účinná, zobrazuje **Nie je v prevádzkovej platnosti**.
- **Neoverené / Overené platné / Overené neplatné / Overenie nepreukázateľné:** záznam overenia revízie. Neoverené neznamená „platné“.
- **Zhodné / Zmenené / Neporovnateľné:** výsledok riadku metadát. Neporovnateľné neznamená „Zhodné“.
- **Bajtovo identické súbory:** iba identické bajty; nie je to posúdenie obsahu.
- **Súbory sa líšia na úrovni bajtov:** ktoré pravidlo sa zmenilo a aký to má význam, musí posúdiť osoba.
- **Obsah je pre vašu rolu obmedzený; len metadáta.** obsah sa pre túto rolu neotvorí.

## Čo nemôžete robiť / upozornenie
- Na tejto stránke nemôžete dokument publikovať, schváliť, uviesť do platnosti ani nahradiť.
- Stránka rozdiely v obsahu automaticky neinterpretuje; o ich význame rozhoduje oprávnená osoba.
- Potvrdenie sa zaznamenáva iba pre platnú revíziu, raz za osobu a revíziu. Nie je to schválenie ani overenie.
- Vytlačená kópia je neriadená. Obsah sa neukladá do vyrovnávacej pamäte ani sa neuchováva offline.
- V režime offline je porovnanie zastarané a nový obsah nemožno načítať.

## Ak sa niečo pokazí
- **Tento dokument vám nie je dostupný:** skontrolujte kontext plavidla a oprávnenie; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Na túto akciu nemáte oprávnenie:** skontrolujte svoju rolu a kontext plavidla (13.03 §10).
- **Záznam sa medzitým zmenil:** pomocou **Znova načítať** otvoríte najnovšiu verziu.
- **Revíziu nemožno overiť:** skontrolujte dôkaz a históriu pôvodu; obráťte sa na oprávneného overovateľa (13.03 §10).
- **Požiadavka zlyhala:** pošlite podpore obrazovku, čas a kroky (13.03 §10).

## Súvisiace stránky
