---
key: towage-plan
title: Vontatási terv
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A vontatási terv egy beküldött vontatási munkához rögzíti a **vontatóhajó-szerepeket és a konfigurációt, az információcserét, a működési határértékeket, a kommunikációt, a vészkioldást, a vészhelyzeti tervet és a visszacsapási (snap-back) zóna ellenőrzéseit**. Arra a kérdésre válaszol: „Hogyan fogják végrehajtani ezt a munkát, milyen határértékeken belül és milyen vészhelyzeti intézkedésekkel?” A terv jóváhagyása a kijelölt Kapitány vagy Vontatóhajó-kapitány emberi rekordja; nem GO-döntés és nem engedély. A határértékek forrásukkal megadott tervbemenetek; a rendszer nem ad alapértelmezett értékeket.

## Ki használja
- **Vontatási műveleti koordinátor / tervező:** a terv előkészítésére és jóváhagyásra küldésére (13.03 §7).
- **Vontatóhajó-kapitány / Kapitány:** a konfiguráció, a kötél, a BP/képesség, a kommunikáció, a határértékek, a vészhelyzeti terv és a munkaleállítási feltételek megerősítésére, valamint a terv jóváhagyására vagy visszaküldésére (13.03 §3).

## Mielőtt elkezdené
- Nyissa meg a vontatóhajót a **Vontatóműveletek** részből, és válassza ki a munkát a **Válasszon egy beküldött vontatási munkát** listából.
- A terv szerkesztéséhez vontató-írási és tervírási hozzáférés szükséges. A tervben szereplő vontatóhajók azok közül választhatók, amelyekhez ehhez a munkához aktuális kiosztási javaslat tartozik.
- A jóváhagyáshoz vagy visszaküldéshez a koordináló vontatóhajóra vonatkozó, szerveren rögzített **Kijelölés** (Vontatóhajó-kapitány vagy Kapitány) szükséges.

## Lépésről lépésre
1. Olvassa el az **Állapot** és a **Revízió** értékét, majd a **Beküldés előtt hiányzik** listát.
2. A **Vontatóhajó-szerepek és konfiguráció** részben minden vontatóhajóhoz használja a **Hozzáadás** gombot; adja meg a **Pozíció**, a **Vontatási mód**, a **Koordináló (vezető) vontatóhajó**, a **Vontatókötél forrása**, a **Csatlakozási pont**, a **Vontatókötél-eszköz azonosítója** és a **Vészkioldó eszköz azonosítója** értékét.
3. Írja meg a **Kapitány / révkalauz / vontatóhajó-kapitány információcsere** szövegét.
4. A **Működési határértékek (tervbemenetek)** részben minden **Paraméter**hez adjon meg egy **Érték** és **A határérték forrása** adatot. Egy új határérték üresen indul.
5. Töltse ki a **Kommunikációs terv** (elsődleges és tartalék csatorna, munkanyelv, kommunikációvesztési eljárás), a **Vészkioldó**, a **Vészhelyzeti terv** és a **Visszacsapási zóna (snap-back) ellenőrzései** részt.
6. Mentsen a **Piszkozat létrehozása** vagy a **Piszkozat mentése** gombbal; ha semmi nem hiányzik, nyomja meg a **Beküldés jóváhagyásra** gombot.
7. A jóváhagyó kiválaszt egy **Kijelölés** értéket, legalább 10 karakteres **Indok** szöveget ír, és megnyomja a **Jóváhagyás** vagy a **Visszaküldés** gombot.
8. Ha egy jóváhagyott tervet módosítani kell, írjon indokot, és használja a **Módosítás** gombot. A **Jóváhagyási rekordok** és a **Revíziók** lista mutatja az előzményeket.

## Mit jelentenek az állapotok
- **Piszkozat:** a terv szerkeszthető.
- **Felülvizsgálat alatt:** a tervet jóváhagyásra küldték; döntésre vár.
- **Visszaküldve:** a jóváhagyó indokkal visszaküldte a tervet; javítsa ki, és küldje be újra.
- **Jóváhagyva:** a Kapitány vagy a Vontatóhajó-kapitány jóváhagyta a terv ezen revízióját. Ez nem GO-döntés; a készenléti kapu és az eligazítás továbbra is szükséges.
- **Ismeretlen:** a mezőhöz nem választottak értéket. A visszacsapási zónákra vonatkozó „Ismeretlen” soha nem helyettesíti a „Nem” értéket.
- **A vontatási igény megváltozott a terv mentése után.:** a terv egy régebbi igényrevízión alapul, és felül kell vizsgálni.

## Amit nem tehet meg / figyelem
- Aki a tervet készítette vagy beküldte, nem hagyhatja jóvá.
- Hiányzó kötelező szakaszokkal rendelkező terv nem küldhető be; hiányos piszkozat menthető.
- A rendszer nem javasol határértéket, csatornát vagy küszöbértéket; minden érték a tervezőtől származik a forrásával együtt.
- A jóváhagyott terv nem engedély az indításra; az indításhoz aktuális eligazítási tudomásulvételek és a Vontatóhajó-kapitány GO rekordja szükséges (13.03 §3).
- Offline állapotban a parancsok ki vannak kapcsolva.

## Ha valami nem sikerül
- **"Required sections are missing." üzenet:** teljesítse a **Beküldés előtt hiányzik** listában szereplő kódokat.
- **"The record changed" üzenet:** az **Újratöltés** gombbal tekintse át az aktuális tervet.
- **"The outcome is unknown" üzenet:** az **Újra** gomb ugyanazt a kérést küldi el újra.
- **No authorization:** (nincs jogosultság) ellenőrizze szerepkörét, vontatóhajó- és kijelölési kontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
