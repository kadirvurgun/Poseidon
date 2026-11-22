---
key: role-uat
title: Szerepkörös UAT-rekordok
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Szerepkörös UAT-rekordok oldal **emberi bizonyítékként** rögzíti a hajós és vontatós szerepkörökkel végzett felhasználói elfogadási tesztek (UAT) futásait, azok forgatókönyv-eredményeit és megállapításait, és összeveti őket a megállapodott küszöbbel. Arra a kérdésre válaszol: „Megfelel-e az igazolt UAT-bizonyíték minden szerepkörre és forgatókönyvre az emberek által megállapodott küszöbnek?” A felső értesítés érvényes: „Csak emberi UAT-bizonyítékot rögzít. A rendszer soha nem fogadja el az UAT-ot és soha nem dönt pilot GO-ról; az automatizált teszteredmények mellékletek, nem PASS.”

## Ki használja
- **UAT-moderátor:** megnyit egy futást, rögzíti a résztvevő megfigyelt forgatókönyv-eredményeit és megállapításait, és igazolásra küldi a futást. A hajós résztvevői szerepkörök közé tartozik a Kapitány, a DPA, a Gépüzemvezető és a Személyzet (13.03 §2, §4, §5, §6).
- **Igazoló:** a moderátortól eltérő személy igazolja a futást.
- **UAT-felelősök / Termékgazda:** rögzítik a megállapodott küszöböt, és az eredményt a **Pilot go/no-go áttekintés** oldalon használják.

## Mielőtt elkezdené
- A megtekintéshez bérlőszintű olvasási és UAT-olvasási jogosultság szükséges. A futások és eredmények rögzítéséhez, a küszöb rögzítéséhez és az igazoláshoz egyenként saját jogosultság kell.
- Az UAT-felelősöknek előzetesen meg kell állapodniuk a küszöbben; a rendszer soha nem állít be küszöböt.
- Válasszon álnevet a résztvevőnek. Soha ne használjon nevet, e-mail-címet vagy személyzeti számot.
- A rögzítéshez élő kapcsolat szükséges; offline állapotban ki van kapcsolva.

## Lépésről lépésre
1. Az **Üzemmód** gombokkal válassza a **Hajó** vagy a **Vontatóhajó** lehetőséget.
2. Az **UAT-bizonyíték a megállapodott küszöbhöz képest** részben tekintse át az állapotot, a **Nyitott blokkolók**, a **Nyitott jelentős megállapítások**, az **Igazolt futások** és az **Igazolásra vár** számokat, valamint a forgatókönyv-táblázatot.
3. Ha nincs küszöb, a **Megállapodott küszöb rögzítése** részben töltse ki a **Minimális forgatókönyv-sikerarány**, a **Nyitott jelentős megállapítások maximuma**, a **Tervezési megfelelés**, a **Megállapodás hivatkozása** és az **Indoklás** mezőt, majd nyomja meg a **Rögzítés** gombot.
4. Az **UAT-futások** részben az **Új UAT-futás** űrlapon adja meg a **Szerepkör**, a **Környezet**, a **Build commit** és a **Résztvevő álneve** mezőt, majd nyomja meg a **Rögzítés** gombot.
5. Nyissa meg a futást. Az **Eredmény rögzítése** résznél válassza ki a **Forgatókönyv** és az **Eredmény** értékét; ha releváns, adja meg a **Segítséget kapott** és az **Automatizált bizonyíték hivatkozása** mezőt. Probléma esetén használja a **Megállapítás rögzítése** részt a **Súlyosság** és a **Megállapítás összefoglalója** mezővel.
6. Ha rögzítette a forgatókönyveket, nyomja meg a **Futás beküldése igazolásra** gombot (csak a moderátor). Az eredmény nélküli forgatókönyvek **Nem futott** állapotúnak számítanak. A moderátortól eltérő személy a **Futás igazolása** részben megír egy **Igazoló nyilatkozat** szöveget, és megnyomja a **Rögzítés** gombot.
7. A **Nyitott megállapítások** részben zárjon le egy megállapítást a **Megoldás** és a **Megoldás megjegyzése** megadásával, majd nyomja meg a **Lezárás** gombot.

## Mit jelentenek az állapotok
- **Küszöb nincs beállítva:** „Nincs rögzítve emberek által megállapodott küszöb. Az UAT addig nem teljesülhet, amíg nem rögzítenek egyet.”
- **A feltételek nem teljesültek / Hiányos bizonyíték:** az igazolt bizonyíték nem éri el a küszöböt, vagy nem minden szerepkör és forgatókönyv van lefedve.
- **Feltételek teljesültek; emberi döntésre vár:** a lehető legjobb állapot. Ez nem UAT-elfogadás és nem pilot GO; a döntés emberi.
- **Nem futott:** a forgatókönyvhöz nincs eredmény. Egy le nem futtatott forgatókönyv soha nem számít sikeresnek.
- **Nyitott / Beküldve / Igazolva:** a futás állapota. Csak az igazolt futások számítanak bele a küszöbbe.
- **Sikeres / Sikertelen / Blokkolva / Nem futott:** a résztvevő megfigyelt forgatókönyv-eredménye.
- **Blokkoló / Jelentős / Kisebb:** a megállapítás súlyossága. Egy nyitott blokkoló miatt a kapu nem teljesül.
- **Fiktív adatok:** szintetikus mintarekord; nem valós UAT-bizonyíték.

## Amit nem tehet meg / figyelem
- Nem igazolhat olyan futást, amelyet Ön moderált: „Az igazolást a moderátortól eltérő embernek kell rögzítenie.”
- Nem zárhat le olyan megállapítást, amelyet Ön jelentett. Egy blokkoló csak **Megoldva (javítva és újratesztelve)** állapottal zárható le; a **Kockázat elfogadva** nem választható.
- Egy automatizált (Playwright) eredmény csak csatolt bizonyíték; az eredmény a résztvevő megfigyelt eredménye.
- A rendszer soha nem fogadja el az UAT-ot, soha nem állít be küszöböt, és soha nem gyárt igazolásokat vagy aláírásokat.

## Ha valami nem sikerül
- **Nincs hozzáférése ezekhez a rekordokhoz.** Ellenőrizze szerepkörét és bérlői hatókörét, és forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **A rekord nem található a bérlőjében.** Ellenőrizze a futás hivatkozását és a kiválasztott módot.
- **A rekord megváltozott vagy már felhasználták. Töltse újra.** Nyissa meg az aktuális állapotot az **Újratöltés** gombbal.
- **Az eredmény bizonytalan. Az újrapróbálás ugyanazt a parancsot küldi.** Az **Újra** gombbal biztonságosan újraküldheti ugyanazt a parancsot.
- **A szabályok elutasították.** Például a forgatókönyv nem tartozik a futás szerepköréhez, vagy egy blokkoló nem zárható le elfogadott kockázatként.
- **Nem engedélyezett.** Például Ön moderálta ezt a futást, és nem igazolhatja, vagy Ön jelentette ezt a megállapítást, és nem zárhatja le.
- **Nincs kapcsolat. A rögzítés a kapcsolat helyreállásáig ki van kapcsolva.** Amikor a kapcsolat helyreáll, nyomja meg a **Frissítés** gombot.

## Kapcsolódó oldalak
