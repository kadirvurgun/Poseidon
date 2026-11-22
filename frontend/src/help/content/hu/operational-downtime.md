---
key: operational-downtime
title: Üzemi állásidő
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Ez az oldal egy hajó **bejelentett üzemi megszakításait** (állásidő) rögzíti a forráseredetükkel együtt. Arra a kérdésre válaszol: „Milyen megszakítást jelentettek be ezen a hajón, mikor, milyen okból és melyik hibához vagy munkarendeléshez kapcsolva, és hol tart a felülvizsgálata és lezárása?” Az oldal nem dönt a bérleti díj kiesésére (loss of hire) való jogosultságról, a biztonságos üzemről vagy bármilyen biztonsági engedélyről.

## Ki használja
- **Gépüzemvezető / műszaki felügyelő:** műszaki hiba vagy karbantartás okozta állásidő rögzítésére és a hiba- vagy munkarendelés-előzményekhez kapcsolására (13.03 §5).
- **Kapitány:** az állásidő üzemi hatásának követésére és kinevezés esetén felülvizsgálatára (13.03 §2).
- **Kinevezett felülvizsgáló (kapitány, első tiszt, gépüzemvezető, flottamenedzser, DPA):** egy állásidő-rekord ellenőrzésére, javításra való visszaküldésére vagy lezárására.

## Mielőtt elkezdené
- A felső kontextussávban ellenőrizze, hogy a **megfelelő vállalat** van-e kiválasztva; az oldal csak az Ön hatókörébe tartozó hajókat listázza.
- Az olvasáshoz állásidő-olvasási hozzáférés szükséges; piszkozat megnyitásához írási hozzáférés. A felülvizsgálati műveletekhez ezenfelül felülvizsgálati hozzáférés és erre a hajóra érvényes feladatköri kinevezés szükséges.
- A kapcsolt állásidő megnyitásához egy hiba vagy munkarendelés oldalán használhatja a **Kapcsolt állásidő-piszkozat megnyitása** lehetőséget.

## Lépésről lépésre
1. Válassza ki a hajót a „Hajó kiválasztása” listából. Megnyílik a **Rögzített üzemi megszakítások** lista.
2. Az **Okok keresése ezen az oldalon** segítségével szűkítse az ezen az oldalon lévő rekordokat.
3. Új rekordhoz nyomja meg az **Új állásidő-piszkozat** gombot.
4. Minden mezőnél először válassza ki az érték állapotát: **Hiányzik**, **Ismeretlen** vagy **Megadott érték**. Értéket csak akkor adhat meg, ha a „Megadott érték” van kiválasztva.
5. A „Megszakítás kezdete” és a „Megszakítás vége” mezőbe teljes időbélyeget írjon másodpercekkel és kifejezett UTC-eltolással.
6. Nyomja meg az **Értékeletlen piszkozat mentése** gombot. Hiányos piszkozat is menthető.
7. Az **Állásidő felülvizsgálata és lezárása** részben szükség esetén kapcsoljon igénybizonyítékot a **Bizonyíték kiválasztása** segítségével, majd nyomja meg a **Beküldés felülvizsgálatra** gombot.
8. Ha Ön kinevezett felülvizsgáló, válassza ki a „Kinevezés” értékét, és használja az **Ellenőrzés**, a **Visszaküldés javításra** vagy a **Lezárás** lehetőséget.
9. A korábbi revíziókat a **Módosíthatatlan piszkozat-előzmények** alatt tekintheti át.

## Mit jelentenek az állapotok
- **Piszkozat / Felülvizsgálat szükséges:** a rögzített tartalom értékeletlen. Az okok a „Miért marad ez a piszkozat értékeletlen” alatt találhatók.
- **Nincs beküldve / Nyitott / Felülvizsgálat alatt:** a rekord életciklus-állapota.
- **Ellenőrizve (ember):** a rögzítőtől eltérő kinevezett személy ellenőrizte. A Befejezett nem Ellenőrzött (13.03 §1).
- **Lezárva:** az állásidőt lezárták. Az ellenőrzött lezárás nem biztonsági engedély.
- **Ismeretlen:** az értéket nem jelentették be, vagy nem határozható meg. Az ismeretlen nem jelenti azt, hogy „Nem” vagy „Nem alkalmazható”. Emiatt a loss of hire és a lezárás „Ismeretlen” marad.
- **Loss of Hire jelölt:** csak jelölt; nem állapít meg jogosultságot vagy összeget.
- **Rögzített időtartam:** értékeletlen műszaki megfigyelés; nem helyettesíti a bejelentett perceket.

## Amit nem tehet meg / figyelem
- Aki az állásidőt rögzítette, nem ellenőrizheti és nem zárhatja le; ezt egy másik kinevezett személynek kell megtennie.
- A beküldés, az ellenőrzés és a lezárás nem lehetséges, amíg kötelező tények hiányoznak.
- Az igénybizonyíték kapcsolása nem ellenőrzés.
- A felelős forrásból (hiba, munkarendelés) semmilyen leírás, időpont vagy lezárási döntés nem másolódik át; csak a pontos revízióhivatkozás marad meg.
- Offline állapotban az aktuális források nem olvashatók, és nincs offline tárolás; a bejegyzései csak a megnyitott oldalon maradnak meg.

## Ha valami nem sikerül
- **The save outcome is unknown:** az eredeti parancsa védett. Ugyanazzal a fiókkal használja a „Pontos állásidőparancs megismétlése” lehetőséget.
- **The saved draft changed:** valaki más módosította a rekordot. A bejegyzései megmaradnak; az újratöltéshez használja az „Aktuális piszkozat olvasása” lehetőséget, vagy vesse el őket.
- **Downtime records or current access are unavailable:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **The source changed:** új piszkozathoz a felelős forrás aktuális revízióját használja.
- **Command not accepted:** jelentse a támogatásnak képernyőképpel, az időponttal és a lépéseivel.

## Kapcsolódó oldalak
