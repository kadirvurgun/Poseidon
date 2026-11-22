---
key: certificate-command-center
title: Bizonyítványközpont
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: [1, 2]
reviewed: false
---
## Mire szolgál ez az oldal
A Bizonyítványközpont a kiválasztott hajó bizonyítvány-**nyilatkozatait** listázza a bejelentett naptári állapotukkal együtt. Arra a kérdésre válaszol: „Mely bizonyítványok járnak le hamarosan vagy közelednek szemleablakhoz ezen a hajón, és mely rekordok várnak még emberi felülvizsgálatra?” A megjelenített dátumok nyilatkozatok. Az oldal soha nem dönti el, hogy egy bizonyítvány érvényes vagy ellenőrzött (13.03 §1, §8).

## Ki használja
- **Kapitány, gépüzemvezető / műszaki felügyelő:** figyeli a hajó bizonyítványnaptárát, új bizonyítványpiszkozatokat rögzít, és elindítja a megújítás előkészítését.
- **Szabályozási / megfelelőségbiztosítási felhasználó:** ellenőrzi, hogy a nyilatkozatok hivatalos, verziózott forráson alapulnak-e, és rögzíti egy kijelölt auditor állítását (13.03 §8).
- A csak olvasási hozzáféréssel rendelkező felhasználók csak a listát látják, és nem módosíthatják.

## Mielőtt elkezdené
- A megtekintéshez bizonyítványolvasási jogosultság szükséges. Piszkozatok rögzítéséhez bizonyítványírási jogosultság és az adott hajóra vonatkozó szerkesztési hatókör kell; dokumentumjavaslatok kinyeréséhez fájlolvasási és kinyerési jogosultság; emberi állítás rögzítéséhez címtárírási jogosultság, az ellenőrzésrögzítési jogosultság, valamint érvényes **Auditor** kinevezés erre a hajóra és bizonyítványra. A védett személyzeti nyilatkozatokhoz ezenfelül személyzetolvasási, személyzeti bizonyítvány-olvasási, személyzeti azonosság-olvasási és fájlolvasási jogosultság is szükséges.
- Először válassza ki a megfelelő hajót: nyomja meg a **Hajó kiválasztása** listában (13.03 §1).
- Az oldalhoz élő, online olvasás szükséges. Offline állapotban a „Current certificate sources unavailable offline or hidden.” üzenet jelenik meg, és a rendszer nem következtet offline jogosultságra.

## Lépésről lépésre
1. Nyissa meg a hajót a **Hajó kiválasztása** listából.
2. A **Bizonyítványkör** segítségével válassza a **Hajóbizonyítványok** lehetőséget, vagy – ha rendelkezik a jogosultsággal – a **Védett személyzeti nyilatkozatok** lehetőséget. A listát a **Megadott naptári szűrő** segítségével szűkítheti.
3. Egy rekordot a sorára kattintva nyithat meg. A sorokon a **Nem ellenőrzött bizonyítványnyilatkozat** címke látható.
4. Új rekordhoz nyomja meg az **Új bizonyítványpiszkozat** gombot. Töltse ki a forrásdokumentum alapján a **Dokumentum neve**, a **Bejelentett szabályozási alap**, a **Bejelentett lobogó**, a szemleablak és a **Megadott elérhetőség** mezőt, majd nyomja meg a **Nem ellenőrzött piszkozat mentése** gombot.
5. A kapcsolt dokumentumból származó javaslatokhoz nyomja meg a **Kinyerés kapcsolt dokumentumból** gombot a **Dokumentum tényeinek felülvizsgálata** részben. Vessen össze minden javaslatot a forrással, jelölje be az **Ezt a tényt összevetettem a forrással** és az **Ellenőriztem, hogy ez a dokumentum ehhez a hajóhoz és bizonyítványhoz tartozik** jelölőnégyzetet, majd nyomja meg a **Kiválasztott tények megerősítése és piszkozat mentése** gombot.
6. Kijelölt auditorként az **Emberi állítás rekordja** részben töltse ki a **Tényleges hozzárendelés kiválasztása** és a **Rögzített állítás kiválasztása** mezőt, írja be az indokot, majd nyomja meg az **Értékeletlen állítás rögzítése** gombot.
7. Megújítás indításához használja a rekordon a **Bizonyítványhoz kapcsolódó munka** alatti **Megújítás előkészítése** lehetőséget. Ez a [Bizonyítvány megújítása](/app/renewal-workflow) folyamatba visz.

