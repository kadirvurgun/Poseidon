---
key: tug-equipment-readiness
title: Pripravenosť vlečného vybavenia
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Táto stránka zobrazuje **vyhlásenia o vlečnom vybavení** zvoleného remorkéra (vlečný navijak, vlečný hák, vlečné lano, núdzové uvoľnenie, hasiace zariadenie FiFi) a ich **Overenia hlavného strojníka**. Odpovedá na otázku: „Aké sú SWL, dátumy skúšok, stav a kritickosť každej položky, kto ju overil a čo hovorí kontrola pripravenosti?“ Posúdenie je iba odporúčaním a vstupuje do kontroly pripravenosti; nie je to rozhodnutie GO.

## Kto ju používa
- **Kapitán remorkéra:** na kontrolu navijaka, vlečného lana, vlečného háku / zarážky reťaze, núdzového uvoľnenia, FiFi a dôkazov o kritických poruchách (13.03 §3).
- **Hlavný strojník:** na overenie alebo neoverenie vyhlásení a zaznamenanie vplyvu kritickej poruchy na pripravenosť (13.03 §5).

## Skôr než začnete
- Otvorte remorkér z **Prevádzka remorkérov**; táto obrazovka nepotrebuje žiadne pridelenie.
- Pridanie alebo úprava vyhlásenia vyžaduje prístup na zápis remorkérov a zápis vybavenia.
- Zaznamenanie overenia vyžaduje menovanie **Hlavný strojník** zaznamenané na serveri pre daný remorkér.

## Krok za krokom
1. Celkový odznak si prečítajte v riadku **Hodnotenie**.
2. Na každej karte vybavenia skontrolujte odznak výsledku, kód dôvodu, **SWL (t)**, **Ďalšia skúška**, **Stav** a **Kritické**.
3. Pre nové vyhlásenie stlačte **Pridať vybavenie**; vyplňte **Typ vybavenia**, **Označenie**, **SWL (t)**, **Posledná skúška**, **Ďalšia skúška**, **Stav**, **Kritické**, **ID aktíva PMS**, **ID certifikátu** a **Poznámky**, potom stlačte **Uložiť vyhlásenie**.
4. Ak chcete zmeniť existujúce vyhlásenie, použite **Upraviť** na jeho karte.
5. Ako hlavný strojník zvoľte svoje **Vymenovanie**, napíšte dôvod a pomocou **Overené** alebo **Neoverené** použite **Zaznamenať overenie**. Pre „Neoverené“ je dôvod povinný.
6. Históriu overení s revíziou a časom uvidíte v časti **Overenia hlavného strojníka** a tabuľku **Kontroly zdrojov** v dolnej časti.

## Čo znamenajú stavy
- **Pripravené / Podmienené / Nepripravené / Vyžaduje posúdenie:** odporúčacie posúdenie. Pripravené nie je rozhodnutie GO.
- **Vyhovuje / Nevyhovuje / Neznáme:** výsledok kontroly jednej položky.
- **Prevádzkyschopné / Obmedzené / Zlyhalo / Neznáme:** deklarovaný stav. Ak nie je zvolený žiadny stav, zostáva „Neznáme“; nepočíta sa ako prevádzkyschopné.
- **Nehodnotené:** kritickosť nebola posúdená. Neznamená to „Nie“.
- **Overené / Neoverené:** ľudský záznam hlavného strojníka.
- **nahradené neskoršou revíziou:** overenie patrilo k staršej revízii vyhlásenia; aktuálna revízia vyžaduje nové overenie.

## Čo nemôžete robiť / upozornenie
- Uloženie zaznamená neoverené vyhlásenie. Každá zmena vyžaduje nové overenie hlavným strojníkom.
- Dokončenie záznamu a jeho overenie nie sú tá istá akcia; overenie čaká na oprávnenú osobu (13.03 §1).
- Typ vybavenia nie je možné po uložení zmeniť; pre iný typ pridajte nové vyhlásenie.
- Stránka nikdy nevyhlasuje vlečné vybavenie za bezpečné; rozhodnutie zostáva u kapitána remorkéra (13.03 §3).
- Offline sú príkazy vypnuté.

## Ak sa niečo pokazí
- **Správa „The record changed“:** pomocou **Znova načítať** skontrolujte aktuálne vyhlásenie.
- **Správa „The outcome is uncertain“:** **Skúsiť znova** odošle ten istý príkaz.
- **Záznam nie je možné overiť:** skontrolujte dôkazy a zdroj PMS/certifikátu; kontaktujte overovateľa (13.03 §10).
- **Bez oprávnenia:** skontrolujte svoju rolu, remorkér a kontext menovania; v prípade potreby kontaktujte administrátora nájomcu (13.03 §10).

## Súvisiace stránky
