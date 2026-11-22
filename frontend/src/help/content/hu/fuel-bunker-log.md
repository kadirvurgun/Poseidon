---
key: fuel-bunker-log
title: Üzemanyag, bunkerolás és ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Az üzemanyag, bunkerolás és ROB oldal a kiválasztott hajó **üzemanyag- és bunkernyilatkozatait** nem ellenőrzött piszkozatként tárolja a forráselőzményeikkel együtt. Arra a kérdésre válaszol: „Milyen üzemanyagot vettünk át vagy fogyasztottunk, milyen ROB-ot jelentettünk be előtte és utána, mely BDN-, szállító-, minta- és tartályhivatkozások tartoznak hozzá, és folytatódik-e a bejelentett ROB-lánc egyik rekordról a következőre?” Az oldal nem javít mennyiségeket, nem számít kibocsátást, és nem állapít meg megfelelést vagy ellenőrzést.

## Ki használja
- **Gépüzemvezető / géptisztek:** bunkerolási, fogyasztási és ROB-nyilatkozatok rögzítésére a bizonyítékhivatkozásaikkal együtt (13.03 §5).
- **Kapitány:** az üzemanyag-nyilatkozatok követésére a kapitány által felülvizsgált déli összesítők mellett (13.03 §2).

## Mielőtt elkezdené
- A felső kontextussávban válassza ki a **megfelelő vállalatot és hajót**. Ha nincs kiválasztva hajó, az oldal a „Hajó kiválasztása” üzenetet mutatja.
- Az olvasáshoz `operations.fuel.read` szükséges. Létrehozáshoz és mentéshez `operations.fuel.write` és szerkesztési jog szükséges erre a hajóra.
- Az időpontoknak tartalmazniuk kell a dátumot, a másodperceket és a Z jelölést vagy egy kifejezett UTC-eltolást; időzóna nélküli helyi időt a rendszer nem fogad el.

## Lépésről lépésre
1. Válassza ki a hajót. Az **Üzemanyag- és ROB-rekordok** a visszaadott rekordokat listázza. A **Visszaadott oldal szűrése** csak ezt az oldalt szűkíti.
2. Olvassa el a lista alatti **ROB-folytonossági ellenőrzés** részt. Bejelentett üzemanyagtípusonként mutatja az egyenleget (előtte + átvett − fogyasztott − utána), a **Folytonosság az előző bejegyzéssel** értéket és a **Felülvizsgálati jelzések** listáját.
3. Kontextusként olvassa el a **A kapitány által felülvizsgált Noon üzemanyag- és ROB-összesítők** részt. Ezek bejelentett szövegként jelennek meg, és nem másolódnak át a nyilvántartásba.
4. Nyomja meg az **Új piszkozatrekord** gombot. Töltse ki a **Rekord- és szállítási kontextus**, a **Mennyiségi és ROB-nyilatkozatok** és az **Üzemanyag-tulajdonságok és mintarekord** részt. Minden mezőnél bejelentett értéket, **Hiányzik** vagy **Ismeretlen** jelölést adjon meg.
5. Nyomja meg a **Nem ellenőrzött piszkozat mentése** gombot. Minden mentés új revíziót ad a **Rögzített revízióelőzmények** listához.
6. Ha egy mentett déli jelentésből vagy gépnaplóból szeretne kiindulni, használja az adott oldalon az **Üzemanyagrekord megnyitása mentett forrásból** lehetőséget. A **Kapcsolt napi nyilatkozat** itt csak olvasható marad.

## Mit jelentenek az állapotok
- **Piszkozatnyilatkozat / Nem ellenőrzött piszkozatrekord:** egy személy jelentette be az értékeket; semmi nincs ellenőrizve.
- **Felülvizsgálat szükséges:** minden rekord megfelelőségi felülvizsgálati állapota; a megfelelést nem értékelték.
- **Függőben; ellenőrzés nincs igazolva:** az ellenőrzési felülvizsgálat állapota; nincs ellenőrzés.
- **Eltérés nem észlelhető:** a bejelentett számok összeadódnak. Ez nem ellenőrzés.
- **Ismeretlen:** a folytonosság nem ítélhető meg, például ha egy érték hiányzik, vagy a régebbi előzmények az értékelt időablakon kívül esnek. Az ismeretlen nem jelenti azt, hogy „Nincs eltérés” vagy „Kész”.
- **Hiányzik / Ismeretlen:** elkülönül a nullától.

## Amit nem tehet meg / figyelem
- Emberi felülvizsgálat, üzemi vagy jogi jóváhagyás, véglegesítés és offline rögzítés ezen az oldalon nem érhető el.
- A rendszer a különbségeket emberi felülvizsgálatra jelzi; soha nem javít mennyiséget, és nem dönt az üzemanyag-megfelelésről.
- Csak a legújabb 100 rekordot értékeli. Tartályhivatkozás nélkül egy több tartállyal rendelkező hajón a hiány a hiányzó tartályadatokra vonatkozó kérdés, nem adathiba.
- A déli jelentés vagy gépnapló tisztázatlan mértékegységű mennyiségei soha nem alakulnak át átvett, fogyasztott vagy ROB-értékké.

## Ha valami nem sikerül
- **„The saved source changed.”:** a bevitt adatai megmaradnak; az újabb mentés előtt kifejezetten töltse újra vagy vesse el őket.
- **„Outcome unknown.”:** használja a **Pontos üzemi parancs megismétlése** gombot; ne gépelje be újra.
- **„Ellenőrizze a megadott értékeket, kifejezett időeltolásokat és aktuális forrásjogosultságot.”:** javítsa a megjelölt mezőt, vagy hagyja Hiányzik vagy Ismeretlen állapotban.
- **Operational source or access unavailable:** használja a **Forrásolvasás újrapróbálása** gombot; ha a hozzáférés továbbra is megtagadott, ellenőrizze szerepkörét és hajókontextusát a bérlői adminisztrátorral (13.03 §10).

## Kapcsolódó oldalak