## Mit jelentenek az állapotok
- **Bejelentett lejárat <90 nap / <60 nap / <30 nap / A megadott lejárat elmúlt:** a bejelentett lejárati dátumból származó naptári megfigyelés. Nem az érvényességről szóló döntés.
- **A megadott szemleidőablak nyitott / Az esedékességi időablakon túl:** a bejelentett szemleablakhoz viszonyított helyzet.
- **Naptári felülvizsgálat szükséges:** egy dátum hiányzik, nem egyértelmű vagy ellentmondásos. A rekord addig nem kész, amíg egy személy össze nem veti a forrásdokumentummal.
- **Ismeretlen:** az értéket nem jelentették be. Az ismeretlen érték soha nem jelenti azt, hogy „nem hiányzik” vagy „megfelelő”; felülvizsgálatot igényel.
- **Alacsony megbízhatóság · felülvizsgálat / Nem egyértelmű · ellenőrizze / Felülvizsgálat szükséges:** egy személy által még meg nem erősített dokumentumjavaslat. A megbízhatósági pontszám nem ellenőrzés.
- „Csak ez az oldal. A naptári megfigyelések elkülönülnek a minősítéstől; ezek nem flottaszintű készenléti összesítések.”

## Amit nem tehet meg / figyelem
- A piszkozat mentése nem ellenőrzi a bizonyítványt. A rekord írása, a dokumentumjavaslatok megerősítése és az auditor állítása külön lépések; Befejezett ≠ Ellenőrzött.
- Az automatikus kinyerés csak javasol. A hiányzó értékek ismeretlenek maradnak, és csak az Ön által kiválasztott és javított tények frissítik a piszkozatot.
- Az adminisztrátori szerepkör önmagában nem jogosít fel emberi állítás rögzítésére; érvényes kinevezés és a forrásfájl szükséges.
- Egy mentett bizonyítvány soha nem változik meg csendben. Minden mentés új revíziót hoz létre, a régebbi revíziók pedig megmaradnak a **Rögzített revízióelőzmények** között (13.03 §1).
- Az oldal nem ad osztályozótársasági, lobogó szerinti vagy hatósági jóváhagyást, és nem dönt az útra való alkalmasságról. Ez a döntés a felhatalmazott személyeké.

## Ha valami nem sikerül
- **A bizonyítványforrás vagy hozzáférés nem érhető el. Nem következik belőle érvényes, üres vagy ellenőrzött állomány.** Nyomja meg a **Forrásolvasás újrapróbálása** gombot. Ha a hiba továbbra is fennáll, ellenőrizze szerepkörét, a hajót és az objektumhatókört; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **Outcome unknown.** Tartsa meg az eredeti parancsot, és küldje el újra ugyanazt a kérést a **Pontos bizonyítványparancs megismétlése** gombbal.
- **Current source changed.** A helyi mezői megmaradnak; használja a **Vissza a rekordokhoz** gombot, olvassa be újra az aktuális rekordot, és végezze el ismét a módosítást.
- **Automatic reading is not available for this document:** adja meg az értékeket kézzel a forrásdokumentum alapján.
- Ha egy rekord nem ellenőrizhető, vizsgálja meg a bizonyítékát és a forrás eredetét, és utalja az ellenőrzőhöz (13.03 §10).

## Kapcsolódó oldalak
