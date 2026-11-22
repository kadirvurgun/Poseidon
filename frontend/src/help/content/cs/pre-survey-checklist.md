---
key: pre-survey-checklist
title: Kontrolní seznam před prohlídkou
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Kontrolní seznam před prohlídkou sleduje přípravné úkoly pro prohlídku osvědčení s odpovědnou osobou, termínem, doklady a nezávislým lidským ověřením. Odpovídá na otázku: „Které přípravné položky pro tuto prohlídku jsou otevřené, které mají doklady a které ověřila jiná osoba?“ Připravenost je poradní a nikdy není schválením prohlídky (13.03 §1, §8).

## Kdo ji používá
- **Kapitán, Vrchní strojník / Technický inspektor:** zahajují kontrolní seznam, odpovídají na položky a připojují doklady.
- **Ověřovatel:** porovnává položky, na které odpověděl někdo jiný, s jejich doklady a ověřuje je nebo zamítá (13.03 §1).
- Uživatelé pouze s oprávněním ke čtení vidí „Pro vaši roli pouze ke čtení.“

## Než začnete
- Zobrazení vyžaduje oprávnění ke čtení osvědčení. Zahájení kontrolního seznamu a odpovídání na něj vyžaduje plánování prohlídek pro dané plavidlo; ověřování vyžaduje posuzování prohlídek.
- Vyberte správné plavidlo pomocí **Vyberte plavidlo** (13.03 §1).
- Interval prohlídky osvědčení by měl být deklarován v [Centru osvědčení](/app/certificate-command-center).
- Stránka vyžaduje živé čtení. V režimu offline uvidíte „Offline: je nutné aktuální online načtení.“

## Krok za krokem
1. Seznamy plavidla najdete v části **Kontrolní seznamy**, nebo v [Plánovači prohlídek](/app/survey-planner) stiskněte u řádku **Otevřít kontrolní seznam** nebo **Zahájit kontrolní seznam**.
2. Pro nový seznam použijte **Vyberte certifikát**, zvolte **Šablona** a stiskněte **Vytvořit kontrolní seznam**. Šablona je výchozí šablona, nikoli schválený katalog.
3. V části **Vstupy plánu prohlídky** vyplňte **Plánovaný začátek**, **Plánovaný konec**, **Plánované místo (text)** a pokud je k dispozici, **Zastávka v přístavu**.
4. V části **Položky** zadejte u každé položky **Odpovědná role**, **Termín**, **Odpověď** a v případě potřeby **Zjištění** a **Komentář**. Doklady propojte pomocí **Přidat důkaz** a pak stiskněte **Uložit**. Každé uložení vytvoří novou revizi.
5. Jako ověřovatel zvolte u položek, na které odpověděl někdo jiný, v části **Rozhodnutí** **Ověřit** nebo **Zamítnout**, zadejte důvod a stiskněte **Zaznamenat ověření**.
6. Sledujte **Připravenost** a **Překážky**.

## Co znamenají stavy
- Odpověď položky: **Otevřeno**, **Připraveno**, **Vyžaduje pozornost**, **Blokováno**, **Nepoužije se**. Položka, jejíž odpověď je neznámá, zůstává **Otevřeno**; nikdy se nepočítá jako „Nepoužije se“ ani „Připraveno“.
- Ověření: **Čeká**, **Ověřeno (člověkem)**, **Zamítnuto**. Zodpovězená položka zůstává ve stavu **Čeká**, dokud není ověřena.
- Připravenost: **Blokováno**, **Neúplné**, **Připraveno, čeká na lidské potvrzení**. I nejlepší stav čeká na lidské potvrzení; systém nikdy neuvádí „prohlídka schválena“.
- **Kritické / Povinné / Volitelná:** důležitost položky. Nevyřešené kritické a povinné položky se počítají proti připravenosti a zobrazují se v části **Překážky**.
- **Plavba: neznámá (chybí modul plaveb):** vhodnost pro plavbu zatím nelze posoudit a zůstává neznámá.

## Co nemůžete dělat / upozornění
- Položku, na kterou jste odpověděli, nemůžete ověřit: „Tuto položku jste vyplnili vy; ověřit ji musí jiná osoba.“
- Odpověď **Připraveno** u položky ji neověřuje; Dokončeno ≠ Ověřeno. Každé rozhodnutí o ověření je vázáno na přesnou odpověď a doklady, které vidíte.
- Připravenost není schválením prohlídky, rezervací ani platností osvědčení. Koordinace s klasifikační společností, vlajkou a úřady je externí a zde se nesimuluje.
- Položky šablony nejsou schváleným katalogem; o tom, zda se pro plavidlo hodí, rozhoduje oprávněná osoba.

## Když se něco pokazí
- **Tento seznam se od otevření změnil. Před úpravou jej znovu načtěte.** Stiskněte **Znovu načíst aktuální**.
- **Výsledek zatím není znám. Opakování odešle stejný požadavek.** Použijte **Zopakovat stejný požadavek**.
- **Nezaznamenané položky:** opravte uvedené položky a uložte znovu.
- **K tomuto zobrazení nemáte přístup.** Zkontrolujte svou roli, plavidlo a rozsah objektů; v případě potřeby se obraťte na Tenant Admin (13.03 §10).
- Pokud položku nelze ověřit, zkontrolujte její doklady a zdroj a předejte ji ověřovateli (13.03 §10).

## Související stránky
