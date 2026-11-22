---
key: system-health
title: Rendszerállapot
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A „Rendszerállapot” egy képernyőn mutatja az adminisztrátoroknak a **szolgáltatásmegfigyeléseket**: API, auditíró, szinkronizálás, értesítési feladatok, fájlszolgáltatás és a bemenetek frissessége. Arra a kérdésre válaszol: „Mely szolgáltatásjeleket figyelték meg a kiválasztott hajóra és erre a bérlőre vonatkozóan, és melyeket nem?” A konfiguráció és a hiányzó telemetria nem igazolja a szolgáltatás egészséges működését vagy a hajó készenlétét.

## Ki használja
- **Rendszer- / bérlőadminisztrátor:** a szolgáltatási és szinkronizálási problémák észlelésére és ellenőrzött módon történő kezelésére (13.03 §9).
- **Támogatás / fejlesztés:** a megfigyelések áttekintésére, amikor alkalmazáshibát jelentenek (13.03 §10).

## Mielőtt elkezdené
- Az oldal csak azoknak a felhasználóknak látható, akik bérlőszintű olvasási, műveleti előzmény-olvasási és rendszerállapot-olvasási hozzáféréssel rendelkeznek.
- A felső kontextussávban ellenőrizze, hogy a **megfelelő vállalat** van-e kiválasztva.
- A megfigyeléseket hajónként kérik le; először hajót kell választania.

## Lépésről lépésre
1. Válassza ki a hajót a **Hajó megfigyelési hatóköre** részben. Hosszú lista esetén használja az **Előző hajók** / **Következő hajók** gombot.
2. Ellenőrizze az „Olvasva” időpontot.
3. A **Szolgáltatás elérhetősége** részben olvassa le a folyamat indítási idejét és futási idejét.
4. Az **API** és az **Auditíró** kártyán olvassa le a kérés-, hiba-, tárolt és késleltetési értékeket. Ezek erre a bérlőre vonatkoznak a folyamat indulása óta.
5. A **Szinkronizálás** részben tekintse meg a véglegesített eredményeket, a tisztázatlan ütközéseket és az utolsó véglegesített szinkronizálást.
6. Az **Értesítési feladatok** részben tekintse meg a nyitott értesítéseket, a határidőn túli eszkalációkat és a kiértékelő feladat állapotát.
7. Olvassa el a **Fájlszolgáltatás** és a **Bemenetek frissessége** kártyát.
8. A **Pilot mérőszámok** alatt tekintse át a regisztrált pilot kliensek megfigyeléseit.
9. Aktuális adatokért nyomja meg a **Megfigyelések frissítése** gombot.

## Mit jelentenek az állapotok
- **Nem megfigyelt / Nem érhető el:** ehhez a jelhez nincs megfigyelés. Ez nem jelenti azt, hogy „egészséges” vagy „nincs probléma”.
- **Beállítva · nem tesztelt:** a fájlszolgáltatás beállítottnak tűnik, de nem történt élő tároló- vagy víruskereső-próba.
- **Ütemezésen belül / Fut / Késésben / Az ütemező nincs engedélyezve / Futás megszakadt (zárolás lejárt):** az értesítéskiértékelő feladat állapota.
- **Tisztázatlan ütközések:** emberi feloldásra váró offline ütközések. Ezek soha nem egyesülnek automatikusan.
- **Aktuális / Elavult / Ismeretlen / Nem érhető el:** a bemenetfrissességi értékelések darabszámai. Az Ismeretlen nem jelenti azt, hogy „Aktuális”.
- **This snapshot is older than two minutes:** (ez a pillanatkép két percnél régebbi) frissítsen, mielőtt támaszkodna rá.

## Amit nem tehet meg / figyelem
- Ez az oldal csak olvasható; semmilyen művelet nem történik szolgáltatásokon, hajókon vagy rekordokon.
- Az értesítési rekordok csak alkalmazáson belüliek; nem bizonyítják a kézbesítést és semmilyen biztonsági döntést sem.
- A szolgáltatás elérhetősége csak azt mutatja, hogy ez az API-folyamat válaszolt; nem bizonyítja, hogy más szolgáltatások futnak.
- A pilot mérőszámok megadott és nem ellenőrzött adatok; nem jelentenek elfogadási állítást.
- Ne javítson adatokat közvetlenül éles környezetben; a konfigurációs módosításokat ellenőrzött módon tegye közzé (13.03 §9).

## Ha valami nem sikerül
- **Health access is unavailable for this context:** (az állapot-hozzáférés ebben a kontextusban nem érhető el) ellenőrizze szerepkörét és bérlői hatókörét.
- **Service observations unavailable:** (a szolgáltatásmegfigyelések nem érhetők el) a rendszer nem következtet egészséges állapotra; egy idő után használja a „Megfigyelések frissítése” gombot.
- **There is an unresolved conflict:** (tisztázatlan ütközés van) irányítsa az érintett felhasználót az Ütközésközpontba (13.03 §10).
- **Application error:** (alkalmazáshiba) jelentse a támogatásnak/fejlesztésnek a korrelációs azonosítóval, a képernyővel, az időponttal és a lépésekkel.

## Kapcsolódó oldalak
