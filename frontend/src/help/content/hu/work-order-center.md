---
key: work-order-center
title: Munkalapok
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## Mire szolgál ez az oldal
A munkalapközpont a kiválasztott hajó **munkalap-piszkozatait és megadott végrehajtási rekordjait** tartalmazza. Arra a kérdésre válaszol: „Milyen munkalapok vannak ezen a hajón, és melyeket adták ki, hajtották végre, melyekhez küldtek be bizonyítékot, és melyeket ellenőrzött egy személy?” Minden mentett revízió megmarad a megváltoztathatatlan előzményekben. Az ellenőrzés és a lezárás nem jelent üzembe való visszahelyezést, engedélyt vagy kockázati jóváhagyást.

## Ki használja
- **Gépüzemvezető / Műszaki felügyelő:** tervezett és nem tervezett munkalapok megnyitására, kiadására, ellenőrzésére és lezárására (13.03 §5).
- **Gépész / személyzet:** a kiosztott munka megkezdésére, valamint leolvasások, alkatrészek, fénykép- és dokumentumbizonyítékok rögzítésére és beküldésére (13.03 §6).

## Mielőtt elkezdené
- Az olvasáshoz `workorder.read`, `equipment.read` és `maintenance.read` szükséges. A piszkozatokhoz `workorder.write`, a kiadáshoz `workorder.release`, a végrehajtáshoz `workorder.execute`, az ellenőrzéshez `workorder.verify`, a lezáráshoz `workorder.close` kell.
- A kiadáshoz, az ellenőrzéshez és a lezáráshoz bejelentkezett emberi felhasználó szükséges.
- Válassza ki a megfelelő hajót a **Hajó vagy vontató kiválasztása** listából.

## Lépésről lépésre
1. Válassza ki a hajót. A **Rögzített munkalapok** részben szűrje ezt az oldalt a **Visszaadott munkalapszámok keresése** mezővel, és nyisson meg egy rekordot.
2. Új munkához nyomja meg az **Új munkalappiszkozat** gombot, töltse ki a mezőket, és nyomja meg az **Értékeletlen piszkozat mentése** gombot. Hiányos piszkozatok is menthetők.
3. A **Végrehajtás és bizonyítékok** részben, ha kiadási jogosultsággal rendelkezik, a **Kötelező bizonyítékok szabályzata** alatt válassza a **Kötelező bizonyítékok meghatározása** lehetőséget, jelölje be a bizonyítéktípusokat, és nyomja meg a **Munkarendelés kiadása** gombot. Ha meghatározás nélkül adja ki, az ellenőrzés blokkolva marad, amíg egy kiadó nem használja a **Kötelező bizonyítékok módosítása** → **Kötelező bizonyítékok mentése** lépést.
4. A végrehajtó megnyomja a **Munka megkezdése** gombot, kitölti a **Leolvasások**, a **Felhasznált alkatrészek**, a **Fénykép- és dokumentumbizonyítékok**, az **Elvégzett munka** és a **Megállapítások** részt, megnyomja a **Bizonyítékok mentése**, majd a **Bizonyítékok beküldése** gombot.
5. Egy jogosult ellenőrző, aki nem hajtotta végre a munkát, áttekinti a bizonyítékokat. Megnyomja az **Ellenőrzés rögzítése** gombot, vagy kiválaszt egy **Visszaküldés oka** értéket, és megnyomja a **Visszaküldés a végrehajtónak** gombot.
6. Egy ellenőrzött munkalapnál egy jogosult személy megnyomja a **Lezárás rögzítése** gombot.
7. A korábbi revíziókat a **Módosíthatatlan munkalapelőzmények** részben tekintheti meg. Ha állásidő-rekordra van szükség, használja a **Kapcsolt állásidő-piszkozat megnyitása** gombot.

## Mit jelentenek az állapotok
- **Nincs kiadva végrehajtásra. / Kiadva / Folyamatban:** a munka még nincs kiadva, ki van adva, vagy végrehajtás alatt áll.
- **Bizonyítékok beküldve:** a végrehajtó megadta a munkát és a bizonyítékokat. Ez nem jelenti azt, hogy ellenőrizték.
- **Ellenőrizve (emberi rögzítés):** egy olyan jogosult ellenőrző rögzítette, aki nem hajtotta végre a munkát. A **Lezárva (emberi rögzítés)** külön lépés.
- **Kötelező bizonyítékok hiányoznak / Megvan, emberi ellenőrzésre vár / Ember által ellenőrizve:** a kötelező bizonyítékok kapujának állapota.
- **Felülvizsgálat szükséges: a szabályzat nincs beállítva:** nem határoztak meg kötelező bizonyítékot. A „nincs beállítva” soha nem jelenti azt, hogy semmi nem kötelező; az ellenőrzés blokkolva marad.
- **Az ellenőrző visszaküldte:** a munka a megjelenített indokkal visszakerült a végrehajtóhoz.

## Amit nem tehet meg / figyelem
- Ha Ön hajtotta végre a munkát, nem ellenőrizheti és nem zárhatja le. Az oldal ezt mondja: „Ön hajtotta végre ezt a munkát, ezért nem ellenőrizheti és nem zárhatja le.”
- A Bizonyítékok beküldve, az Ellenőrizve és a Lezárva külön emberi rekordok. Az oldal soha nem nyilvánítja a berendezést üzemkésznek.
- A „Mentett munkalap forrása” összefoglaló a mentett piszkozatmezőket írja le, és kimondja: „A kiadást, a bizonyítékok rögzítését, a független ellenőrzést és a lezárást a végrehajtási panelen rögzítik. A beküldött bizonyítékot csak független emberi ellenőrzés fogadja el, soha nem az a személy, aki a munkát végezte; nincs külön bizonyíték-elfogadási lépés, és az ellenőrzés nem üzembe való visszahelyezési döntés.” Ezeket a műveleteket a **Végrehajtás és bizonyítékok** részben végzik.
- A leolvasások a megadott formában maradnak meg; nem történik mértékegység-átváltás és nem alkalmaznak küszöbértéket.
- Az offline bizonyítéksor csak jóváhagyott offline szabályzat megléte esetén és a munka folyamatban léte alatt működik. Az ütközések soha nem egyesülnek automatikusan; használja az **Ütközésközpont megnyitása** gombot, és egy személy dönt.

## Ha valami nem sikerül
- **The record changed or this action is not allowed in its current state:** (a rekord megváltozott, vagy ez a művelet a jelenlegi állapotában nem megengedett) az **Újratöltés** gombbal olvassa be az aktuális rekordot, és próbálja újra.
- **Outcome unknown:** (az eredmény ismeretlen) az **Ugyanazon parancs megismétlése** ugyanazt a parancsot küldi el; nem hoz létre új rekordot.
- **Permission or human authority is missing:** (hiányzik a jogosultság vagy az emberi jogkör) ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **The required-evidence gate rejected this action:** (a kötelező bizonyítékok kapuja elutasította ezt a műveletet) teljesítse a hiányzó bizonyítéktípusokat. Ha egy rekord nem ellenőrizhető, vizsgálja meg a bizonyítékot és annak eredetét az ellenőrzővel (13.03 §10).

## Kapcsolódó oldalak
