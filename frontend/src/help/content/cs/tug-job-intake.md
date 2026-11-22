---
key: tug-job-intake
title: Příjem zakázky
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Příjem zakázky strukturuje nový **požadavek na zakázku remorkéru jako koncept** ve zvoleném kontextu remorkéru. Odpovídá na otázku: „Jaké je pro tuto zakázku obsluhované plavidlo, typ zakázky, časové okno, místo a požadované zdroje (počet remorkérů, tah na kůlu)?“ Uložený požadavek je prohlášení ve stavu konceptu; není to provozní přidělení, způsobilost, povolení ani bezpečnostní schválení.

## Kdo ji používá
- **Koordinátor provozu remorkérů / pobřežní provoz:** strukturuje požadavek a zadává obsluhované plavidlo, typ zakázky, časové okno a požadovanou schopnost (13.03 §7).

## Než začnete
- V horní kontextové liště zvolte **správnou společnost a remorkér**.
- Potřebujete přístup k zápisu remorkéru. Bez něj se formulář neotevře a zobrazí se „Přístup není k dispozici.“
- Neúplné koncepty lze uložit. Povinná provozní data a oprávněné posouzení musí být vyřešeny před odesláním.

## Krok za krokem
1. V **Požadavek a obsluhované plavidlo** vyplňte **Zdroj požadavku**, **Reference zákazníka**, **Obsluhované plavidlo**, **IMO obsluhovaného plavidla**, **Typ zakázky**, **Priorita** a **Vlastník požadavku**. Seznamy bez výběru zůstávají „Neznámé“.
2. V **Požadovaný čas a místo** zadejte **Požadovaný začátek (UTC)**, **Požadovaný konec (UTC, podmíněný)**, **Oblast provozu**, **Místo převzetí** a **Cílové místo**. Časy jsou v UTC.
3. V **Potřeby zdrojů a poznámky** zadejte **Požadovaný BP (tuny, přesné desetinné číslo)**, **Požadovaný počet remorkérů**, **Zvláštní požadavky** a **Poznámky**.
4. Stiskněte **Uložit koncept**. Při úspěchu se otevře detail zakázky s „Koncept uložen. Provozní schválení zůstává nedostupné.“
5. Chcete-li své úpravy zahodit, použijte **Zahodit změny**.
6. V detailu zakázky použijte **Odeslat požadavek** k zaznamenání požadavku pro plánování. Pokud povinná data chybí, odeslání je blokováno a chybějící pole jsou uvedena.

## Co znamenají stavy
- **Koncept:** požadavek je uložen a lze jej upravovat; dosud nebyl odeslán.
- **Odesláno:** požadavek je zaznamenán pro plánování. Není to schválení, přidělení ani rozhodnutí o připravenosti.
- **Zrušeno:** požadavek je uzavřen; nelze jej znovu zahájit.
- **Neznámé:** pro pole nebyla zvolena žádná hodnota. Neznámé není výchozí hodnota a nikdy nezastupuje „Ne“ ani „Nepoužitelné“.
- **Zkontrolujte tuto hodnotu.:** server toto pole odmítl; opravte jej a uložte znovu.

## Co nemůžete dělat / upozornění
- Tato stránka nepřiděluje žádný remorkér a nikdy neprohlašuje zakázku za připravenou. Návrh přidělení je samostatný krok a nikdy nepřevažuje nad bezpečnostním rozhodnutím Kapitána remorkéru na palubě (13.03 §7).
- Požadavek nelze odeslat, dokud chybí povinná data; neúplný koncept lze přesto uložit.
- Pole odeslaného požadavku nelze z tohoto formuláře změnit; zrušený požadavek nelze znovu zahájit, vytvořte místo toho nový.
- Offline se koncept uchovává pouze na této stránce a nelze jej uložit.

## Když se něco pokazí
- **Zpráva „Save outcome uncertain“:** přesný pokus zůstává zachován; po opětovném připojení použijte **Zkusit znovu** k odeslání stejného příkazu. Zahození místních vstupů nezruší příkaz, který již byl odeslán.
- **Zpráva „Conflict“:** váš koncept zůstává zachován; před novým pokusem jej zahoďte a znovu načtěte.
- **Zkontrolovat pole:** opravte označená pole.
- **Access is not available:** zkontrolujte svou roli a kontext remorkéru; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).

## Související stránky
