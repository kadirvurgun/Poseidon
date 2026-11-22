---
key: renewal-workflow
title: Bizonyítvány megújítása
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Bizonyítvány megújítása oldal lépésről lépésre rögzíti egy bizonyítvány megújításának előkészítését és belső folyamatát: előkészítés, kérelem, a hatóságra várás, a kiadott helyettesítő, ellenőrzés és befejezés. Arra a kérdésre válaszol: „Hol tart ennek a bizonyítványnak a megújítása, és ki teszi meg a következő lépést?” Ez kizárólag belső folyamatfeljegyzés; nem állapít meg jogi érvényességet vagy hatósági jóváhagyást (13.03 §1, §8).

## Ki használja
- **Kapitány, gépüzemvezető / műszaki felügyelő:** előkészíti a megújítási piszkozatot, és rögzíti a követelményeket és a kérelmet.
- **Kijelölt megújítási felelős:** viszi előre a folyamatot. Ez a megújításfelelősi jogosultsággal rendelkező tagok által betöltött szerepkör; ilyen nélkül a folyamat nem nyitható meg és nem érvényesíthető.
- **Ellenőrző és lezáró:** összeveti a kiadott helyettesítőt a forrásával és ellenőrzi, valamint – egy másik személyként – befejezi a megújítást (13.03 §1, §8).

## Mielőtt elkezdené
- A megtekintéshez bizonyítványolvasási jogosultság szükséges. Piszkozat előkészítéséhez bizonyítványírási és megújításírási jogosultság kell az adott hajóra; az ellenőrzéshez megújítás-ellenőrzési, a befejezéshez megújítás-befejezési jogosultság.
- Először válassza ki a megfelelő hajót a **Hajó kiválasztása** listában (13.03 §1).
- A megújítandó bizonyítványnak rendelkeznie kell aktuális mentett revízióval. Felváltott bizonyítvány nem újítható meg.
- A folyamatlépésekhez élő kapcsolat szükséges. Offline állapotban a „Nincs kapcsolat. A lépések a kapcsolat visszatéréséig nem érhetők el.” üzenet jelenik meg.

## Lépésről lépésre
1. Nyisson meg egy **Megújítási rekord** elemet a **Rögzített megújítási piszkozatok** közül. Új megújításhoz a **Megújítandó bizonyítvány kiválasztása** segítségével lépjen a [Bizonyítványközpont](/app/certificate-command-center) oldalra, és a rekordon nyomja meg a **Megújítás előkészítése** gombot.
2. Töltse ki a **Bizonyítványmegújítás űrlapja** részt: **Bejelentett megújítási esedékesség**, a szemleablak, **Tervezett helyszín**, **Hozzárendelt megújítási felelősi szerep**, valamint **Nyilatkozat a szemle szükségességéről**, **Nyilatkozat a dokumentumok szükségességéről**, **Nyilatkozat az intézkedések szükségességéről**. Az Ön által nem ismert értéket hagyja **Ismeretlen** állapotban.
3. Mentse a piszkozatot a **Nem ellenőrzött piszkozat mentése** gombbal.
4. A **Megújítási folyamat** részben válassza ki az **Elérhető lépések** közül a szerepköre számára nyitott lépést, például **Követelmények teljesülésének megerősítése**, **Kérelem rögzítése** vagy **Hatósági döntésre várás**. Töltse ki a kötelező mezőket, és nyomja meg a **Lépés rögzítése** gombot.
5. Amikor a hatóság kiadja az új bizonyítványt, használja a **Kiadott helyettesítő rögzítése** lehetőséget, és válassza ki. Bizonyítékként a saját dokumentuma szolgál.
6. Az ellenőrző a **Helyettesítő ellenőrzése** lehetőséget használja, és a forrással összevetve beállítja **A kiállító egyezik**, **A tanúsítványszám egyezik**, **A tárgy egyezik**, **A dátumok egyeznek** és **A hatály egyezik** értéket.
7. Egy másik személy a **Lezárás és felváltás** segítségével fejezi be a megújítást. Ha a hatóság elutasította, használja a **Hatósági elutasítás rögzítése** lehetőséget; ha az előkészítést feladják, használja az **Előkészítés visszavonása** lehetőséget.

