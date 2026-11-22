---
key: deck-daily-log
title: Fedélzeti napi napló
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A fedélzeti napi napló a kiválasztott hajó **fedélzeti őrszolgálati feljegyzéseit** piszkozatként tárolja a rögzített forrásverziójukkal együtt. Arra a kérdésre válaszol: „Mit jelentett be az őrszolgálat erre a dátumra és időszakra a pozícióról, irányról, sebességről, időjárásról, látótávolságról, navigációról, kikötői és rakománymunkáról, ballasztról, fedélzeti munkáról, biztonsági megfigyelésekről és az átadásról, és rögzítettek-e felülvizsgálatot pontosan erre a revízióra?” A mentett bejegyzés nem ellenőrzött nyilatkozat, nem jóváhagyott napló.

## Ki használja
- **Őrszolgálatos tiszt (Crew / OOW):** az őrszolgálati feljegyzés valós idejű, időrendi vezetésére (13.03 §6).
- **Kapitány vagy első tiszt:** a mentett napló felülvizsgálatára és a felülvizsgálat emberi cselekményként való rögzítésére (13.03 §2). Fedélzeti napló felülvizsgálatát csak kapitányi vagy első tiszti feladatkörrel rendelkező kinevezés rögzítheti.

## Mielőtt elkezdené
- A felső kontextussávban válassza ki a **megfelelő vállalatot és hajót**. Ha nincs kiválasztva hajó, az oldal a „Hajó kiválasztása” üzenetet mutatja.
- Az olvasáshoz `daily.deck.read` szükséges. Létrehozáshoz és mentéshez `daily.deck.write` és szerkesztési jog szükséges erre a hajóra; ennek hiányában a „Csak olvasási hozzáférés. Szerkesztés és mentés nem engedélyezett.” üzenet jelenik meg.
- Felülvizsgálat rögzítéséhez `daily.deck.write` és szerkesztési jog szükséges erre a hajóra, továbbá `daily.deck.review`, valamint érvényes kapitányi vagy első tiszti kinevezés pontosan erre a rekordra.

## Lépésről lépésre
1. Válassza ki a hajót. A **Rögzített piszkozatok** dátum és revízió szerint listázza a naplókat „Piszkozat · felülvizsgálat szükséges” jelöléssel.
2. Nyomja meg az **Új piszkozat** gombot egy **Új napi piszkozat** létrehozásához, vagy nyisson meg egy listázott naplót a **Rögzített piszkozat szerkesztése** nézetben.
3. Először töltse ki az **Őrszolgálati kontextus** részt (**Napló dátuma**, **Őrszolgálat / időszak**, **Őrszolgálatos tiszt hivatkozása**). A tisztlista csak a bejelentett naplódátumon rendelkezésre álló személyeket kínálja fel.
4. Töltse ki a **Navigáció és időjárás**, a **Kikötői és rakománytevékenységek** és a **Fedélzeti munka és átadás** részt. Minden mezőnél válasszon az **Ismert**, az **Ismeretlen** vagy a **Hiányzik** lehetőség közül.
5. Az őrszolgálat alatt vagy közvetlenül utána nyomja meg a **Piszkozat mentése** gombot. Minden mentés új revíziót ad a **Rögzített revízióelőzmények** listához; a korábbi revíziók változatlanok maradnak.
6. A kapitány vagy az első tiszt megnyitja az **Emberi felülvizsgálat rögzítése** részt, kiválaszt egy **Jogosult személy kinevezése** és egy **Felülvizsgálati bejegyzés** értéket, megírja a **Felülvizsgálat indoklása** szövegét, és megnyomja a **Felülvizsgálati bejegyzés rögzítése** gombot.

## Mit jelentenek az állapotok
- **Piszkozat · nyilatkozatok nem ellenőrzöttek / Nem ellenőrzött nyilatkozat:** egy személy jelentette be; nincs ellenőrizve.
- **Ismert / Ismeretlen / Hiányzik:** a mező adatállapota. Az ismeretlen nem jelenti azt, hogy „Nem”, „Kész” vagy „Nem alkalmazható”.
- **Függőben · felülvizsgálat nincs rögzítve:** ehhez a revízióhoz nincs emberi felülvizsgálat.
- **Emberi metaadatok rögzítve · értékeletlen:** ehhez a revízióhoz létezik felülvizsgálati bejegyzés. Ez nem állapít meg ellenőrzést, benyújtást vagy kapitányi biztonsági jóváhagyást.
- **Felülvizsgált nyilatkozat rögzítése (értékeletlen) / Visszaküldés javításra:** a két felülvizsgálati bejegyzéstípus, amelyet a felülvizsgáló rögzíthet.
- **Historical revision:** a felülvizsgálat egy régebbi revízióhoz tartozik, nem az aktuális piszkozathoz.

## Amit nem tehet meg / figyelem
- A rendszer soha nem vizsgálja felül és nem véglegesíti a naplót. A felülvizsgálat a kinevezett kapitány vagy első tiszt emberi cselekménye (13.03 §2).
- Aki a napló bármely revízióját írta, nem vizsgálhatja felül. A szerzőnek a rendszer nem kínál fel kinevezést, és az oldal ezt írja: „Ön írta a napló ezen változatát; az áttekintést független áttekintőnek kell rögzítenie.”; a szerver a feladatkörök szétválasztása miatt szintén elutasítja az ilyen felülvizsgálatot.
- A bejegyzéseket valós időben és időrendben vezesse; ne írjon át egy régi őrszolgálatot úgy, hogy újnak tűnjön (13.03 §6).
- Ne módosítsa más felülvizsgált rekordját; egy későbbi mentés új revízió, és új felülvizsgálatot igényel (13.03 §6).
- Az offline rögzítés csak a kifejezetten besorolt mezőkre terjed ki. Az offline ütközést a rendszer soha nem egyesíti automatikusan; ne feltételezze, hogy az Ön rekordja érvényesül (13.03 §6).

## Ha valami nem sikerül
- **„The record changed. Your inputs are retained.”:** új próbálkozás előtt kifejezetten vesse el a módosításokat, és töltse be újra.
- **„Save outcome unknown”:** használja a **Pontos mentett parancs megismétlése** gombot; ne írja be újra a mezőket.
- **Officer reference unavailable:** hagyja a mezőt Hiányzik vagy Ismeretlen állapotban; egy begépelt név nem helyettesítheti a keresést.
- **„Emberi ütközésfeloldás szükséges”:** nyissa meg az **Ütközés felülvizsgálata** részt vagy az ütközésközpontot, és döntsön kifejezetten (13.03 §10).
- **No appointment available:** ellenőrizze szerepkörét és hajókontextusát a bérlői adminisztrátorral (13.03 §10).

## Kapcsolódó oldalak
