---
key: tug-operations-offline
title: Offline vontatóműveletek
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Ez az oldal egy vontatási munka **előre letöltött pillanatképét** tárolja az eszközön, és a kapcsolat nélkül készült háromféle rögzítést egy **tartós, titkosított sorba** helyezi: eligazítási tudomásulvételeket, végrehajtási megfigyeléseket és napi naplóbejegyzéseket. Arra a kérdésre válaszol: „Mit rögzíthetek biztonságosan kapcsolat nélkül, mi vár a sorban, és hogyan fogadta a szerver?” A kapitányi indítási engedély, a készenléti GO, a végrehajtás indítása/állapotváltásai, az ellenőrzés és a felülvizsgálatok online emberi rekordok maradnak.

## Ki használja
- **Vontatóhajó-kapitány:** a kritikus munka folytatására gyenge kapcsolat esetén, és a szinkronizálási állapot követésére (13.03 §10).
- **Személyzet:** eligazítási tudomásulvételek és őrszolgálati bejegyzések offline rögzítésére, valamint ütközések feloldására anélkül, hogy feltételeznék, hogy automatikusan a saját rekordjuk érvényesül (13.03 §6).

## Mielőtt elkezdené
- Az oldalt egy vontatási munkából nyissa meg; munka nélkül az oldal ezt mutatja: „Nyissa meg ezt a képernyőt egy vontatási munkából a pillanatkép előzetes letöltéséhez.”
- Az **Offline szabályzat** sornak beállított tárolási szabályzatot kell mutatnia (`CONFIGURED_UNVERIFIED`); szabályzat nélkül a pillanatkép előzetes letöltése és az offline rögzítés nem érhető el.
- A pillanatképet **online állapotban** töltse le előre. Rögzítés előtt ellenőrizze, hogy a pillanatkép bérlete nem járt-e le.

## Lépésről lépésre
1. Online állapotban nyomja meg a **Megbízás-pillanatkép előzetes letöltése** gombot. Megjelenik a „Pillanatkép lezárva ezen az eszközön.” üzenet.
2. A **Megbízás-pillanatkép** részben ellenőrizze a munkaszámot, az állapotát és a **Bérlet lejárata** értékét.
3. Ha egy kiadott eligazítás résztvevője, a **Briefing visszaigazolása** részben válassza a **Tudomásulvétel** vagy a **Kifogás** (legalább 10 karakteres indokkal) lehetőséget, és nyomja meg a **Lezárás és sorba állítás** gombot.
4. Folyamatban lévő végrehajtásnál a **Végrehajtási megfigyelés rögzítése** részben válassza ki a végrehajtást és a megfigyelés típusát (megjegyzés, vontatókötél csatlakoztatva, vontatókötél kioldva), írja meg a megjegyzést, és nyomja meg a **Lezárás és sorba állítás** gombot.
5. Piszkozat vagy visszaküldött napi napló esetén a **Napi naplóbejegyzés** részben válassza ki a naplót, írja meg a **Napi összegzés** szövegét, és nyomja meg a **Lezárás és sorba állítás** gombot.
6. Kövesse az egyes rögzítések állapotát a **Sorban lévő rögzítések** részben. A rögzítések a kapcsolat helyreállásakor sorrendben kerülnek elküldésre; kézi küldéshez használja a **Sorban lévő rögzítések elküldése** gombot.
7. Ha egy rögzítés **Lényeges ütközés** állapotot mutat, olvassa el **A szerveren módosult** részt, kifejezetten válassza a **Szerver rekordjának megtartása** vagy **Az általam rögzített érték alkalmazása az aktuális rekordon** lehetőséget, és nyomja meg az **Ütközés feloldása** gombot.

## Mit jelentenek az állapotok
- **Sorban az eszközön (nincs elküldve):** a rögzítés le van zárva ezen az eszközön; a sorba állított nem azonos az elküldöttel.
- **A szerver elfogadta:** a szerver megkapta a rögzítést. Ez nem jóváhagyás és nem ellenőrzés.
- **Lényeges ütközés:** a szerverrekord a rögzítés után megváltozott; soha nem egyesül automatikusan, és egy személy döntésére vár.
- **Szerverérték megtartva / A szerver elutasította:** a szerver eredménye a rögzítésre.
- **UNKNOWN (offline):** a szabályzat állapota offline nem volt olvasható; a rendszer nem feltételez szabályzatot.

## Amit nem tehet meg / figyelem
- Csak online: kapitányi indítási engedély, készenléti GO, a végrehajtás indítása és állapotváltásai, az eligazítás kiadása, az ellenőrzés, a napi napló beküldése és felülvizsgálata.
- A lényeges ütközések soha nem egyesülnek automatikusan; a feloldás mindig kifejezett emberi döntés (13.03 §1).
- Lejárt pillanatkép-bérlettel nem készíthető új rögzítés; töltsön le előre egy új pillanatképet.
- Újrajátszáskor minden rögzítés egyszer, az eredeti azonosítójával kerül feldolgozásra; ne hozza létre újra ugyanazt a rögzítést.

## Ha valami nem sikerül
- **"The device key for this queue is unavailable." üzenet:** a rögzített rekordok megmaradnak, de nem olvashatók; ne törölje őket, és forduljon a támogatáshoz.
- **"Outcome unknown" üzenet:** ugyanazt a rögzítést a rendszer az eredeti azonosítójával újra megpróbálja; ne hozzon létre újat.
- **"The offline policy does not allow a snapshot." üzenet:** a szabályzat beállításával kapcsolatban forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **Lényeges ütközés:** használja ezen az oldalon az **Ütközés feloldása** űrlapot; az ütközések a **Lényeges ütközések központja** oldalon is megjelennek (13.03 §10).

## Kapcsolódó oldalak
