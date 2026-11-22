---
key: noon-report
title: Déli jelentés
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: [1]
reviewed: false
---
## Mire szolgál ez az oldal
A déli jelentés oldal a kiválasztott hajó **napi déli nyilatkozatait** piszkozatként tárolja a rögzített forrásverziójukkal együtt. Arra a kérdésre válaszol: „Mit jelentettünk be a jelentés időpontjáról, az útról, a pozícióról, a megtett távolságról, az időjárásról, a rakományról, a gépüzemórákról, az üzemanyagról és a ROB-ról, és rögzített-e a kapitány felülvizsgálatot pontosan erre a revízióra?” A mentett piszkozat nem ellenőrzött nyilatkozat. A mentés nem állapít meg kapitányi vagy gépüzemvezetői jóváhagyást, és az oldal nem von le belőle kibocsátási, üzemanyag-biztosítási vagy készenléti következtetést.

## Ki használja
- **A jelentést készítő tiszt (Crew / OOW):** a déli piszkozat kitöltésére és mentésére a megfelelő hajóra és dátumra (13.03 §6).
- **Kapitány:** a mentett jelentés felülvizsgálatára és a felülvizsgálat emberi cselekményként való rögzítésére (13.03 §2). Déli jelentés felülvizsgálatát csak kapitányi feladatkörrel rendelkező kinevezés rögzítheti.

## Mielőtt elkezdené
- A felső kontextussávban válassza ki a **megfelelő vállalatot és hajót**. Ha nincs kiválasztva hajó, az oldal a „Hajó kiválasztása” üzenetet mutatja.
- Az olvasáshoz `daily.noon.read` szükséges. Piszkozatok létrehozásához és mentéséhez `daily.noon.write` és szerkesztési jog szükséges erre a hajóra; ezek nélkül a „Csak olvasási hozzáférés. Szerkesztés és mentés nem engedélyezett.” üzenet jelenik meg.
- Felülvizsgálat rögzítéséhez `daily.noon.write` és szerkesztési jog szükséges erre a hajóra, továbbá `daily.noon.review`, valamint érvényes kapitányi kinevezés pontosan erre a rekordra.

## Lépésről lépésre
1. Válassza ki a hajót. A **Rögzített piszkozatok** lista dátum és revízió szerint mutatja a jelentéseket „Piszkozat · felülvizsgálat szükséges” jelvénnyel. Lapozáshoz használja az **Előző piszkozatok** / **További piszkozatok** gombot.
2. Nyomja meg az **Új piszkozat** gombot az **Új napi piszkozat** megnyitásához, vagy nyisson meg egy listázott jelentést a **Rögzített piszkozat szerkesztése** nézetben.
3. Töltse ki a csoportokat (**Jelentés és út**, **Pozíció és tevékenység**, **Időjárás és rakomány**, **Gépészet, üzemanyag és ROB**). Minden mezőnél válasszon **Adatállapot** értéket: **Ismert**, **Ismeretlen** vagy **Hiányzik**. Hiányos piszkozatok is menthetők.
4. Nyomja meg a **Piszkozat mentése** gombot. Az oldal ezt erősíti meg: „Piszkozat mentve. Az emberi felülvizsgálat és üzemi jóváhagyás külön marad.” Minden mentés új revíziót ad a **Rögzített revízióelőzmények** alatt.
5. Egy mentett jelentésen a kapitány megnyitja az **Emberi felülvizsgálat rögzítése** részt, kiválaszt egy **Jogosult személy kinevezése** és egy **Felülvizsgálati bejegyzés** értéket („Felülvizsgált nyilatkozat rögzítése (értékeletlen)” vagy „Visszaküldés javításra”), megírja a **Felülvizsgálat indoklása** szövegét, majd megnyomja a **Felülvizsgálati bejegyzés rögzítése** gombot.
6. Ha üzemanyagot vagy kikötői eseményt kell rögzíteni, használja a **Mentett üzemi forrás használata**, valamint az **Üzemanyagrekord megnyitása mentett forrásból** vagy a **Kikötői esemény megnyitása mentett forrásból** lehetőséget. A mennyiségek, mértékegységek és jóváhagyások nem másolódnak át.

## Mit jelentenek az állapotok
- **Piszkozat · nyilatkozatok nem ellenőrzöttek / Nem ellenőrzött nyilatkozat:** az értékeket egy személy jelentette be; senki nem ellenőrizte őket.
- **Ismert / Ismeretlen / Hiányzik:** egy mező adatállapota. Az ismeretlen nem jelenti azt, hogy „Nem”, „Kész” vagy „Nem alkalmazható”, és a hiányzó nem nulla.
- **Függőben · felülvizsgálat nincs rögzítve:** ehhez a revízióhoz nem rögzítettek emberi felülvizsgálatot.
- **Emberi metaadatok rögzítve · értékeletlen / Rögzítve · értékeletlen:** a megjelenített revízióhoz létezik kapitányi felülvizsgálati bejegyzés. A felülvizsgálat rögzítése nem állapít meg ellenőrzést, benyújtást vagy kapitányi biztonsági jóváhagyást.
- **Historical revision:** a felülvizsgálati bejegyzés egy régebbi revízióhoz tartozik, és nem vizsgálja felül az aktuális mentett piszkozatot.

## Amit nem tehet meg / figyelem
- A rendszer soha nem vizsgálja felül és nem véglegesíti a jelentést. A felülvizsgálat rögzítése a kinevezett kapitány emberi cselekménye (13.03 §2).
- Aki a napló bármely revízióját írta, nem vizsgálhatja felül. A szerzőnek a rendszer nem kínál fel kinevezést, és az oldal ezt írja: „Ön írta a napló ezen változatát; az áttekintést független áttekintőnek kell rögzítenie.”; a szerver a feladatkörök szétválasztása miatt szintén elutasítja az ilyen felülvizsgálatot. Ha a jelentést a kapitány írta, a felülvizsgálatot akkor is egy felülvizsgálati jogkörrel rendelkező független felülvizsgálónak kell rögzítenie.
- Egy új mentés új revíziót hoz létre; a korábbi felülvizsgálat a saját revízióján marad. Módosítás után vizsgálja felül újra.
- Egy begépelt szabad szöveges hivatkozás nem helyettesítheti a jogosult keresést; hagyja a mezőt Hiányzik vagy Ismeretlen állapotban.
- Offline állapotban csak a kifejezetten besorolt mezők kerülhetnek a titkosított sorba. A felülvizsgálati, jóváhagyási és ellenőrzési mezők ki vannak zárva.

## Ha valami nem sikerül
- **„The record changed. Your inputs are retained.”:** valaki újabb verziót mentett. Új próbálkozás előtt kifejezetten vesse el a módosításokat, és töltse be újra.
- **„Save outcome unknown”:** a mezők zárolva vannak; használja a **Pontos mentett parancs megismétlése** gombot, ne gépelje be újra.
- **„Ehhez a rekordhoz nincs aktuális, forrás által engedélyezett emberi kinevezés.”:** ellenőrizze szerepkörét és hajókontextusát a bérlői adminisztrátorral (13.03 §10).
- **„Emberi ütközésfeloldás szükséges”:** egy offline rögzítés ütközik a szerverrel. Nyissa meg az **Ütközés felülvizsgálata** részt vagy az ütközésközpontot, és döntsön kifejezetten; semmi nem egyesül automatikusan (13.03 §10).
- **Daily sources unavailable or access denied:** használja a **Forrásolvasás újrapróbálása** gombot; ha a hiba továbbra is fennáll, jelentse az időpontot és a lépéseit a támogatásnak.

## Kapcsolódó oldalak
