---
key: departure-signoff
title: Připravenost k vyplutí a rozhodnutí kapitána
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka zobrazuje **poradní posouzení připravenosti** pro vybranou loď a plánované období vyplutí a zaznamenává rozhodnutí jmenovaného kapitána. Odpovídá na otázku: „Co pro toto období uvádějí zdroje posádky, techniky, zákonných požadavků, nouzové připravenosti a bezpečnostních událostí, a proč?“ Rozhodnutí vyplutí uvolnit, pozdržet nebo zastavit náleží **kapitánovi** (13.03 §2). Systém vyplutí nikdy neuvolňuje; zaznamenané rozhodnutí není povolením k vyplutí.

## Kdo ji používá
- **Kapitán:** posoudí hodnocení a seznam „Proč“ a pak zaznamená Uvolnit, Pozdržet nebo Zamítnout s odůvodněním (13.03 §2).
- **Pobřežní provoz / Technický inspektor:** sledují hodnocení a zaznamenaná rozhodnutí kapitána pouze pro čtení. Pobřežní personál nemůže zaznamenat rozhodnutí místo kapitána.

## Než začnete
- V horním kontextovém pruhu zkontrolujte, že je vybrána **správná společnost**. Stránka uvádí pouze lodě ve vašem rozsahu (13.03 §1).
- K zobrazení stránky potřebujete přístup ke čtení připravenosti k vyplutí.
- Samotné oprávnění rozhodovat k zaznamenání rozhodnutí nestačí: potřebujete také **platné jmenování kapitánem** pro tuto loď. Bez něj stránka uvádí „Nemáte platné jmenování kapitánem této lodi. Rozhodnutí jsou jen pro čtení.“

## Krok za krokem
1. Vyberte loď ze seznamu „Vyberte loď“.
2. V části **Plánované období vyplutí** vyplňte „Datum vyplutí“ a „Konec plavby“ a stiskněte **Vyhodnotit připravenost**.
3. V řádku **Hodnocení** si přečtěte odznak stavu, režim „Aktuální“ nebo „Předpověď“, „Vyhodnoceno“ a „Platné do“.
4. V tabulce kategorií zkontrolujte výsledky Posádka, Technické, Zákonné, Nouzová připravenost a Bezpečnostní události.
5. Přečtěte si seznam **Proč**. Každý řádek ukazuje pozorování, které nevyhovělo, jeho kód důvodu a jeho zdroj. Neočekávaný řádek zkontrolujte na vlastní stránce zdroje.
6. Pokud hodnocení není aktuální a ve stavu Připraveno, přečtěte si **Proč není Uvolnit dostupné**. Uvádí, kdy je období předpovědí, vyjmenovává kategorie, které zatím nevyhovují, s jejich výsledky, a vysvětluje, který záznam by uzavřel mezery ve zdrojích Nouzová připravenost (zatím není připojen žádný zdroj cvičení 61.02) a Zákonné (není definována sada požadovaných osvědčení). Pouze vysvětluje kategorie serveru; bránu nemění.
7. V části **Rozhodnutí kapitána** vidíte dříve zaznamenaná rozhodnutí a jejich platnost.
8. Pokud jste kapitán, v části **Zaznamenat rozhodnutí kapitána** zvolte Uvolnit, Pozdržet nebo Zamítnout, vyberte své jmenování v poli „Jmenování“, napište „Důvod“ (10–2000 znaků) a stiskněte **Zaznamenat**.
9. Pomocí **Obnovit** hodnocení přečtěte znovu.

## Co znamenají stavy
- **Připraveno:** žádný zaznamenaný zdroj nemá pozorování, které nevyhovělo. Jde pouze o informaci; rozhodnutí o vyplutí zůstává na kapitánovi.
- **Podmíněné:** alespoň jeden zdroj hlásí podmínku. Pro toto hodnocení se Uvolnit nenabízí.
- **Nepřipraveno:** alespoň jeden zdroj vrací „Nevyhovuje“. To, že ostatní kategorie vyhovují, to nevyvažuje.
- **Vyžaduje posouzení:** alespoň jeden vstup je „Neznámé“. Oprávněná osoba se na něj musí podívat.
- **Vyhovuje / Podmínka / Nevyhovuje / Neznámé:** výsledek kategorie v tabulce kategorií. **Neznámé** znamená, že zdroj nebylo možné přečíst nebo nemá data; neznamená „Vyhovuje“ ani „Připraveno“. V této verzi není pro kategorii Nouzová připravenost připojen žádný zdroj nouzové připravenosti, takže tento řádek ukazuje Neznámé. Zákonné může zůstat Neznámé, i když je každé zaznamenané osvědčení platné, protože žádný zdroj nedefinuje sadu požadovaných osvědčení pro loď. Zákonné zahrnuje také signály důvěryhodnosti dat (DATA_TRUST_*); bez přístupu ke čtení důvěryhodnosti dat ukazuje Zákonné Neznámé.
- **Předpověď:** období nezahrnuje dnešek. Jde pouze o plánovací předpověď a nelze ji uvolnit.
- **Aktuální / Zastaralé — vstupy se změnily:** platnost zaznamenaného rozhodnutí. Když se vstupy změní, dřívější rozhodnutí se stane zastaralým a nesmaže se.

## Co nemůžete dělat / upozornění
- Systém vyplutí nikdy neuvolňuje a žádný stav nezobrazuje jako povolení k vyplutí. Zaznamenané rozhodnutí je vlastní lidský záznam jmenovaného kapitána.
- Uvolnit lze zaznamenat pouze pro aktuální hodnocení READY; každá kategorie musí vyhovovat. Protože kategorie Nouzová připravenost v této verzi zůstává Neznámé, hodnocení nemůže dosáhnout stavu Připraveno a Uvolnit se nenabízí; **Proč není Uvolnit dostupné** ukazuje proč. I ve stavu Připraveno je rozhodnutí o uvolnění na kapitánovi; systém nic neschvaluje. „Pozdržet a Zamítnout lze vždy zaznamenat s odůvodněním.“
- U každého rozhodnutí je odůvodnění povinné. Zaznamenaná rozhodnutí se nemění; nové rozhodnutí je nový záznam.
- Rozhodnutí nemůžete zaznamenat, dokud je hodnocení zastaralé nebo jste offline; nejprve obnovte.

## Když se něco pokazí
- **Jste offline:** hodnocení může být zastaralé a rozhodnutí je vypnuto. Po návratu online obnovte (13.03 §10).
- **Nemáte přístup k připravenosti k vyplutí pro toto plavidlo:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta.
- **Neočekávaná připravenost:** otevřete seznam „Proč“; pokud zdroj vypadá nesprávně, předejte to vlastníkovi kontroly nebo odbornému expertovi.
- **Výsledek je nejistý:** „Zkusit znovu“ odešle stejný příkaz. Nepište nové rozhodnutí.
- **Historie rozhodnutí se změnila / Hodnocení se změnilo:** použijte „Znovu načíst“, posuďte aktuální hodnocení a pak rozhodněte.
- **Příkaz nebyl přijat:** nahlaste to podpoře se snímkem obrazovky, časem a postupem.

## Související stránky