## Mit jelentenek az állapotok
- **Piszkozat / Előkészítés alatt:** a megújítás előkészítés alatt áll; az űrlap szerkeszthető.
- **Kérelemre kész / Kérelem rögzítve / Hatóságra vár:** a kérelmezési szakasz. A hatósági döntés a rendszeren kívül születik.
- **Kiadva, nem ellenőrzött:** a helyettesítőt rögzítették, de egy személy még nem vetette össze a forrásával.
- **Ellenőr által igazolva:** az ellenőrző rögzítette az összevetést; a megújítás még nincs befejezve.
- **Befejezve:** a belső folyamat lezárult, és a helyettesítő felváltja az eredetit. Ez nem döntés a bizonyítvány jogi érvényességéről.
- **Visszavonva / Elutasítva:** a folyamat leállt.
- Egy követelmény lehet **Teljesítve / Nem szükséges / Ismeretlen**. Az **Ismeretlen** nem jelenti azt, hogy „Nem szükséges”; a szerver ismeretlen követelmény mellett nem viszi tovább a folyamatot.

## Amit nem tehet meg / figyelem
- Aki a kiadott helyettesítőt rögzítette, vagy aki azt a helyettesítő bizonyítványt rögzítette, nem ellenőrizheti. A lezárónak különböznie kell az ellenőrzőtől, a kiadott helyettesítőt rögzítő személytől és a helyettesítő bizonyítvány szerzőjétől.
- Az ellenőrzés és a befejezés kizárólag emberi döntés; Befejezett ≠ Ellenőrzött.
- A tervezési nyilatkozatok nem állapítanak meg elvégzett szemlét, jóváhagyott kérelmet, útra való alkalmasságot vagy ellenőrzést.
- A helyettesítőre mutató piszkozat-kapcsolatok nem váltják fel az eredetit. A régi bizonyítvány csak a befejezési lépéssel, nyomon követhető felváltási kapcsolattal zárul le (13.03 §1).
- Minden lépés módosíthatatlan revízióként kerül rögzítésre; az előzmények a **Rögzített revízióelőzmények** között maradnak.
- Kifejezetten kijelölt felelős hiányában a rendszer soha nem rendel alapértelmezésként adminisztrátort.

## Ha valami nem sikerül
- **Ebben az állapotban nincs elérhető lépés a szerepköréhez.** A következő lépés egy másik szerepkörhöz tartozik; ellenőrizze jogosultságát és kinevezését, és szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **The renewal changed after it was loaded.** vagy **Ez nem az aktuális revízió.** Nyomja meg az **Újratöltés** gombot, és vizsgálja felül az aktuális állapotot.
- **Az eredmény ismeretlen. Az újrapróbálás ugyanazt a kérést küldi el újra.** Használja az **Ugyanazon kérés ismétlése** lehetőséget.
- **Elutasítva a feladatkörök szétválasztása miatt: ezt a lépést másik személynek kell elvégeznie. Ön rögzítette, készítette vagy ellenőrizte a megújítás egy korábbi lépését, vagy még nincs rögzítve másik személy általi ellenőrzés.** A rekord nem változott; az újratöltés nem segít. Hagyja a lépést egy másik felhatalmazott személyre.
- **A lépés nem rögzült. Ellenőrizze az adatokat, és próbálja újra.** Ellenőrizze a kötelező mezőket és az egyezési beállításokat.
- Ha a helyettesítő nem ellenőrizhető, vizsgálja meg a dokumentumot és a forrás eredetét, és utalja az ellenőrzőhöz (13.03 §10).

## Kapcsolódó oldalak
