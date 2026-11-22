---
key: towage-execution
title: Vontatás végrehajtása
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A végrehajtási konzol idővonalként rögzíti egy beküldött vontatási munka **tényleges végrehajtását**: az indítást, a megfigyeléseket, a **Felfüggesztés / Folytatás / Megszakítás / Befejezés** műveleteket és a későbbi ellenőrzést. Arra a kérdésre válaszol: „Mikor indult a művelet, mikor csatlakoztatták és oldották ki a vontatókötelet, milyen megfigyeléseket rögzítettek, milyen állapotban van a művelet, és ellenőrizték-e a rekordot?” Az indítás és az állapotváltások a végrehajtó vontatóhajó Vontatóhajó-kapitányának vagy Kapitányának emberi rekordjai; a konzol soha nem ad ki GO-t.

## Ki használja
- **Vontatóhajó-kapitány / Kapitány:** a művelet indítására és az Indítás/Felfüggesztés/Folytatás/Megszakítás/Befejezés idővonal és bizonyítékai rögzítésére (13.03 §3).
- **Ellenőrző (végrehajtás-ellenőrzési jogkörrel rendelkező személy):** egy befejezett vagy megszakított rekord ellenőrzésére vagy ellenőrzésének elutasítására (13.03 §1).
- **Vontatási műveleti koordinátor / Parti műveletek:** a bizonyítékok teljességének és a befejezett munkák előzményeinek követésére; műveletet nem indíthatnak és nem szakíthatnak meg (13.03 §7).

## Mielőtt elkezdené
- Nyissa meg a vontatóhajót a **Vontatóműveletek** részből, és válassza ki a munkát a **Válasszon egy beküldött vontatási munkát** listából.
- Az indításhoz mindezeknek aktuálisnak kell lenniük: a vontatóhajó aktív kiosztása, egy jóváhagyott **Vontatási terv** (a vontatóhajónak szerepelnie kell a tervben), egy kiadott, minden résztvevő által tudomásul vett **Vontatási kockázat és eligazítás**, valamint egy, az aktuális értékeléshez kötött **GO** rekord a **Műveleti készenléti kapu** oldalon.
- Az indításhoz és az állapotváltásokhoz az adott vontatóhajóra vonatkozó, szerveren rögzített **Kijelölés** (Vontatóhajó-kapitány vagy Kapitány) szükséges.

## Lépésről lépésre
1. Az **Indítási feltétel** részben olvassa el minden vontatóhajóra a **Készenléti döntés** és az **Akadályok** értékét.
2. Ha nincsenek akadályok, a fedélzeten kijelölt személyként olvassa el a megerősítő szöveget, válassza ki a **Kijelölés** értékét, szükség esetén adjon meg egy **Kezdő pozíció** értéket, és nyomja meg a **Művelet indítása** gombot.
3. Amint a végrehajtás megnyílt, kövesse az **Állapot**, az **Ellenőrzés** és a naplómezők értékeit (vontatókötél csatlakoztatva/kioldva, legnagyobb megfigyelt vontatási terhelés, kommunikáció, csörlő, vészkioldási próba, aktív percek).
4. A **Megfigyelés rögzítése** részben válassza ki a megfigyelés típusát (például vontatókötél csatlakoztatva, vontatási terhelés, kommunikációs ellenőrzés, időjárás, csörlőállapot, vészkioldási próba, pozíció, bizonyíték, megjegyzés), adja meg az értékeket, és nyomja meg a **Megfigyelés rögzítése** gombot.
5. A **Vontatóhajó-parancsnoki műveletek** részben válassza ki a **Művelet** értékét (Felfüggesztés, Folytatás, Megszakítás, Befejezés), és adja meg a **Kijelölés** és az **Indok** értékét. A megszakításhoz és a befejezéshez legalább 20 karakteres indok szükséges, és ezek megnyitják a **Záró pozíció** mezőt; záró pozíció nélkül a befejezés nem fogadható el.
6. Ha **A befejezést akadályozó tételek** listában tételek szerepelnek, szüntesse meg őket; ha például a vontatókötél még csatlakoztatva van, először annak kioldását rögzítse.
7. Ellenőrzőként írjon indokot, és nyomja meg a **Bejegyzés ellenőrzése** vagy az **Ellenőrzés elutasítása** gombot.
8. Az **Idővonal** részben sorrendben láthatja az összes eseményt.

## Mit jelentenek az állapotok
- **Aktív / Felfüggesztve / Befejezve / Megszakítva:** a művelet állapota. A Befejezve nem jelenti azt, hogy a rekord ellenőrzött.
- **Függőben / Ellenőrizve / Elutasítva:** az ellenőrzés állapota. Befejezve ≠ Ellenőrizve; az ellenőrzés egy másik személy rekordja.
- **Jó / Megszakadt / Megfelelt / Nem felelt meg / Feltételes / Ismeretlen:** a kommunikációs, csörlő- és vészkioldási megfigyelések eredménye. Az Ismeretlen azt mutatja, hogy semmit nem rögzítettek; soha nem helyettesíti a „Jó” vagy a „Megfelelt” értéket.
- **Biztonsági események — Ismeretlen:** ez a konzol nem számolja a biztonsági eseményeket; „nincs esemény” következtetést nem von le.
- **GO (emberi bejegyzés) / NO GO (emberi bejegyzés):** a Vontatóhajó-kapitány vagy a Kapitány által a készenléti kapun rögzített döntés.

## Amit nem tehet meg / figyelem
- Parti felhasználók, MI és rendszeridentitások nem indíthatnak és nem szakíthatnak meg műveletet. Egy parti kiosztás soha nem írja felül a Vontatóhajó-kapitány fedélzeti biztonsági döntését (13.03 §7).
- Csak a kijelölt Vontatóhajó-kapitány vagy Kapitány függesztheti fel, folytathatja, szakíthatja meg vagy fejezheti be a műveletet. A megszakítás a művelet futása alatt mindig elérhető.
- Aki a műveletet elindította vagy befejezte, nem ellenőrizheti ugyanazt a rekordot; az ellenőrzés a művelet befejezése után történik.
- A napló csak hozzáfűzhető; a rögzített eseményeket soha nem törlik és nem módosítják.
- Offline állapotban semmilyen parancs nem kerül elküldésre. A megfigyelések sorba állíthatók az **Offline vontatóműveletek** oldalon; az indítás, az állapotváltások és az ellenőrzés online marad.

## Ha valami nem sikerül
- **"The server listed missing or blocking items." üzenet:** szüntesse meg az **Akadályok** vagy **A befejezést akadályozó tételek** részben szereplő kódokat (például a terv nincs jóváhagyva, az eligazítás tudomásulvétele függőben, a GO rekord hiányzik vagy nem aktuális).
- **"The record changed or the command conflicts." üzenet:** a **Legfrissebb betöltése** gombbal tekintse át a rekordot.
- **"The outcome is unknown." üzenet:** az **Újra** gomb ugyanazt a parancsot küldi el újra.
- **No authorization:** (nincs jogosultság) ellenőrizze szerepkörét, vontatóhajó- és kijelölési kontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
