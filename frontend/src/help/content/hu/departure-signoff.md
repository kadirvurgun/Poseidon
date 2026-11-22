---
key: departure-signoff
title: Indulási készenlét és kapitányi döntés
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: [1, 2, 3, 4, 5, 6, 7, 9]
reviewed: false
---
## Mire szolgál ez az oldal
Ez az oldal **tanácsadó jellegű készenléti értékelést** mutat a kiválasztott hajóra és a tervezett indulási időszakra, és rögzíti a kinevezett kapitány döntését. Arra a kérdésre válaszol: „Mit mondanak erre az időszakra a legénységi, műszaki, jogszabályi, vészhelyzeti és biztonsági eseményekre vonatkozó források, és miért?” Az indulás engedélyezéséről, visszatartásáról vagy leállításáról szóló döntés a **Kapitány** hatásköre (13.03 §2). A rendszer soha nem engedélyez indulást; a rögzített döntés nem indulási engedély.

## Ki használja
- **Kapitány:** az értékelés és a „Miért” lista áttekintésére, majd az Engedélyez, a Visszatart vagy az Elutasít döntés indoklással való rögzítésére (13.03 §2).
- **Parti üzemeltetés / műszaki felügyelő:** az értékelés és a rögzített kapitányi döntések csak olvasható követésére. A parti személyzet nem rögzíthet döntést a kapitány helyett.

## Mielőtt elkezdené
- A felső kontextussávban ellenőrizze, hogy a **megfelelő vállalat** van-e kiválasztva. Az oldal csak az Ön hatókörébe tartozó hajókat listázza (13.03 §1).
- Az oldal megtekintéséhez indulási készenlét-olvasási hozzáférés szükséges.
- A döntési jogosultság önmagában nem elég egy döntés rögzítéséhez: ehhez a hajóhoz **érvényes kapitányi kinevezés** is szükséges. Ennek hiányában az oldal ezt írja: „Nincs érvényes kapitányi kinevezése ehhez a hajóhoz. A döntések csak olvashatók.”

## Lépésről lépésre
1. Válassza ki a hajót a „Válasszon hajót” listából.
2. A **Tervezett indulási időszak** részben töltse ki az „Indulás dátuma” és az „Út vége” mezőt, és nyomja meg a **Készenlét értékelése** gombot.
3. Az **Értékelés** sorban olvassa el az állapotjelvényt, az „Aktuális” vagy „Előrejelzés” módot, valamint az „Értékelve” és az „Érvényes eddig” értéket.
4. A kategóriatáblázatban ellenőrizze a Legénység, a Műszaki, a Jogszabályi, a Vészhelyzeti és a Biztonsági események eredményeit.
5. Olvassa el a **Miért** listát. Minden sor egy nem megfelelt megfigyelést mutat az okkódjával és a forrásával. Egy váratlan sort a forrás saját oldalán vizsgáljon felül.
6. Ha az értékelés nem egy aktuális, Kész állapotú értékelés, olvassa el a **Miért nem érhető el az Engedélyez** részt. Ez jelzi, ha az időszak előrejelzés, felsorolja a még nem megfelelt kategóriákat az eredményeikkel, és elmagyarázza, melyik rekord zárná le a Vészhelyzeti (még nincs 61.02 gyakorlatforrás csatlakoztatva) és a Jogszabályi (nincs meghatározva a kötelező bizonyítványok köre) forráshiányt. Csak a szerver kategóriáit magyarázza; a kaput nem változtatja meg.
7. A **Kapitányi döntések** alatt láthatja a korábban rögzített döntéseket és azok érvényességét.
8. Ha Ön a kapitány, a **Kapitányi döntés rögzítése** részben válassza az Engedélyez, a Visszatart vagy az Elutasít lehetőséget, a „Kinevezés” mezőben válassza ki a kinevezését, írjon „Indoklás” szöveget (10–2000 karakter), és nyomja meg a **Rögzítés** gombot.
9. Az értékelés ismételt beolvasásához használja a **Frissítés** gombot.

