---
key: tug-operation-history
title: História operácií remorkérov
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
História operácií zobrazuje **históriu iba na čítanie** zákaziek remorkérov, vykonaní, denných denníkov a dôkazov. Odpovedá na otázku: „Ktoré zákazky skončili s akým výsledkom, boli vykonania overené, existuje záznam dôkazu a ktoré denné denníky patria k týmto zákazkám?“ Zrušené a prerušené záznamy zostávajú viditeľné; nič sa nemaže ani nepremenúva.

## Kto ju používa
- **Koordinátor prevádzky remorkérov / pobrežná prevádzka:** na sledovanie úplnosti dôkazov, záznamov udalostí a histórie dokončených zákaziek (13.03 §7).
- **Kapitán remorkéra:** na zobrazenie minulých zákaziek a časových osí vykonania vlastného remorkéra (13.03 §3).

## Skôr než začnete
- Otvorte remorkér z **Prevádzka remorkérov**; bez zvoleného remorkéra stránka zobrazí „Pre pokračovanie vyberte remorkér.“
- Stačí prístup na čítanie remorkérov; stránka nemení žiadne údaje.
- História sa načítava zo servera; offline ju nie je možné obnoviť.

## Krok za krokom
1. V **Filtre** voliteľne nastavte **ID remorkéra**, **Od (dátum UTC)**, **Do (dátum UTC)** a **Výsledok**, potom stlačte **Použiť filtre**. Rozsah dátumov je najviac 400 dní.
2. Na každom riadku tabuľky si prečítajte **Zákazka** (číslo zákazky, asistované plavidlo, typ práce, stav plánu a briefingu), **Čas aktivity**, **Výsledok** a **Dôkazy**.
3. V **Vykonania** uvidíte stav každého vykonania, stav overenia, trvanie, záznam kapitána a počet záznamov dôkazov; pomocou **Otvoriť časovú os vykonania** prejdete do konzoly vykonania.
4. Pomocou odkazov v **Denné záznamy** otvoríte súvisiaci denník. Poznámka „záznamom nepreviazané“ ukazuje, že denník toto vykonanie neprepája.
5. Pre staršie záznamy použite **Načítať ďalšie**. **Načítané o** ukazuje, kedy boli údaje načítané.

## Čo znamenajú stavy
- **Nezačaté / Prebieha / Dokončené (zaznamenané) / Prerušené / Zrušené / Zmiešané:** celkový výsledok zákazky. Dokončené (zaznamenané) neznamená, že zákazka bola overená.
- **Overené, dôkaz zaznamenaný / Overené, bez záznamu dôkazu / Overenie zamietnuté / Dôkaz neoverený / Neznáme:** stav dôkazov a overenia.
- **verifikácia čaká / overené / verifikácia zamietnutá:** stav overenia jedného vykonania.
- **Žiadne:** pre túto zákazku neexistuje žiadny prepojený záznam. Neznamená to, že nebol potrebný žiadny záznam.

## Čo nemôžete robiť / upozornenie
- Na tejto stránke sa žiadny záznam neupravuje, nemaže ani neoveruje.
- Žiadne tiché mazanie: zrušené zákazky a prerušené alebo zamietnuté vykonania zostávajú v histórii.
- Dokončené ≠ Overené: overenie je záznam inej osoby.
- Zobrazené údaje sú stavom k **Načítané o**; offline sa neobnovujú.

## Ak sa niečo pokazí
- **Správa „The filter is not valid“:** skontrolujte, či je ID remorkéra UUID a rozsah dátumov nepresahuje 400 dní.
- **Správa „Históriu sa nepodarilo načítať.“:** skontrolujte pripojenie a stlačte **Skúsiť znova**.
- **„K tomuto rozsahu remorkérov nemáte prístup.“:** skontrolujte svoju rolu a kontext remorkéra; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).
- **Záznam nie je možné overiť:** skontrolujte odkaz na dôkaz a jeho zdroj v časovej osi vykonania; kontaktujte overovateľa (13.03 §10).

## Súvisiace stránky
