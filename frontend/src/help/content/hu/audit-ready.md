---
key: audit-ready
title: Auditra kész nyomkövetés
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Az Auditra kész nyomkövetés oldal szakaszról szakaszra mutatja az Önhöz rendelt nyomkövetési láncokat egy hajóra vonatkozóan: Követelmény → kontroll → bizonyíték → ellenőrzés → döntés, állapotokkal, időbélyegekkel és bejelentett forráshivatkozásokkal. Arra a kérdésre válaszol: „Melyik követelményen alapul ez a bizonyíték, ki ellenőrizte, és milyen döntést rögzítettek?” A rögzített lánc emberi feljegyzés, nem jóváhagyás és nem készenléti nyilatkozat (13.03 §4, §8).

## Ki használja
- **Szabályozási / megfelelőségbiztosítási felhasználó:** ellenőrzi, hogy a Követelmény→Kontroll→Bizonyíték→Ellenőrzés láncban nem maradt-e árva rekord (13.03 §8).
- **DPA / HSEQ:** audit, PSC vagy szemle előkészítésekor bemutatja a Megállapítás→Intézkedés→Ellenőrzés nyomvonalat a bizonyítékaival együtt (13.03 §4).

## Mielőtt elkezdené
- Az oldalhoz auditolvasási és emberi nyomkövetés-olvasási jogosultság, valamint az adott hajóra vonatkozó érvényes nyomkövetés-olvasói kinevezés szükséges. A jogosultság önmagában nem elég; kinevezés nélkül egyetlen lánc sem jelenik meg.
- Válassza ki a megfelelő hajót. A lista csak az Önhöz rendelt láncokat mutatja.
- Az oldal élő kapcsolaton keresztül töltődik be. Offline állapotban a megjelenített adatok elavultak lehetnek.

## Lépésről lépésre
1. Válasszon hajót a **Hajó** mezőben (**Hajó kiválasztása**).
2. Nyisson meg egy láncot a **Hozzárendelt láncok** közül.
3. A **Nyomkövetési szakaszok** részben bontsa ki az egyes szakaszokat (Követelmény, Kontroll, Bizonyíték, Ellenőrzés, Döntés), és tekintse át az **Állapot**, a **Rögzítés időpontja**, a **Revízió**, a **Tartalom-ujjlenyomat** és a **Forráshivatkozás (bejelentett, nem ellenőrzött)** értékeit.
4. Ha **Nyitott tételek** szerepelnek, adja át a kódokat az érintett rekord felelősének.
5. Ha vannak olvasható hozzárendelt láncok, állítson össze egy csomagot az **Auditcsomag** részben az **Új csomag összeállítása** gombbal, nyissa meg az **Összeállított csomagok** közül, és szükség esetén használja a **JSON exportálása** funkciót.
6. Az adatok frissítéséhez nyomja meg a fejlécben az **Újra** gombot.

## Mit jelentenek az állapotok
- **Rögzített lánc — csak emberi feljegyzés, nem jóváhagyás:** minden szakasz rögzítve van. Ez nem nyilvánítja a hajót vagy a műveletet késznek vagy megfelelőnek.
- **Felülvizsgálat szükséges:** legalább egy szakasz hiányzik, ismeretlen vagy nem érhető el. Ismeretlen vagy olvashatatlan bemenet soha nem jelenik meg teljesként.
- **Nincs rögzítve:** az adott szakaszhoz nincs rekord; például még nem rögzítettek ellenőrzést vagy döntést.
- **Nem elérhető / UNKNOWN:** a rekord jelenleg nem olvasható; a tartalmát a rendszer nem feltételezi.
- **Elavult: egy bemenet lecserélődött vagy lejárt; felülvizsgálat szükséges.** A bizonyíték vagy egy forrás később megváltozott; vizsgálja felül újra a láncot.
- **Hivatkozás nincs megadva:** nem adtak meg forráshivatkozást; a hivatkozás nyilatkozat, nem ellenőrzött tény.
- Az ellenőrzött bizonyíték nem jelenti azt, hogy az intézkedés hatékony volt: Ellenőrzött ≠ Hatékony (13.03 §4).
- **Auditcsomag:** „Rendszer által összeállított; nem hivatalos rekord”. Az Ön kinevezésein kívül eső tételeket csak megszámolja, nem jeleníti meg; a **Felváltva (a nyomon követéshez megőrizve)** tételeket soha nem kezeli aktuálisként.

## Amit nem tehet meg / figyelem
- Ezen az oldalon nem szerkeszthet rekordokat, nem ellenőrizhet és nem dönthet; az oldal csak olvasható. Az ellenőrzést és a döntéseket a kinevezett személy rögzíti a saját oldalukon.
- Nem láthatja az Önhöz nem rendelt láncokat; a rejtett láncokat a rendszer soha nem nevezi meg, a nem elérhető hozzárendelt láncok pedig csak darabszámként jelennek meg.
- Az integritási és kontextus-ujjlenyomatok nem jelentenek felhatalmazást vagy jóváhagyást.

## Ha valami nem sikerül
- **Nincs jogosultsága ehhez a nézethez.** Ellenőrizze szerepkörét, a hajót és a kinevezési kontextust; forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **Ehhez a hajóhoz nincs Önhöz rendelt lánc.** Ellenőrizze, hogy a megfelelő hajót választotta-e; ha kinevezés szükséges, kérdezze meg a felelős személyt.
- **A nyomkövetés nem tölthető be.** Nyomja meg az **Újra** gombot.
- **A lánc nem található, vagy már nem látható Önnek.** Lehet, hogy a kinevezése megszűnt, vagy a rekord kikerült a hatóköréből.
- Ha egy rekord nem ellenőrizhető, vizsgálja meg a bizonyítékát és eredetét, és szükség esetén forduljon az ellenőrzőhöz (13.03 §10).

## Kapcsolódó oldalak
