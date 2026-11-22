---
key: critical-spares
title: Kritikus alkatrészek és igénylések
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A kritikus alkatrészek és igénylések oldal a kiválasztott hajó **bejelentett készletét, minimumát és maximumát, alacsonykészlet-riasztásait és műszaki igénylési javaslatait** mutatja. Arra a kérdésre válaszol: „Melyik kritikus alkatrész érte el a minimumát, hány nyitott munkarendelés hivatkozik rá, és javasoltak-e és döntöttek-e már műszaki igénylésről?” A darabszámok emberi nyilatkozatok, és nincsenek raktárral összevetve ellenőrizve.

## Ki használja
- **Gépüzemvezető / műszaki felügyelő:** a kritikus alkatrészek minimális küszöbértékeinek követésére és műszaki igénylés indítására (13.03 §5). Az igénylést javasló és az azt jóváhagyó személy két különböző ember.

## Mielőtt elkezdené
- A megtekintéshez `spare.read` szükséges. Alkatrészek meghatározásához és leltárak rögzítéséhez `spare.write`, igénylés javaslatához `requisition.propose`, a döntéshez pedig `requisition.approve` szükséges.
- A javaslatokhoz és döntésekhez bejelentkezett emberi felhasználó szükséges.
- Válassza ki a megfelelő hajót a **Hajó** listából.

## Lépésről lépésre
1. Válassza ki a hajót. Megnyílik a **Kritikus alkatrészek** nézet. Olvassa el felül az **Alacsony készlet** és a **Felülvizsgálat szükséges** darabszámát.
2. A táblázat minden sorában olvassa el az **Alkatrész kritikussága**, a **Berendezés kritikussága**, a **Készletállapot**, a **Várható**, a **Min. / max.** és a **Nyitott munkarendelések** értékét.
3. Új alkatrészhez nyomja meg az **Új kritikus alkatrész** gombot, meglévőhöz a **Szerkesztés** gombot. Adja meg a **Cikkszám**, a **Név**, a **Mértékegység**, a **Minimális készlet** és a **Maximális készlet** értékét. Ha a küszöbértékekről még nem döntöttek, hagyja őket üresen.
4. Leltár rögzítéséhez jelölje be az **Új leltár rögzítése** lehetőséget, töltse ki a **Megszámolt mennyiség** mezőt, és nyomja meg a **Mentés** gombot.
5. Egy **Alacsony készlet** állapotú, nyitott igénylés nélküli soron nyomja meg az **Igénylés javaslata** gombot. A javasolt mennyiséget a szerver számítja ki (maximum mínusz várható készlet). Ha nincs maximum beállítva, a rendszer nem javasol mennyiséget, és a jóváhagyónak kell megadnia a **Jóváhagyott mennyiség** értékét.
6. A **Műszaki igénylések** nézetben egy felhatalmazott személy, aki nem maga tette a javaslatot, ellenőrzi a **Jóváhagyott mennyiség** értékét, és megnyomja a **Jóváhagyás ERP-exportra** gombot, vagy kiválaszt egy **Elutasítás oka** értéket, és megnyomja az **Elutasítás** gombot.
7. Egy jóváhagyott igénylésen nyissa meg az **ERP-exporthivatkozás** részt a hivatkozás megtekintéséhez.

## Mit jelentenek az állapotok
- **Alacsony készlet:** a várható készlet (az utolsó bejelentett leltár mínusz az azóta benyújtott munkarendeléseken rögzített alkatrészek) a minimumon vagy alatta van. Ezen a soron kínálja fel a rendszer az igénylési javaslatot.
- **Minimum felett (bejelentett):** a várható készlet a minimum felett van; a leltár nincs ellenőrizve.
- **Minimum nincs beállítva:** nem adtak meg küszöbértéket. Az üres küszöbértéket a rendszer felülvizsgálatot igénylőként jelzi, soha nem rendben lévőként.
- **Készlet ismeretlen / Fogyasztás tisztázatlan:** a készletet nem lehetett kiszámítani. Az ismeretlen nem jelenti azt, hogy „elegendő a készlet”.
- **Javasolva / Jóváhagyva ERP-exportra / Elutasítva:** a műszaki igénylés állapota.
- **Kritikus (bejelentett) / Nem kritikus (bejelentett) / Ismeretlen:** az alkatrész kritikussága nyilatkozat.

## Amit nem tehet meg / figyelem
- Nem dönthet olyan igénylésről, amelyet Ön javasolt. Az oldal ezt írja: „Ön javasolta ezt; másnak kell döntenie.”
- A javaslat és a jóváhagyás külön emberi cselekmények. A jóváhagyás csak ERP-exporthivatkozást hoz létre; itt nem kezel a rendszer megrendelést, szállítót vagy árat.
- Az oldalon nincs külön újrarendelési küszöb mező; az alacsony készletet a minimális készlethez viszonyítva értékeli.
- Offline állapotban az adatok nem frissíthetők, és semmilyen művelet nem hajtható végre.

## Ha valami nem sikerül
- **The record changed:** használja az **Újra** gombot az oldal frissítéséhez, és próbálja újra.
- **Outcome unknown:** az **Ugyanazon parancs megismétlése** ugyanazt a parancsot küldi el.
- **The request was rejected by a rule:** ellenőrizze a készletállapotot és a nyitott igénylés adatait.
- **You do not have permission:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
