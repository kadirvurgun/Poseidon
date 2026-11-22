---
key: drydock-planning
title: Szárazdokk-tervezés
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A szárazdokk-tervezés a kiválasztott hajó **szárazdokk-csomagjait** tartalmazza (projekt, hajógyár, tervezett dátumok és munkaköri tételek). Arra a kérdésre válaszol: „Melyik munkarendeléshez, hibához, bizonyítványhoz vagy szemle-ellenőrzőlistához kapcsolódik az egyes szárazdokk-munkaköri tételek, és naprakészek-e ezek a kapcsolatok?” A munkakör ezekhez a rekordokhoz kapcsolódik (13.03 §5). A **Kapcsolatok készültsége** csak a kapcsolatok teljességét jelzi; nem osztályozótársasági vagy lobogó szerinti szemle eredménye, és nem tengerállósági nyilatkozat.

## Ki használja
- **Műszaki felügyelő:** szárazdokk-csomag előkészítésére, a munkakör rekordokhoz kapcsolására, tervezésére és befejezésére (13.03 §5).
- **Gépüzemvezető:** annak követésére, hogyan kapcsolódik a munkakör a hiba- és munkarendelés-rekordokhoz, és jogosultság esetén egy más által befejezett csomag ellenőrzésére (13.03 §5).

## Mielőtt elkezdené
- Válassza ki a megfelelő hajót. Ha nincs kiválasztva hajó, az oldal ezt írja: **A folytatáshoz válasszon hajót a munkaterületen.**
- A csomagok megtekintéséhez szárazdokk-olvasási hozzáférés szükséges. Létrehozáshoz, szerkesztéshez és állapotváltoztatáshoz írási hozzáférés, ellenőrzéshez ellenőrzési hozzáférés kell.
- Tartsa kéznél a kapcsolandó munkarendelés-, hiba-, bizonyítvány- és szemle-ellenőrzőlista-rekordok azonosítóit és revízióit.

## Lépésről lépésre
1. Nyisson meg egy csomagot a listából, vagy hozzon létre egy üres piszkozatot a **Szárazdokk-csomag létrehozása** gombbal.
2. Töltse ki a **Szárazdokk-projekt**, a **Hajógyár**, a **Tervezett kezdés** és a **Tervezett befejezés** mezőt.
3. Adjon hozzá tételeket a **Munkaköri tétel hozzáadása** gombbal; adja meg a **Munkaköri tétel** típusát, a **Cím**, a **Munkacsomag**, az **Osztályozó társasági követelmény**, a **Jogszabályi követelmény** és a **Prioritás** értékét.
4. Minden tételen használja a **Kapcsolat hozzáadása** lehetőséget, és adja meg a **Rekordtípus**, a **Rekordazonosító** és a **Rögzített változat** értékét.
5. A befejezéshez adjon hozzá bizonyítékhivatkozást a **Befejezés bizonyítéka** alatt a **Hozzáadás** gombbal. Ezután nyomja meg a **Mentés** gombot.
6. Ellenőrizze **A tervezést akadályozó tételek** vagy **A befejezést akadályozó tételek** listát. Az **Állapot módosítása** alatt válasszon egy **Művelet** értéket (**Tervezés**, **Vissza piszkozatba**, **Szárazdokkolás indítása**, **Szárazdokkolás befejezése**, **Csomag törlése**), és erősítse meg. A törléshez megjegyzés szükséges. Csak az aktuális állapotban érvényes műveleteket kínálja fel a rendszer; egy csomag csak Piszkozat vagy Tervezett állapotban törölhető.
7. Egy befejezett csomagon, amelyet nem Ön fejezett be, írjon legalább 10 karakteres **Indok** szöveget, és használja a **Bejegyzés ellenőrzése** vagy az **Ellenőrzés elutasítása** lehetőséget.
8. Az **Idővonal** részben kövesse a tervezett és tényleges dátumokat és minden revíziót.

## Mit jelentenek az állapotok
- **Piszkozat / Tervezett / Folyamatban / Befejezve / Ellenőrizve / Törölve:** a csomag életciklusa. A Befejezve és az Ellenőrizve nem ugyanaz.
- **Kapcsolatok naprakészek / Figyelmet igényel / Felülvizsgálat szükséges / Hiányos:** a kapcsolatok készültsége. Ez akkor sem szemleeredmény, ha a kapcsolatok naprakészek.
- **Kapcsolva / Nincs kapcsolva / Késésben / Felülvizsgálat szükséges:** egy munkaköri tétel kapcsolati állapota (Felülvizsgálat szükséges, ha bármely kapcsolat Elavult vagy Nem elérhető).
- **Aktuális / Elavult / Nem elérhető:** egy kapcsolt rekord a rögzített revíziójához viszonyítva. Vizsgálja felül az elavult kapcsolatot, és az **Újrarögzítés erre:** segítségével rögzítse az új revízióhoz.
- **Ismeretlen / nincs megadva:** nem adtak meg prioritást. Az ismeretlen nem jelenti azt, hogy „Normál” vagy „Alacsony”.

## Amit nem tehet meg / figyelem
- Az oldal nem dönti el, hogy egy szárazdokkolás biztonságos, vagy hogy a hajó tengerálló.
- Nem ellenőrizheti az Ön által befejezett csomagot. Az ellenőrzés elutasítása a csomagot visszaállítja **Folyamatban** állapotba.
- A költség, a pénznem, a szállító és a költségvetés csak külső hivatkozás; itt semmit nem összesítenek, hagynak jóvá vagy rendelnek meg.
- Befejezett, ellenőrzött vagy törölt csomag nem szerkeszthető.
- Offline állapotban a parancsok nem kerülnek elküldésre.

## Ha valami nem sikerül
- **The server listed missing or blocking items:** oldja fel az akadálylistát, és próbálja újra.
- **The record changed or the command conflicts:** használja a **Legfrissebb betöltése** gombot, és vizsgálja felül.
- **The outcome is unknown:** az **Újra** pontosan ugyanazt a parancsot küldi el ismét.
- **You do not have access to this record:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **Ezeket a kapcsolt bejegyzéstípusokat nem olvashatja:** ellenőrizze a jogosultságot a rekord felelősével vagy a bérlői adminisztrátorral.

## Kapcsolódó oldalak
