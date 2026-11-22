---
key: notification-escalation
title: Oznámenia
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka oznámení uvádza pre vybrané plavidlo alebo remorkér **výnimky zo zdrojov nakonfigurované v aplikácii** (splatná údržba, údržba vyžadujúca posúdenie, zmenený alebo nedostupný zdroj údržby). Odpovedá na otázku: „Ktoré oznámenia o údržbe dorazili k mojej role, aké sú závažné, ktoré boli eskalované, pretože ich nikto včas nepotvrdil, a na ktorom zdrojovom zázname sa zakladajú?“ Potvrdenie neoveruje dôkazy ani neschvaľuje operácie.

## Kto ju používa
- **Hlavný strojník / Technický inšpektor:** sledujú termíny údržby a žiadosti o posúdenie a prechádzajú do zdrojového plánu (13.03 §5).
- **Kapitán remorkéra / Kapitán / Posádka:** vidia oznámenia smerované na ich roly a potvrdzujú ich (13.03 §6).

## Skôr než začnete
- Potrebujete prístup na čítanie oznámení, údržby a vybavenia. Potvrdzovanie a ukladanie nastavení vyžadujú samostatné oprávnenia; bez nich sa príslušné tlačidlá nezobrazujú.
- Vyberte oprávnené plavidlo alebo remorkér v poli **Plavidlo oznámenia**.
- V režime offline nemožno odoslať žiadny príkaz potvrdenia ani nastavenia.

## Krok za krokom
1. Vyberte plavidlo v poli **Plavidlo oznámenia**.
2. Prečítajte si riadok zásady: „Configured · unverified source policy · in-app delivery only“.
3. Pri každom oznámení si prečítajte názov, odznak závažnosti (**Informácia**, **Varovanie**, **Kritické**) a odznak stavu; skontrolujte **Zdroj / revízia**, **Termín splatnosti** a **Čas vytvorenia**. **Pravidlo smerovania** zobrazuje kód pravidla.
4. Pomocou **Otvoriť zdrojový plán** prejdete do príslušného plánu údržby.
5. Ak chcete zaznamenať, že ste oznámenie videli, stlačte **Potvrdiť** (iba v stave **Otvorené** alebo **Eskalované**).
6. Ak máte prístup k nastaveniam, upravte **Stlmiť nepovinné poradné oznámenia** a **Tiché hodiny** (začiatok, koniec, časové pásmo IANA) a stlačte **Uložiť poradné nastavenie**.
7. Na ďalšie strany použite **Predchádzajúce oznámenia** / **Ďalšie oznámenia** a na opätovné načítanie **Znova načítať doručené oznámenia**.

## Čo znamenajú stavy
- **Otvorené:** oznámenie čaká na potvrdenie.
- **Prijatie potvrdené:** osoba zaznamenala, že ho videla. Neznamená to, že údržba bola vykonaná alebo dôkaz overený.
- **Eskalované:** oznámenie nebolo včas potvrdené a bolo presmerované na eskalačnú rolu svojho pravidla.
- **Zdroj sa zmenil / Zdroj nedostupný:** podkladový záznam sa zmenil alebo ho nemožno potvrdiť. Zaznamenaný podklad sa uchováva; oznámenie sa tým nevymaže ani znova neaktivuje.
- **Povinná trasa · nemožno stlmiť:** nastavenia nemôžu toto oznámenie stlmiť.
- **Neznáme (Termín splatnosti):** zdroj nemá termín splatnosti. Neznamená to, že nič nie je splatné.

## Čo nemôžete robiť / upozornenie
- Potvrdenie neoveruje dôkazy ani neschvaľuje operácie.
- Povinné a kritické trasy nemožno stlmiť poradnými nastaveniami. Tiché hodiny iba odkladajú voliteľné upozornenia.
- Prázdna stránka neznamená, že je všetko v poriadku. Ak zásada alebo zdroje nie sú dostupné, stránka to uvádza samostatne.
- Neexistuje automatické opakovanie; neistý príkaz sa uchová v tejto karte a zmeny kontextu zostanú zamknuté.

## Ak sa niečo pokazí
- **Správa „Outcome unknown“:** pomocou **Zopakovať presný príkaz oznámenia** znova odošlite uchovaný príkaz.
- **Správa „Notification version changed“:** pomocou **Znova načítať doručené oznámenia** načítajte aktuálny zoznam.
- **Správa „Notification sources unavailable or access denied.“:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Správa „The saved quiet hours could not be applied“:** zobrazujú sa všetky upozornenia; skontrolujte nastavenia tichých hodín.

## Súvisiace stránky
