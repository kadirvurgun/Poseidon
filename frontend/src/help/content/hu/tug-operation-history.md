---
key: tug-operation-history
title: Vontatóműveletek előzményei
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A műveleti előzmények a vontatási munkák, végrehajtások, napi naplók és bizonyítékok **csak olvasható előzményeit** mutatják. Arra a kérdésre válaszol: „Mely munkák milyen eredménnyel zárultak, ellenőrizték-e a végrehajtásokat, van-e bizonyítékrekord, és mely napi naplók tartoznak ezekhez a munkákhoz?” A visszavont és megszakított rekordok láthatók maradnak; semmit nem törölnek és semmit nem címkéznek át.

## Ki használja
- **Vontatási műveleti koordinátor / Parti műveletek:** a befejezett munkák bizonyítéki teljességének, esemény- és előzményrekordjainak követésére (13.03 §7).
- **Vontatóhajó-kapitány:** a saját vontatóhajója korábbi munkáinak és végrehajtási idővonalainak megtekintésére (13.03 §3).

## Mielőtt elkezdené
- Nyissa meg a vontatóhajót a **Vontatóműveletek** részből; kiválasztott vontatóhajó nélkül az oldal ezt mutatja: „A folytatáshoz válasszon vontatóhajót.”
- Vontató-olvasási hozzáférés elegendő; az oldal semmilyen adatot nem módosít.
- Az előzmények a szerverről olvasódnak; offline állapotban nem frissíthetők.

## Lépésről lépésre
1. A **Szűrők** részben szükség szerint állítsa be a **Vontató ID**, a **Kezdete (UTC dátum)**, a **Vége (UTC dátum)** és az **Eredmény** értékét, majd nyomja meg a **Szűrők alkalmazása** gombot. A dátumtartomány legfeljebb 400 nap.
2. A táblázat minden sorában olvassa el a **Munka** (munkaszám, segített hajó, munka típusa, terv- és eligazítási állapot), a **Tevékenység ideje**, az **Eredmény** és a **Bizonyíték** értékét.
3. A **Végrehajtások** részben tekintse meg minden végrehajtás állapotát, ellenőrzési állapotát, időtartamát, kapitányi rekordját és bizonyítékbejegyzéseinek számát; a **Végrehajtási idővonal megnyitása** gombbal léphet a végrehajtási konzolra.
4. A **Napi naplók** részben található hivatkozásokkal nyissa meg a kapcsolódó naplót. A „napló által nem kapcsolt” megjegyzés azt mutatja, hogy a napló nem kapcsolja ezt a végrehajtást.
5. Régebbi rekordokhoz használja a **Továbbiak betöltése** gombot. Az **Olvasva** mutatja, mikor olvasták az adatokat.

## Mit jelentenek az állapotok
- **Nem kezdődött el / Folyamatban / Befejezve (rögzítve) / Megszakítva / Visszavonva / Vegyes:** a munka összesített eredménye. A Befejezve (rögzítve) nem jelenti azt, hogy a munkát ellenőrizték.
- **Ellenőrizve, bizonyíték rögzítve / Ellenőrizve, nincs bizonyíték-bejegyzés / Ellenőrzés elutasítva / Bizonyíték ellenőrizetlen / Ismeretlen:** a bizonyíték és az ellenőrzés állapota.
- **ellenőrzés függőben / ellenőrizve / ellenőrzés elutasítva:** egyetlen végrehajtás ellenőrzési állapota.
- **Nincs:** ehhez a munkához nincs kapcsolt rekord. Ez nem jelenti azt, hogy nem volt szükség rekordra.

## Amit nem tehet meg / figyelem
- Ezen az oldalon semmilyen rekord nem szerkeszthető, nem törölhető és nem ellenőrizhető.
- Nincs csendes törlés: a visszavont munkák és a megszakított vagy elutasított végrehajtások az előzményekben maradnak.
- Befejezve ≠ Ellenőrizve: az ellenőrzés egy másik személy rekordja.
- A megjelenített adatok az **Olvasva** időpontbeli állapotot mutatják; offline állapotban nem frissülnek.

## Ha valami nem sikerül
- **"The filter is not valid" üzenet:** ellenőrizze, hogy a vontató ID UUID-e, és hogy a dátumtartomány nem haladja-e meg a 400 napot.
- **"The history could not be loaded." üzenet:** ellenőrizze a kapcsolatot, és nyomja meg az **Újra** gombot.
- **"You do not have access to this tug scope.":** (nincs hozzáférése ehhez a vontatói hatókörhöz) ellenőrizze szerepkörét és vontatóhajó-kontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **Record cannot be verified:** (a rekord nem ellenőrizhető) ellenőrizze a bizonyítékhivatkozást és annak forrását a végrehajtási idővonalon; forduljon az ellenőrzőhöz (13.03 §10).

## Kapcsolódó oldalak
