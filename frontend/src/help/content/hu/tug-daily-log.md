---
key: tug-daily-log
title: Vontatóhajó napi naplója
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A vontatóhajó napi naplója egy adott dátumra és őrszolgálatra rögzíti a **munkákat, a motorok üzemóra-számlálóit, az üzemanyagot és a hibákat**, és **egyeztetés** céljából összeveti őket a nap vontatási végrehajtásaival. Arra a kérdésre válaszol: „Hány munkát fejeztek be ezen a napon/őrszolgálatban, egyeznek-e az üzemórák a végrehajtási időkkel, mennyi üzemanyagot használtak fel, és milyen hibákat jelentettek?” Az egyeztetés tanácsadó jellegű; a kapitányi ellenőrzés külön emberi rekord.

## Ki használja
- **Személyzet / őrszolgálatot ellátó:** az őrszolgálati rekord valós idejű és rendezett vezetésére (13.03 §6).
- **Vontatóhajó-kapitány / Kapitány:** egy beküldött napló áttekintésére, ellenőrzésére vagy visszaküldésére (13.03 §3).

## Mielőtt elkezdené
- Nyissa meg a vontatóhajót a **Vontatóműveletek** részből.
- Napló létrehozásához és szerkesztéséhez vontató-írási hozzáférés szükséges.
- Az ellenőrzéshez az adott vontatóhajóra vonatkozó, szerveren rögzített **Kijelölés** (Vontatóhajó-kapitány vagy Kapitány) szükséges. Aki a naplót írta vagy beküldte, nem ellenőrizheti.

## Lépésről lépésre
1. Nyisson meg egy meglévő naplót a listából, vagy válasszon **Napló dátuma** és **Őrszolgálat** értéket, és nyomja meg a **Napi napló létrehozása** gombot.
2. Adja meg az **Időzóna (IANA)** értékét.
3. A **Motorok üzemóra-számlálói** részben minden **Számláló** esetén adja meg a **Kezdő állás**, a **Záró állás** és szükség esetén a **Nullázás vagy átfordulás bizonyítéka** értékét.
4. A **Felhasznált üzemanyag** részben adja meg az értéket, a mértékegységet és az **Üzemanyag-forrás hivatkozása** értékét.
5. A **A nap vontatási végrehajtásai** részben jelölje be azokat a végrehajtásokat, amelyek ehhez a naplóhoz tartoznak; töltse ki a **Linked defect identifiers** és a **Napi összefoglaló** mezőt (az összefoglaló legalább 20 karakter).
6. Nyomja meg a **Mentés** gombot. Az **Egyeztetés** sorban ellenőrizze a befejezett munkákat, a végrehajtási órákat, az üzemórákat és a jelentett hibákat.
7. Amikor az **Akadályok** lista üres, nyomja meg a **Beküldés kapitányi ellenőrzésre** gombot.
8. Kapitányként válassza ki a **Kijelölés** értékét, írjon indokot, és nyomja meg a **Kapitány: napló ellenőrzése** vagy a **Kapitány: napló visszaküldése** gombot.

## Mit jelentenek az állapotok
- **Piszkozat / Beküldve / Visszaküldve / Ellenőrizve:** a napló állapota.
- **Kapitányi ellenőrzés — Függőben / Figyelmet igényel / Átnézve:** Függőben = ellenőrzésre vár; Figyelmet igényel = visszaküldték, vagy úgy küldték be, hogy az egyeztetés nem Egyeztetve állapotú; Átnézve = a Kapitány ellenőrizte. Egy beküldött napló nem ellenőrzött napló.
- **Egyeztetve / Felülvizsgálat szükséges / Hiányos:** a tanácsadó jellegű összehasonlítás eredménye. Az indokkódok alatta szerepelnek.
- **nyitott intervallum:** egy végrehajtás még folyamatban van; a végrehajtási órák még nem véglegesek.
- **Biztonsági események — Ismeretlen:** ez az oldal nem számolja a biztonsági eseményeket; „nincs esemény” következtetést nem von le.
- **Szinkronizálva / Szinkronizálásra vár / Ütközés / Sikertelen:** a szinkronizálás állapota. A Szinkronizálásra vár azt mutatja, hogy a módosítás még nem érte el a szervert.

## Amit nem tehet meg / figyelem
- Aki a naplót írta vagy kapitányi ellenőrzésre beküldte, nem ellenőrizheti.
- Az egyeztetés eredménye nem jóváhagyás; az ellenőrzést a Kapitány végzi.
- Egy rekord befejezése és ellenőrzése nem ugyanaz a művelet (13.03 §1).
- Offline állapotban egy nem mentett módosítás az oldal memóriájában marad, és a kapcsolat helyreállásakor az eredeti kulcsával kerül elküldésre; tartsa nyitva az oldalt. A beküldés és az ellenőrzés online történik.
- Ha ütközés jelenik meg, ne feltételezze, hogy automatikusan a saját rekordja érvényesül (13.03 §6).

## Ha valami nem sikerül
- **"The server listed missing or blocking items." üzenet:** szüntesse meg az **Akadályok** részben szereplő kódokat.
- **"The record changed or the command conflicts." üzenet:** a **Legfrissebb betöltése** gombbal tekintse át az aktuális naplót; a megőrzött helyi módosítás elvész.
- **"The outcome is unknown." üzenet:** az **Újra** gomb ugyanazt a parancsot küldi el újra.
- **Ütközés:** az ezen az oldalon megőrzött módosítás soha nem egyesül; használja a **Legfrissebb betöltése** gombot. Az **Offline vontatóműveletek** oldalon rögzített, ütköző napi bejegyzéseket ott kell feloldani; ezek a **Lényeges ütközések központja** oldalon is megjelennek (13.03 §10).

## Kapcsolódó oldalak
