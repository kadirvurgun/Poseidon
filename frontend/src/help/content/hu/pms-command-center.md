---
key: pms-command-center
title: PMS irányítóközpont
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A PMS irányítóközpont a kiválasztott hajó karbantartási **kivételeit** gyűjti össze egy képernyőn. Arra a kérdésre válaszol: „Vannak-e ezen a hajón lejárt kritikus munkák, nyitott munkarendelések, hibariasztások vagy kritikus alkatrészhiányok, és miért?” Az oldal tanácsadó jellegű kivételnézet; nem alkalmassági, készenléti vagy biztonsági döntés. A rendben lévő tételeket nem listázza, csak megszámolja.

## Ki használja
- **Gépüzemvezető / műszaki felügyelő:** a lejárt munkák, nyitott munkarendelések, hibariasztások és kritikus alkatrészhiányok követésére (13.03 §5).
- **Kapitány:** a PMS- és hibarekordok üzemeltetési korlátozási hatásának megtekintésére (13.03 §2).

## Mielőtt elkezdené
- Az oldal megtekintéséhez berendezésolvasási jogosultság (`equipment.read`) szükséges.
- Minden rész a saját moduljának olvasási jogosultságától függ. Például hibaolvasási jogosultság nélkül az **Állapotriasztások** rész „Hozzáférés szükséges: nem látható” állapotban marad.
- Először válasszon hajót. Hajó nélkül az oldal csak a **Válasszon hajót.** listát mutatja.

## Lépésről lépésre
1. Válassza ki a hajót a listából. Ha egyik sem tartozik a hatókörébe, az oldal ezt írja: „Nincs hajó a hatókörében.”
2. Olvassa el felül a **Kivételösszesítő** jelvényt és az **Utolsó frissítés** időpontját.
3. Tekintse át a részeket: **Lejárt kritikus munkák**, **Esedékes, lejárt és felülvizsgálandó munkák**, **Nyitott munkarendelések**, **Állapotriasztások** és **Kritikus alkatrészhiányok**.
4. Egy tételen nyissa meg a **Miért** részt a rögzített okkódok és – ha van – a **Megfigyelve** időpont megtekintéséhez.
5. A forráshoz a **Forrásrekord megnyitása** segítségével léphet. Egy hibatétel a [Hibák és ideiglenes intézkedések](/app/defects), egy munkarendelés-tétel a [Munkalapközpont](/app/work-order-center) oldalt nyitja meg.
6. Az adatok ismételt beolvasásához nyomja meg a **Frissítés** gombot.

## Mit jelentenek az állapotok
- **Blokkolva / Felülvizsgálat szükséges / Figyelem:** a kivételösszesítő állapota. A részletek az egyes részek tételeiben találhatók.
- **Nincs nyitott kivétel (tanácsadó):** a beolvasott források nem adtak vissza nyitott kivételt. Ez nem készenléti vagy alkalmassági eredmény.
- **Kritikus / Figyelem / Felülvizsgálat szükséges:** egy tétel súlyossága.
- **A forrás nem érhető el: ismeretlen, nem nulla:** a rész forrása nem olvasható. Az ismeretlen nem jelenti azt, hogy „nulla”, „nincs probléma” vagy „Kész”.
- **Hozzáférés szükséges: nem látható:** nincs jogosultsága ehhez a részhez; sem a tartalom, sem a darabszám nem jelenik meg.
- **Ebből a forrásból nincs kivétel:** a részt beolvasták, és nem adott vissza kivételt. A rendben lévő tételek száma külön jelenik meg.
- **Stale data / Frissesség ismeretlen:** a tétel mögötti adat nem aktuális, vagy a frissessége ismeretlen.

## Amit nem tehet meg / figyelem
- Az oldal egyetlen rekordot sem módosít; csak olvas. A munkarendeléseket, hibákat és alkatrészeket a saját oldalukon kezelik.
- Az oldal soha nem nyilvánít egy hajót „késznek” vagy „biztonságosnak”. Az indulási döntés a kapitányé (13.03 §2).
- Egy hiba csak emberi lezárási rekorddal számít lezártnak, és csak arra a nyilatkozatrevízióra, amelyet lezártak. Ha a nyilatkozat később megváltozik, a tétel visszakerül felülvizsgálatra.
- Az offline állapotban megjelenített adatok régiek lehetnek, és a **Frissítés** nem érhető el.

## Ha valami nem sikerül
- **The record could not be loaded:** ellenőrizze a kapcsolatát, és használja az **Újratöltés** gombot.
- **You do not have the server permission for this action or record:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **Unexpected exception:** nyissa meg a **Miért** részt, és vizsgálja felül a forrásrekordot a felelőssel vagy a szakterületi szakértővel (13.03 §10).
- **Application error:** jelentse a korrelációs azonosítót (Correlation ID), a képernyőt, az időpontot és a lépéseket a támogatásnak (13.03 §10).

## Kapcsolódó oldalak
