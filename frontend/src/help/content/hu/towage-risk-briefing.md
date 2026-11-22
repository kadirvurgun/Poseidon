---
key: towage-risk-briefing
title: Vontatási kockázat és eligazítás
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Ez az oldal egy beküldött vontatási munkához előkészíti és kiadja a **vontatási kockázati és eligazítási rekordot**, és összegyűjti a résztvevők **tudomásulvételeit**. Arra a kérdésre válaszol: „Milyen körülményeket, veszélyeket, megelőző és enyhítő korlátokat, munkaleállítási jogosultságot és vészhelyzeti intézkedéseket ismertettek az eligazításon, milyen állapotban van a kapcsolt kockázatértékelés, és minden résztvevő tudomásul vette-e a kiadott revíziót?” Az eligazítás kiadása a kijelölt Kapitány vagy Vontatóhajó-kapitány emberi rekordja; nem GO-döntés és nem engedély. A hiányos eligazítás blokkolja a READY állapotot.

## Ki használja
- **Vontatóhajó-kapitány / Kapitány:** az eligazítás előkészítésére és kiadására, valamint annak biztosítására, hogy ne szülessen READY döntés, mielőtt a kockázat és az eligazítás teljes (13.03 §3).
- **Személyzet és más résztvevők (révkalauz, tiszt, fedélzeti személyzet, gépész):** a kiadott eligazítás tudomásulvételére vagy kifogásolására a megfelelő szerepkör-kontextusban (13.03 §6).
- **Vontatási műveleti koordinátor:** az eligazítás és a tudomásulvétel állapotának követésére (13.03 §7).

## Mielőtt elkezdené
- Nyissa meg a vontatóhajót a **Vontatóműveletek** részből, és válassza ki a munkát a **Válasszon egy beküldött vontatási munkát** listából.
- A munka **Vontatási terv** rekordjának jóváhagyottnak kell lennie, és a kapcsolt kockázatértékelésnek is jóváhagyottnak kell lennie; ellenkező esetben az eligazítás nem adható ki.
- A kiadáshoz a koordináló vontatóhajóra vonatkozó, szerveren rögzített **Kijelölés** (Vontatóhajó-kapitány vagy Kapitány) szükséges. A kiadáshoz ezenfelül kockázatértékelés-olvasási hozzáférés és a koordináló vontatóhajóra vonatkozó vontató-írási jogosultság kell. Tudomásulvételre csak a résztvevői listán szereplő személyek jogosultak.

## Lépésről lépésre
1. A felső sorban olvassa el az **Állapot**, a **Revízió**, a **Tudomásulvétel**, a **Kockázati állapot** és a **Terv állapota** értékét.
2. A **Készenléti bemenet (a hiányos eligazítás blokkolja a READY állapotot)** részben tekintse meg a készenléti kapunak küldött eredményeket és indokkódokat.
3. Töltse ki az **Az eligazításon megerősített körülmények** (időjárás, áramlás, látótávolság), a **Veszélyek**, a **Megelőző korlátok**, az **Enyhítő korlátok**, a **Munkaleállítási jogosultság** és a **Vészhelyzeti intézkedések** részt.
4. Adja meg a **Kapcsolt kockázatértékelés azonosítója** értékét; a **Résztvevők** részben a **Hozzáadás** gombbal adja meg minden résztvevő **Személyazonosító** és **Szerepkör** értékét.
5. Mentsen a **Piszkozat létrehozása** vagy a **Piszkozat mentése** gombbal; amikor a **Beküldés előtt hiányzik** lista üres, válasszon egy **Kijelölés** értéket, és nyomja meg az **Eligazítás kiadása** gombot.
6. Résztvevőként olvassa el a kiadott revíziót, és nyomja meg a **Tudomásulvétel** gombot. Ha nem ért egyet, írjon legalább 10 karakteres **Indok** szöveget, és nyomja meg a **Kifogás** gombot.
7. Ha az eligazítást módosítani kell, írjon indokot, és használja a **Módosítás** gombot. A **Tudomásulvételek** és a **Revíziók** lista mutatja az előzményeket.

## Mit jelentenek az állapotok
- **Piszkozat / Kiadva:** az eligazítás állapota. A Kiadva azt jelenti, hogy a tudomásulvételek megkezdődhetnek; önmagában nem készenléti eredmény.
- **Nincs kiadva / Függőben / Teljes / Vitatott:** a résztvevői tudomásulvétel állapota. Csak akkor válik **Teljes** állapotúvá, ha minden résztvevő tudomásul vette a kiadott revíziót.
- **Jóváhagyva és tudomásul véve / Figyelmet igényel / Blokkolva / Felülvizsgálat szükséges:** a kapcsolt kockázatértékelés állapota.
- **Megfelelt / Nem felelt meg / Ismeretlen:** a készenléti kapunak küldött ellenőrzési eredmény. A hiányzó vagy ki nem adott eligazítás és a függőben lévő tudomásulvételek **Nem felelt meg** eredményt jelentenek, amely blokkolja a READY állapotot.

## Amit nem tehet meg / figyelem
- Nem születik READY döntés, mielőtt a kockázat és az eligazítás teljes, és a résztvevői tudomásulvételek rögzítésre kerültek (13.03 §3).
- Egy kifogás a tudomásulvételi állapotot **Vitatott** értékre állítja, és Nem felelt meg eredményt küld a készenléti kapunak, így a READY és a GO blokkolva van. Az indok a **Tudomásulvételek** részben jelenik meg. Ugyanarra a revízióra a tudomásulvétel nem módosítható; a kifogás feloldásához az eligazítást módosítják (új revízió), újra kiadják, és minden résztvevő újra tudomásul veszi.
- Ha a terv vagy a kockázatértékelés az eligazítás után megváltozik, az eligazítást újra felül kell vizsgálni; a korábbi tudomásulvételek nem számítanak egy új revízióra.
- Senki nem vesz tudomásul más nevében; minden résztvevő a saját tudomásulvételét rögzíti (13.03 §6).
- Offline állapotban erről az oldalról semmilyen parancs nem kerül elküldésre. Egy tudomásulvétel sorba állítható az **Offline vontatóműveletek** oldalon.

## Ha valami nem sikerül
- **"Required sections are missing." üzenet:** teljesítse a hiányzó kódokat; ellenőrizze, hogy a terv és a kockázatértékelés jóvá van-e hagyva.
- **"The record changed" üzenet:** az **Újratöltés** gombbal tekintse át az aktuális eligazítást.
- **"The outcome is unknown" üzenet:** az **Újra** gomb ugyanazt a kérést küldi el újra.
- **No authorization:** (nincs jogosultság) ellenőrizze szerepkörét, vontatóhajó- és kijelölési kontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
