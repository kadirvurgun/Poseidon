---
key: pilot-program
title: Pilotprogram: hetek és értékjelentés
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Pilotprogram oldal egy helyen tartja egy hajó (és opcionálisan egy vontatóhajó) pilotheteit, az egyes hetek ellenőrzőlista-tételeit, a pilotproblémákat, a napi visszajelzéseket és az értékjelentést. Arra a kérdésre válaszol: „Minden pilothét tételeit valódi rekordok teljesítették-e, és hogyan változtak a mért értékek a kiinduló értékhez képest?” A felül látható figyelmeztetés érvényes: „Software cannot complete a pilot. A checklist item is satisfied only by a link you make to a real record that the server verifies; a missing baseline stays Unknown; only the appointed Product Owner closes a week or records the program decision.” (A szoftver nem tud pilotot befejezni. Egy ellenőrzőlista-tétel csak egy Ön által létrehozott, a szerver által ellenőrzött valódi rekordra mutató kapcsolattal teljesül; a hiányzó kiinduló érték Ismeretlen marad; hetet lezárni vagy programdöntést rögzíteni csak a kinevezett termékgazda tud.)

## Ki használja
- **Termékgazda:** lezárja a heteket, és rögzíti a programdöntést (Folytatás, Javítás, Leállítás, Bővítés). Ehhez érvényes termékgazdai kinevezés szükséges.
- **Pilotcsapat:** rekordokat kapcsol az ellenőrzőlista-tételekhez, problémákat jelent be, visszajelzéseket és méréseket rögzít.
- **Triázsfelelős:** besorolja (triázsolja) és megoldja a problémákat.
- A 13.03 ehhez az oldalhoz nem határoz meg külön szerepkört; a bérlőadminisztrációs elvek érvényesek (13.03 §9).

## Mielőtt elkezdené
- A megtekintéshez bérlőszintű olvasási és pilotprogram-olvasási jogosultság szükséges. A programok létrehozásához, a rögzítéshez, a triázshoz és a döntéshez mind saját jogosultság kell; a hetek lezárásához és a döntésekhez termékgazdai kinevezés is szükséges.
- A kapcsolandó rekordoknak (például bizonyítvány-, személyzeti vagy munkarendelés-rekord) már létezniük kell a saját oldalukon.
- A visszajelzésekben soha ne adjon meg neveket, e-mail-címeket, telefon- vagy azonosítószámokat; csak álnevet és szerepkört használjon.
- A rögzítéshez élő kapcsolat szükséges.

## Lépésről lépésre
1. Nyisson meg egy programot a **Pilotprogramok** közül. Új programhoz a **Pilotprogram létrehozása** részben töltse ki a **Program neve**, a **Hajó**, a **Vontatóhajó (nem kötelező)**, az **Az 1. hét kezdő dátuma** és a **Naptár időzónája (IANA)** mezőt, és nyomja meg a **Rögzítés** gombot.
2. Minden **Hét** részben tekintse át az **Ellenőrzőlista-tétel**, a **Kritérium (forrás)**, a **Terület**, az **Állapot** és a **Kapcsolt rekordok** oszlopot.
3. Egy tétel teljesítéséhez a **Rekord kapcsolása** részben adja meg az **Ellenőrzőlista-tétel**, a **Rekordtípus** és a **Rekordazonosító** értékét, és nyomja meg a **Rekord kapcsolása** gombot. Egy hibás kapcsolatot a **Visszavonás oka** megírása után a **Kapcsolat visszavonása** gombbal távolíthat el.
4. A **Pilotproblémák** részben a **Probléma bejelentése** segítségével adja meg a területet, a **Kategória** értékét, a súlyosságot és a **Probléma összefoglalója** szöveget. A triázsfelelős a **Triázs** funkciót használja **Triázs-megjegyzés** megadásával, majd megoldja a problémát.
5. A **Napi visszajelzés** részben a **Visszajelzés rögzítése** segítségével adjon meg **Összefoglaló** és **Hangulat** értéket.
6. Az **Értékjelentés** részben a **Mérés rögzítése** segítségével adja meg a **Mutató**, a **Fázis** (Kiinduló érték vagy Pilotidőszak), az **Érték**, a **Mintaméret**, az **Időszak kezdete**, az **Időszak vége**, a **Bizonyítékhivatkozás** és a **Módszer és minta** értékét.
7. Ha Ön a termékgazda, rögzítse az eredményt és az indoklást a **Hét lezárása** részben. A program végén töltse ki a **Programdöntés rögzítése** részben a döntést, a **Kinevezés** és az **Indoklás** mezőt, és nyomja meg a **Rögzítés** gombot.

