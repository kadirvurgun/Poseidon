---
key: engine-daily-log
title: Gépüzemi napi napló
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A gépüzemi napi napló a kiválasztott hajó **gépüzemi őrszolgálati feljegyzéseit** piszkozatként tárolja a rögzített forrásverziójukkal együtt. Arra a kérdésre válaszol: „Mit jelentett be a gépüzemi őrszolgálat a gépállapotról és üzemórákról, az üzemanyag- és kenőolaj-fogyasztásról, az édesvízről, a kritikus mérésekről, riasztásokról, hibákról, a bunkerolásról, a fenékvízről és a környezetvédelmi ellenőrzésekről, és rögzített-e a gépüzemvezető felülvizsgálatot pontosan erre a revízióra?” A mentett bejegyzés nem ellenőrzött nyilatkozat. A piszkozatadatokból a rendszer nem von le kibocsátási, üzemanyag-biztosítási vagy készenléti következtetést.

## Ki használja
- **Őrszolgálatos gépész (Crew / OOW / Engineer):** a gépüzemi őrszolgálati feljegyzés valós idejű, időrendi vezetésére (13.03 §6).
- **Gépüzemvezető:** a mentett napló felülvizsgálatára, munkarendelésekhez és hibákhoz kapcsolására, valamint a felülvizsgálat emberi cselekményként való rögzítésére (13.03 §5). Gépüzemi napló felülvizsgálatát csak gépüzemvezetői feladatkörrel rendelkező kinevezés rögzítheti.

## Mielőtt elkezdené
- A felső kontextussávban válassza ki a **megfelelő vállalatot és hajót**.
- Az olvasáshoz `daily.engine.read` szükséges; a mentéshez `daily.engine.write` és szerkesztési jog erre a hajóra. Felülvizsgálat rögzítéséhez `daily.engine.write` és szerkesztési jog szükséges erre a hajóra, továbbá `daily.engine.review`, valamint érvényes gépüzemvezetői kinevezés pontosan erre a rekordra.
- Munkarendelések vagy hibák kapcsolásához ezekhez a rekordokhoz is olvasási hozzáférés szükséges.

## Lépésről lépésre
1. Válassza ki a hajót. A **Rögzített piszkozatok** dátum és revízió szerint listázza a naplókat „Piszkozat · felülvizsgálat szükséges” jelöléssel.
2. Nyomja meg az **Új piszkozat** gombot, vagy nyisson meg egy listázott naplót a **Rögzített piszkozat szerkesztése** nézetben.
3. Töltse ki a **Gépüzemi őrszolgálat kontextusa**, a **Gépállapot és üzemórák**, a **Fogyasztás és termelés**, a **Mérések, riasztások és hibák** és a **Munka- és környezeti kontextus** részt. Minden mezőnél válasszon az **Ismert**, az **Ismeretlen** vagy a **Hiányzik** lehetőség közül.
4. Nyomja meg a **Piszkozat mentése** gombot. Minden mentés új revíziót ad a **Rögzített revízióelőzmények** listához.
5. Egy mentett naplón használja a **Munkautasítások és hibák** részt: válasszon **Rekordtípus** és **Rekord** értéket, majd nyomja meg a **Hivatkozás** gombot. A kapcsolat egy pontos revíziót rögzít; a **Hivatkozás eltávolítása** megszünteti.
6. Szükség esetén nyissa meg az **Üzemanyagrekord megnyitása mentett forrásból** vagy az **Állásidőrekord megnyitása mentett forrásból** lehetőséget. A mennyiségek és mértékegységek nem másolódnak át.
7. A gépüzemvezető megnyitja az **Emberi felülvizsgálat rögzítése** részt, kiválaszt egy **Jogosult személy kinevezése** és egy **Felülvizsgálati bejegyzés** értéket, megírja a **Felülvizsgálat indoklása** szövegét, és megnyomja a **Felülvizsgálati bejegyzés rögzítése** gombot.

## Mit jelentenek az állapotok
- **Nem ellenőrzött nyilatkozat / Ismert / Ismeretlen / Hiányzik:** a bejelentett értékek és adatállapotuk. Az ismeretlen nem jelenti azt, hogy „Nem”, „Kész” vagy „Nem alkalmazható”.
- **Függőben · felülvizsgálat nincs rögzítve:** ehhez a revízióhoz nincs emberi felülvizsgálat.
- **Emberi metaadatok rögzítve · értékeletlen:** ehhez a revízióhoz létezik gépüzemvezetői felülvizsgálati bejegyzés. Ez nem állapít meg ellenőrzést vagy gépüzemvezetői jóváhagyást.
- **Aktuális változat / Újabb változat létezik:** megváltozott-e egy kapcsolt munkarendelés vagy hiba a kapcsolás óta. A kapcsolat csak az eredetet jelzi; az adott rekordot nem ellenőrzi, nem zárja le és nem hagyja jóvá.
- **keresés nem elérhető — ISMERETLEN:** a kapcsolt rekordtípus az Ön hozzáférésével nem olvasható.

## Amit nem tehet meg / figyelem
- A rendszer soha nem vizsgálja felül és nem véglegesíti a naplót. A felülvizsgálat a kinevezett gépüzemvezető emberi cselekménye (13.03 §5).
- Aki a napló bármely revízióját írta, nem vizsgálhatja felül. A szerzőnek a rendszer nem kínál fel kinevezést, és az oldal ezt írja: „Ön írta a napló ezen változatát; az áttekintést független áttekintőnek kell rögzítenie.”; a szerver a feladatkörök szétválasztása miatt szintén elutasítja az ilyen felülvizsgálatot.
- A bejegyzéseket valós időben és időrendben vezesse; ne módosítsa más felülvizsgált rekordját (13.03 §6).
- Egy hiba itteni kapcsolása nem rögzít korlátozást, és nem zárja le a hibát; ezt a hiba vagy a munkarendelés oldalán végezze (13.03 §5).
- A kapcsoláshoz aktív online kapcsolat szükséges; offline semmi nem kapcsolható. Az offline ütközéseket a rendszer soha nem egyesíti automatikusan.

## Ha valami nem sikerül
- **„A bejegyzés megváltozott. Töltse újra a hivatkozások módosítása előtt.”:** töltse újra, majd kapcsolja ismét.
- **„Save outcome unknown”:** használja a **Pontos mentett parancs megismétlése** gombot; ne gépelje be újra.
- **„Emberi ütközésfeloldás szükséges”:** nyissa meg az **Ütközés felülvizsgálata** részt vagy az ütközésközpontot, és döntsön kifejezetten (13.03 §10).
- **No appointment available or access denied:** ellenőrizze szerepkörét és hajókontextusát a bérlői adminisztrátorral (13.03 §10).

## Kapcsolódó oldalak
