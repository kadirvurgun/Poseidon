---
key: industry-assurance
title: Ágazati megfelelőségbiztosítás (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Az Ágazati megfelelőségbiztosítás oldal egy tartályhajó SIRE 2.0 ellenőrzési eredményeit importálja munkafüzetből, a változásokat emberi felülvizsgálatra bocsátja, a megállapításokat biztonsági intézkedésekhez kapcsolja, és helyszíni bizonyítékokat rögzít. Arra a kérdésre válaszol: „Ennek az ellenőrzésnek mely megállapításai nyitottak, melyekhez kapcsoltak intézkedést, és hol van a bizonyíték?” Az importok piszkozatok: minden módosult tételt egy másik személy fogad el. A megállapítások nyitottak és nem ellenőrzöttek maradnak; ez az oldal soha nem zárja le és nem ellenőrzi őket. A lánc: Megállapítás → Intézkedés → Ellenőrzés; az ellenőrzés és a hatékonyság külön emberi feljegyzések az intézkedésen a Biztonsági események és majdnem-balesetek részben (13.03 §4, §8).

## Ki használja
- **DPA / HSEQ:** az ellenőrzési megállapítások intézkedésekhez kapcsolására és a Megállapítás → Intézkedés → Ellenőrzés lánc követésére (13.03 §4).
- **Szabályozási / megfelelőségbiztosítási felhasználó:** az oszlopmegfeleltetések kezelésére, valamint importok előkészítésére vagy felülvizsgálatára (13.03 §8).
- **Kapitány és hajószemélyzet:** egy megállapításhoz tartozó helyszíni bizonyíték (fényképek) rögzítésére.

## Mielőtt elkezdené
- Válasszon hajót. A SIRE 2.0 csak tartályhajókra vonatkozik. Ha a hajótípus nincs bejelentve a hajóprofilban, az importok **felülvizsgálat szükséges** állapotban maradnak.
- Az olvasáshoz ellenőrzésolvasási jogosultság szükséges. Az importálás, az import elfogadása, a bizonyíték írása, a megállapítások kapcsolása és a megfeleltetések írása külön jogosultságok. Egy intézkedés kapcsolásához biztonságiesemény-olvasási jogosultság is szükséges; a bizonyíték rögzítéséhez fájlolvasás is.
- Az oszlopséma szintetikus; a licencelt OCIMF formátumot a rendszer nem használja.
- Offline állapotban csak a fényképrögzítések kerülnek sorba ezen az eszközön; az importokhoz és döntésekhez kapcsolat szükséges.

## Lépésről lépésre
1. **Válasszon hajót**, és ellenőrizze az **Alkalmazhatóság** sort.
2. Szükség esetén az **Oszlopmegfeleltetések** részben adjon meg egy **Megfeleltetési kulcs** értéket, írjon oszloponként egy sort (fejléc=mező), és nyomja meg a **Mentés új verzióként** gombot.
3. Az **Import előkészítése** részben válassza ki a **Megfeleltetési verzió**, az **Ellenőrzés hivatkozása**, az **Ellenőrzés dátuma** és a **Munkafüzet (.xlsx)** értékét, majd nyomja meg a **Beküldés felülvizsgálatra** gombot.
4. Egy másik személy az **Import felülvizsgálata** részben minden módosult tételnél az **Elfogadás** vagy a **Jelenlegi megtartása** lehetőséget választja, legalább 10 karakteres **Indok** szöveget ír, és megnyomja az **Import elfogadása** vagy az **Import elutasítása** gombot.
5. Nyisson meg egy ellenőrzést az **Ellenőrzések** listából. Tekintse át a **Tételek** és a **Megállapítások** részt.
6. Egy megállapítás intézkedéshez kapcsolásához adja meg a **Biztonsági intézkedés azonosítója** értékét, és nyomja meg az **Intézkedés kapcsolása** gombot.
7. Helyszíni bizonyítékhoz adja meg a **Média**, a **Tiszta fájl azonosítója**, a **Fájlrevízió** és a **Fájl ellenőrzőösszege (SHA-256)** értékét, és nyomja meg a **Helyszíni bizonyíték rögzítése** gombot. Offline állapotban a gomb **Mentés erre az eszközre** lesz; online állapotban a **Küldés most** gombbal küldje el.

## Mit jelentenek az állapotok
- **Tartályhajó: alkalmazható / Nem alkalmazható / Hajótípus ismeretlen: felülvizsgálat szükséges:** ismeretlen hajótípus esetén az eredmény **Felülvizsgálat szükséges** marad; a rendszer soha nem feltételezi, hogy nem alkalmazható.
- **Felülvizsgálatra vár / Elfogadva / Elutasítva:** az import állapota.
- **Új / Módosult / Változatlan / Nincs ebben az importban:** a tétel a korábban elfogadott importhoz viszonyítva. Az ebben az importban nem szereplő tétel **Megtartva (soha nem törlődik)**.
- **Negatív megfigyelés / Pozitív megfigyelés / Nincs megfigyelés / Ismeretlen:** a tételre vonatkozó megfigyelés.
- **Nyitott / Nem ellenőrzött:** a megállapítások itt mindig nyitottként és nem ellenőrzöttként jelennek meg.
- **Nincs kapcsolt intézkedés / Intézkedés kapcsolva:** kapcsolódik-e a megállapítás biztonsági intézkedéshez. A kapcsolt intézkedés nem jelenti azt, hogy a megállapítás ellenőrzött, vagy hogy az intézkedés hatékony.
- **Online rögzítve / Offline rögzítve:** hogyan rögzítették a bizonyítékot.

## Amit nem tehet meg / figyelem
- Az a személy, aki egy importot előkészített, nem vizsgálhatja felül: „Ezt az importot Ön készítette elő; másnak kell felülvizsgálnia.”
- Ez az oldal nem zárja le és nem ellenőrzi a megállapításokat. Az Ellenőrzött nem Hatékony; a hatékonyság külön emberi felülvizsgálat az intézkedésen.
- Videóbizonyíték nem érhető el, amíg az üzemeltető nem engedélyez egy vírusellenőrzött videófájltípust.
- Egy későbbi importból hiányzó tétel soha nem törlődik; az előzmények megmaradnak.

## Ha valami nem sikerül
- **The request was refused:** oldja meg az alatta felsorolt okokat.
- **A rekord időközben megváltozott. Töltse újra és ellenőrizze ismét.** Nyomja meg az **Újratöltés** gombot.
- **Az eredmény ismeretlen (a kapcsolat megszakadt).** Használja az **Ugyanazon kérés ismétlése** lehetőséget.
- **Nincs jogosultsága az ágazati megfelelőségi rekordok olvasására.** vagy **Ehhez nincs jogosultsága.** Ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- Ha egy bizonyíték nem ellenőrizhető, ellenőrizze a fájlazonosítót, a revíziót és az SHA-256 ellenőrzőösszeget; szükség esetén forduljon az ellenőrzőhöz (13.03 §10).

## Kapcsolódó oldalak
