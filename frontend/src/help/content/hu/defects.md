---
key: defects
title: Hibák és védőintézkedések
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A hibák és ideiglenes intézkedések oldal a kiválasztott hajó **rögzített hibáit, korlátozásait és bejelentett ideiglenes intézkedéseit** tartalmazza. Arra a kérdésre válaszol: „Mennyire kritikus ez a hiba, milyen védőintézkedést vagy üzemeltetési korlátozást rögzítettek, ellenőrizték-e a javító intézkedést, és mi akadályozza még a lezárást?” Minden lépés emberi feljegyzés; az oldal nem ad biztonsági vagy üzemeltetési felhatalmazást.

## Ki használja
- **Gépüzemvezető / műszaki felügyelő:** egy kritikus hiba védőintézkedésének vagy korlátozásának és készenléti hatásának rögzítésére, valamint a javító intézkedés követésére (13.03 §5).
- **Kapitány:** egy hiba üzemeltetési korlátozási hatásának megtekintésére (13.03 §2).
- **Gépész / személyzet:** egy általuk talált hiba bejelentésére (13.03 §6).

## Mielőtt elkezdené
- Az olvasáshoz `defect.read` és `equipment.read` szükséges. A nyilatkozatokhoz és életciklus-bejegyzésekhez `defect.write` szükséges.
- Az ellenőrzéshez `defect.verify`, a hatékonysághoz `defect.effectiveness`, a lezáráshoz `defect.close` szükséges, mindegyikhez bejelentkezett emberi felhasználóval.
- Válassza ki a megfelelő hajót a **Hajó vagy vontató kiválasztása** listából.

## Lépésről lépésre
1. Válassza ki a hajót. Szükség esetén szűrje a **Rögzített nyilatkozatok** listát a **Visszaadott lejáratmegfigyelések szűrése** segítségével, és nyisson meg egy nyilatkozatot.
2. Új hibához nyomja meg az **Új hibanyilatkozat** gombot, töltse ki a mezőket, és nyomja meg az **Értékeletlen nyilatkozat mentése** gombot. Hiányos piszkozatok is menthetők.
3. Egy mentett nyilatkozaton nyomja meg az **Életciklus indítása** gombot a **Hiba életciklusa** részben.
4. Az **Értékelés rögzítése** segítségével válassza ki a kritikusságot, és írja meg a megállapítást (legalább 10 karakter).
5. Használja a **Védőintézkedés vagy korlátozás rögzítése** lehetőséget, és az **Intézkedés típusa** mezőben válassza a **Védőintézkedés** vagy az **Üzemeltetési korlátozás** értéket. Ideiglenes intézkedésnél adja meg a lejáratot (UTC).
6. A **Javító intézkedés rögzítése** segítségével írja le az elvégzett munkát.
7. Egy felhatalmazott személy, aki nem rögzített javító intézkedést, rögzíti az **Ellenőrzés rögzítése**, majd a **Hatékonyság rögzítése** bejegyzést.
8. Ha a **Lezárási akadályok** listában már nem maradt tétel, egy felhatalmazott személy, aki szintén nem rögzített javító intézkedést, rögzíti a **Lezárás és üzembe visszaállítás** bejegyzést. A bejegyzések az **Életciklus-bejegyzések** alatt jelennek meg.

## Mit jelentenek az állapotok
- **Észlelés / Értékelés / Védőintézkedés / Intézkedés / Ellenőrzés / Hatékonyság:** az életciklus szakasza.
- **Lezárva (rögzített emberi döntés):** a lezárás emberi döntés; nem zöld „javítva” eredmény.
- **Kritikus szabály érvényes:** egy kritikus hiba védőintézkedés nélkül nem zárható le.
- **Aktív / Lejárt — blokkoló / Feloldva / Lezáráskor megszűnt:** egy védőintézkedés vagy korlátozás állapota. A lejárt ideiglenes intézkedést meg kell újítani vagy fel kell oldani.
- **Kritikus hiba aktív védőintézkedés nélkül / Ismeretlen kritikusság, nincs védőintézkedés:** riasztások. Ezek a hibák a [PMS irányítóközpont](/app/pms-command-center) **Állapotriasztások** része alatt is megjelennek.
- **Ismeretlen:** a kritikusságot még nem értékelték. Az ismeretlen nem jelenti azt, hogy „Alacsony” vagy „Nem kritikus”.

## Amit nem tehet meg / figyelem
- Ha Ön rögzítette a javító intézkedést, nem rögzíthet ellenőrzést, hatékonyságot vagy üzembe visszaállítást; ezekhez saját felhatalmazás és másik személy szükséges.
- Az Ellenőrzött, a Hatékony és a Lezárt külön emberi feljegyzések. Egy bejelentett szakasz vagy intézkedés nem engedélyezi az üzemet, nem ellenőrzi a hatékonyságot és nem engedi meg a lezárást.
- Ha a nyilatkozat az ellenőrzés után megváltozik, a lezárás blokkolódik, és a rekord visszakerül felülvizsgálatra.
- Az eszköz kritikussága nem másolódik át ennek a hibának az értékeléseként.
- Offline állapotban az aktuális hibaforrások nem érhetők el; nincs offline tárolás.

## Ha valami nem sikerül
- **Someone else changed this record:** használja az **Újratöltés** gombot az aktuális rekord beolvasásához, és rögzítse újra.
- **The save outcome is unknown:** az **Ugyanazon parancs újrapróbálása** ugyanazt a parancsot küldi el.
- **You do not have the server permission for this action or record:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **Lezárási akadályok:** pótolja a listában hiányzó rekordot. Ha egy rekord nem ellenőrizhető, vizsgálja meg a bizonyítékot az ellenőrzővel (13.03 §10).

## Kapcsolódó oldalak
