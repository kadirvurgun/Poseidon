---
key: port-activity-sof
title: Kikötői tevékenység és SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A kikötői tevékenység és SOF oldal a kiválasztott hajó **kikötői eseményeit** üzemi feljegyzésként tárolja bizonyítékhivatkozásokkal, és lehetővé teszi, hogy egy kinevezett személy belsőleg véglegesítse a **tényállási jegyzéket (Statement of Facts, SOF)**. Arra a kérdésre válaszol: „Milyen kikötői eseményeket jelentettünk be, milyen UTC-sorrendben és időtartammal, milyen rakomány-, merülés-, révkalauz-, vontató-, késési és tiltakozási hivatkozásokkal, és véglegesítette-e egy kinevezett személy ezt a SOF-revíziót?” Az oldal nem hoz létre aláírást, rakodási időt (laytime) vagy fekbért (demurrage), és a rögzített események nem utasítások és nem jóváhagyott tervezett időablakok.

## Ki használja
- **Kapitány:** az érkezés és a kikötői tevékenység bizonyítékokkal alátámasztott üzemi feljegyzésként való vezetésére és a SOF véglegesítésére (13.03 §2).
- **Első tiszt / gépüzemvezető:** kikötői események rögzítésére, érvényes kinevezés esetén pedig a SOF véglegesítésére.

## Mielőtt elkezdené
- A felső kontextussávban válassza ki a **megfelelő vállalatot és hajót**. Ha nincs kiválasztva hajó, az oldal a „Hajó kiválasztása” üzenetet mutatja.
- Az olvasáshoz `operations.port.read` szükséges; piszkozatok mentéséhez `operations.port.write` és szerkesztési jog erre a hajóra.
- A véglegesítéshez `operations.port.write`, szerkesztési jog erre a hajóra és `operations.port.finalize` szükséges, valamint erre a hajóra érvényes véglegesítési kinevezés (kapitány, első tiszt vagy gépüzemvezető).

## Lépésről lépésre
1. Válassza ki a hajót. A **Rögzített kikötői események idővonala** a visszaadott eseményeket a szerver UTC-sorrendjében listázza. A **Visszaadott oldal szűrése** csak ezt az oldalt szűkíti.
2. Olvassa el a lista alatti **Statement of Facts idővonal** részt: minden esemény a **Rögzített időtartam**, a **Késés oka** és a **Megjegyzések** értékkel.
3. Nyomja meg az **Új piszkozatrekord** gombot. Töltse ki a **Kikötőlátogatási kontextus**, a **Rögzített tevékenység és időzítés**, a **Rakomány- és merülésnyilatkozatok** és a **Támogatás, kivételek és bizonyítékok** részt. Az eseményidőpontokhoz Z jelölés vagy kifejezett UTC-eltolás szükséges.
4. Nyomja meg a **Nem ellenőrzött piszkozat mentése** gombot. Minden mentés új revíziót ad a **Rögzített revízióelőzmények** listához.
5. Egy mentett rekordon nyissa meg a **Statement of Facts véglegesítése** részt. Ha a **Required facts missing** jelzés látható, először töltse ki ezeket a mezőket.
6. Jelölje be az összes felsorolt megoldatlan hivatkozást a tudomásulvételhez, válassza ki a **Kinevezés** értékét, és nyomja meg a **SOF véglegesítése** gombot. A rendszer rögzíti, ki, mikor, milyen feladatkörben és milyen indokkal véglegesítette.

## Mit jelentenek az állapotok
- **PISZKOZAT:** a SOF nincs véglegesítve.
- **VÉGLEGES (belső):** egy kinevezett személy véglegesítette ezt a revíziót. Belső jellegű: nem aláírás és nem külső megállapodás.
- **Aláírás nincs rögzítve:** mindig megjelenik; ezen az oldalon nincs aláírás.
- **VÉGLEGES után módosítva: új felülvizsgálat és véglegesítés szükséges.:** a rekordot a véglegesítés után újra mentették, és visszakerült PISZKOZAT állapotba.
- **UNKNOWN (unresolved references):** ezekhez a kötelező hivatkozásokhoz nincs felelős keresés. A tudomásulvételük nem teszi őket ismertté.
- **Felülvizsgálat szükséges / Nem ellenőrzött piszkozatrekord:** a kikötői események nyilatkozatok; semmi nincs ellenőrizve.
- **Piszkozat (nem véglegesített) / Végleges (belső; nem aláírás):** a szerver által erre a revízióra rögzített SOF-állapot, amely a rekord összefoglalójában látható. A Végleges állapot kizárólag rögzített emberi véglegesítésből származik.

## Amit nem tehet meg / figyelem
- A rendszer soha nem véglegesíti a SOF-ot. A véglegesítés egy kinevezett kapitány, első tiszt vagy gépüzemvezető emberi cselekménye (13.03 §2).
- A SOF véglegesítése le van tiltva, amíg kötelező tények hiányoznak, nincs kinevezés, vagy a megoldatlan hivatkozásokat nem vették tudomásul.
- A véglegesítés nem aláírás, nem külső megállapodás, és nem rakodási idő vagy fekbér.
- Az oldal lábléce ezt rögzíti: „SOF finalization is an internal human act recorded in the finalization panel by an appointed Master, Chief Officer or Chief Engineer. Signatures, operational/legal approval and offline capture remain unavailable.” (A SOF véglegesítése belső emberi cselekmény, amelyet egy kinevezett kapitány, első tiszt vagy gépüzemvezető rögzít a véglegesítési panelen. Aláírás, üzemi/jogi jóváhagyás és offline rögzítés továbbra sem érhető el.)
- A piszkozatok mentéséhez és a véglegesítéshez aktív online munkamenet szükséges; ezen az oldalon nincs offline rögzítés.

## Ha valami nem sikerül
- **„Ehhez a hajóhoz nincs érvényes véglegesítési kijelölés.”:** ellenőrizze szerepkörét és hajókontextusát a bérlői adminisztrátorral (13.03 §10).
- **„Ismeretlen eredmény: az újrapróbálás egyszer elküldi ugyanazt a parancsot.”:** használja az **Újra** gombot; ne kezdjen új véglegesítést.
- **„Command not accepted”:** olvassa el a megjelenített kódot, töltse be újra a rekordot az **Aktuális mentett rekord olvasása** gombbal, és próbálja újra.
- **„The saved source changed.”:** az újabb mentés előtt kifejezetten töltse újra vagy vesse el a módosításokat.

## Kapcsolódó oldalak