## Mit jelentenek az állapotok
- **Kész:** egyik rögzített forrásnak sincs nem megfelelt megfigyelése. Ez csak tájékoztatás; az indulási döntés továbbra is a kapitányé.
- **Feltételes:** legalább egy forrás feltételt jelez. Ennél az értékelésnél az Engedélyez nem érhető el.
- **Nem kész:** legalább egy forrás „Nem felelt meg” eredményt ad. Az, hogy más kategóriák megfeleltek, ezt nem ellensúlyozza.
- **Felülvizsgálat szükséges:** legalább egy bemenet „Ismeretlen”. Egy felhatalmazott személynek meg kell vizsgálnia.
- **Megfelelt / Feltétel / Nem felelt meg / Ismeretlen:** a kategória eredménye a kategóriatáblázatban. Az **Ismeretlen** azt jelenti, hogy a forrás nem olvasható, vagy nincs adata; nem jelenti azt, hogy „Megfelelt” vagy „Kész”. Ebben a verzióban a Vészhelyzeti kategóriához nincs vészhelyzeti felkészültségi forrás csatlakoztatva, ezért az a sor Ismeretlen értéket mutat. A Jogszabályi kategória akkor is Ismeretlen maradhat, ha minden rögzített bizonyítvány érvényes, mert egyetlen forrás sem határozza meg a hajóra kötelező bizonyítványok körét. A Jogszabályi kategória az adatmegbízhatósági jelzéseket (DATA_TRUST_*) is tartalmazza; adatmegbízhatósági olvasási hozzáférés nélkül a Jogszabályi kategória Ismeretlen értéket mutat.
- **Előrejelzés:** az időszak nem tartalmazza a mai napot. Ez csak tervezési előrejelzés, és nem engedélyezhető.
- **Aktuális / Elavult — a bemenetek változtak:** egy rögzített döntés érvényessége. Ha a bemenetek megváltoznak, a korábbi döntés elavulttá válik, de nem törlődik.

## Amit nem tehet meg / figyelem
- A rendszer soha nem engedélyez indulást, és egyetlen állapotot sem mutat indulási engedélyként. A rögzített döntés a kinevezett kapitány saját emberi feljegyzése.
- Az Engedélyez csak aktuális READY értékelésnél rögzíthető; minden kategóriának meg kell felelnie. Mivel ebben a verzióban a Vészhelyzeti kategória Ismeretlen marad, az értékelés nem érheti el a Kész állapotot, és az Engedélyez nem érhető el; a **Miért nem érhető el az Engedélyez** megmutatja, miért. Az engedélyezési döntés Kész állapotban is a kapitányé; a rendszer semmit nem hagy jóvá. „A Visszatart és az Elutasít indoklással mindig rögzíthető.”
- Minden döntéshez indoklás szükséges. A rögzített döntések nem módosulnak; egy új döntés új rekord.
- Nem rögzíthet döntést, amíg az értékelés elavult, vagy amíg Ön offline állapotban van; előbb frissítsen.

## Ha valami nem sikerül
- **You are offline:** az értékelés elavult lehet, és a döntésrögzítés le van tiltva. Frissítsen, amint újra online állapotban van (13.03 §10).
- **You do not have access to departure readiness for this vessel:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz.
- **Unexpected readiness:** nyissa meg a „Miért” listát; ha egy forrás hibásnak tűnik, jelezze a kontroll felelősének vagy a szakterületi szakértőnek.
- **The outcome is uncertain:** az „Újra” ugyanazt a parancsot küldi el. Ne írjon új döntést.
- **The decision history changed / The assessment changed:** használja az „Újratöltés” lehetőséget, vizsgálja felül az aktuális értékelést, majd döntsön.
- **The command was not accepted:** jelentse a támogatásnak képernyőképpel, az időponttal és a lépéseivel.

## Kapcsolódó oldalak