## Mit jelentenek az állapotok
- **Kapcsolva és ellenőrizve:** a tételhez egy, a szerver által ellenőrzött valódi rekord kapcsolódik. **A kapcsolat már nem érvényes** és **Hiányzik** állapotú tételek nem teljesültek.
- **Completion required:** a kapcsolt rekordnak befejezett állapotban kell lennie. „Az auditnyom azt igazolja, hogy egy személy írta a rekordot; nem igazolja, hogy a rekord vagy a folyamat befejeződött.”
- **Emberi lezárásra kész / Nem kész:** javaslat a hétre vonatkozóan; csak a termékgazda zárhatja le **Elfogadva** vagy **Nem elfogadva** eredménnyel.
- **Aznap triázsolva / Késve triázsolva / Triázs ma esedékes / Késésben:** a P0-problémák triázsának időzítése a program naptári napjához viszonyítva. **A forrás nem ad meg aznapi célt** állapotú problémáknál a rendszer nem talál ki határidőt.
- **Mért / Ismeretlen:** ha egy kiinduló vagy pilotérték hiányzik, a jelentés **Ismeretlen** marad; soha nem számít nullának vagy javulásnak. A **Nem összevethető** értékeket a rendszer nem veti össze.
- **Telemetria (bejelentett, nem ellenőrzött):** egy bejelentett telemetriai érték.
- **Bővítés lehetséges; emberi döntésre vár / Javítás javasolt:** programjavaslat, nem döntés.

## Amit nem tehet meg / figyelem
- „Elfogadott csak akkor lehet, ha minden tétel ellenőrzött, és nincs nyitott, függő vagy késett P0-triázs.”
- „A bővítés csak akkor lehetséges, ha minden hét elfogadott, és az értékek, az UAT, a hibák és a P0 rendezettek.”
- Ha Ön írta azt a bizonyítékot, amelyen egy hét vagy döntés alapul (kapcsolatok, visszajelzések, mérések), nem zárhatja le azt a hetet, és nem rögzítheti azt a döntést.
- Kitalált tesztprogramhoz (fixture) nem rögzíthető hétlezárás vagy döntés.
- A rendszer soha nem fejez be pilotot, soha nem zár le hetet, és soha nem hamisít döntéseket vagy jóváhagyásokat.

## Ha valami nem sikerül
- **Nincs érvényes Product Owner kinevezése; a lezárások és döntések csak olvashatók.** A kinevezéshez forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **A visszajelzés rögzítése nem érhető el, amíg egy üzemeltető be nem állítja a megőrzési szabályt.** Kérje meg a rendszergazdát, hogy állítsa be a megőrzési szabályt.
- **A rekord megváltozott vagy már felhasználták. Töltse újra.** Nyissa meg az aktuális állapotot az **Újratöltés** gombbal.
- **Az eredmény bizonytalan. Az újrapróbálás ugyanazt a parancsot küldi.** Az **Újra** gombbal biztonságosan újraküldheti ugyanazt a parancsot.
- **A szabályok elutasították.** Például előfordulhat, hogy a kapcsolt rekord nem létezik, kívül esik a hatókörön, vagy nincs befejezve.
- **Nem engedélyezett.** Ön írta azokat a kapcsolatokat, visszajelzéseket vagy méréseket, amelyeken ez a hétlezárás vagy döntés alapul; egy másik személynek kell rögzítenie.
- Egy még el nem kezdődött hét nem zárható le (ilyenkor „A rekord megváltozott vagy már felhasználták. Töltse újra.” jelenik meg).
- **Nincs hozzáférése ezekhez a rekordokhoz.** Ellenőrizze szerepkörét és bérlői hatókörét (13.03 §10).

## Kapcsolódó oldalak
