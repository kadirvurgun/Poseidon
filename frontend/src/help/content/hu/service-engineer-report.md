---
key: service-engineer-report
title: Szerviz- és mérnöki jelentések
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Ez az oldal rögzíti és listázza a kiválasztott hajón végzett **szerviz- és mérnöki látogatások jelentéseit**. Arra a kérdésre válaszol: „Ki milyen munkát végzett melyik berendezésen és melyik munkalaphoz, milyen alkatrészeket és méréseket rögzítettek, és ellenőrzött-e a jelentés?” A jelentés egy berendezés-azonosítóhoz, egy munkalaphoz és hibákhoz kapcsolódik, így a munkalap és a berendezés előzményeihez kötődik (13.03 §5). Az ellenőrzött jelentés emberi rekord; nem zár le hibát vagy munkalapot.

## Ki használja
- **Gépüzemvezető / Műszaki felügyelő:** a jelentéseket a berendezés és a munkalap előzményeihez kapcsolja, követi a hiányzó elemeket, és ellenőrzi azokat a jelentéseket, amelyeknek nem ő a szerzője (13.03 §5).
- **Gépész:** szerviz után piszkozatként rögzíti a jelentést, és ellenőrzésre küldi (13.03 §6).

## Mielőtt elkezdené
- Ha nincs kiválasztott hajó, az oldal a **Válasszon hajót.** szöveget mutatja, és felsorolja a hatókörébe tartozó hajókat. Válassza ki a megfelelő hajót.
- A jelentések megtekintéséhez szervizjelentés- és berendezés-olvasási hozzáférés szükséges. Az **Új jelentés** gomb csak írási hozzáféréssel és online állapotban jelenik meg.
- Készítse elő a berendezés, a munkalap, a hiba és a bizonyítékfájl azonosítóit. A munkalap-, hiba- és bizonyítékmezők csak akkor jelennek meg, ha olvashatja ezeket a rekordokat.

## Lépésről lépésre
1. Nyisson meg egy jelentést a **Jelentések** listából, vagy indítson új piszkozatot az **Új jelentés** gombbal.
2. Töltse ki a **Jelentésszám**, a **Szolgáltató**, a **Gépész**, a **Szerviz dátuma**, az **Ok és hatókör** és az **Elvégzett munka** mezőt.
3. Szükség esetén az **Alkatrész hozzáadása** és a **Mérés hozzáadása** gombbal adja meg a felhasznált alkatrészeket és a méréseket. Ha egy mérési eredményt nem ismer, hagyja **Ismeretlen** értéken.
4. A **Kapcsolatok** alatt adja meg a **Berendezés-azonosító**, a **Munkarendelés-azonosító** értékét, valamint a kapcsolt hiba- és bizonyítékfájl-azonosítókat (soronként egyet).
5. Mentsen a **Piszkozat mentése** gombbal. Ha a jelentés **Beküldés előtt hiányzik** listát mutat, először teljesítse ezeket a tételeket.
6. Küldje el a jelentést a **Beküldés ellenőrzésre** gombbal.
7. Egy olyan beküldött jelentésen, amelynek nem Ön a szerzője, írjon legalább 10 karakteres **Indok** szöveget, és használja az **Ellenőrzés rögzítése** vagy a **Visszaküldés a szerzőnek** gombot.
8. Egy ellenőrzött jelentés módosításához írjon indokot, és használja a **Módosítás (új változat)** gombot. Minden változat megmarad a **Megváltoztathatatlan előzmények** részben.

## Mit jelentenek az állapotok
- **Piszkozat / Beküldve / Visszaküldve:** a jelentés életciklusa. A beküldés nem ellenőrzés.
- **Ellenőrizve (rögzített emberi döntés) / Nem szerző ellenőrizte:** egy jogosult személy, aki nem szerzője és nem beküldője a jelentésnek, ellenőrzést rögzített. A Befejezve és az Ellenőrizve nem ugyanaz.
- **Ellenőrzésre vár:** a jelentést beküldték, és még nincs ellenőrizve.
- **Egy korábbi változat ellenőrizve, ez nem:** módosítás után az új változatot újra ellenőrizni kell.
- **aktuális változat / a kapcsolás óta módosult / nem érhető el:** egy kapcsolt berendezés, munkalap vagy hiba állapota a kapcsoláskori változathoz képest.
- **Elfogadott átvitel, nem ellenőrzött bizonyíték:** egy csatolt fájl csak elfogadott átvitel.
- Az **Ismeretlen** mérési eredmény nem jelenti azt, hogy „határértéken belül”.

## Amit nem tehet meg / figyelem
- Nem ellenőrizheti azt a jelentést, amelynek Ön a szerzője vagy beküldője; a szerver elutasítja.
- Az ellenőrzött jelentés nem zár le hibát vagy munkalapot, és nem mutatja, hogy a berendezés biztonságos.
- A bizonyítékok eredetijei nem változnak; a módosítás új változatot nyit, és a régi változat megmarad az előzményekben (13.03 §1).
- Offline állapotban semmi nem kerül rögzítésre. Amikor a kapcsolat helyreáll, az **Ugyanazon parancs újrapróbálása** ugyanazt a parancsot küldi el újra.

## Ha valami nem sikerül
- **Required data or records are missing:** (kötelező adatok vagy rekordok hiányoznak) teljesítse a hiánylistát, és mentsen újra.
- **Someone else changed this record:** (valaki más módosította ezt a rekordot) az **Újratöltés** gombbal nyissa meg a legújabb verziót, majd mentsen újra.
- **You do not have the server permission for this action or record:** (nincs szerveroldali jogosultsága ehhez a művelethez vagy rekordhoz) ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **A report cannot be verified:** (a jelentés nem ellenőrizhető) ellenőrizze a kapcsolt rekordokat és bizonyítékfájlokat; beszéljen egy jogosult ellenőrzővel (13.03 §10).
- **The record could not be loaded:** (a rekord nem tölthető be) küldje el a képernyőt, az időpontot és a lépéseket a támogatásnak (13.03 §10).

## Kapcsolódó oldalak
