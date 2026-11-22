---
key: risk-assessment
title: Kockázatértékelés
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Kockázatértékelés oldal egy hajón végzett munkára vagy műveletre vonatkozóan rögzíti a veszélyeket, a lehetséges következményeket, a kockázatcsökkentő intézkedéseket, a kezdeti és a maradó kockázatot, valamint a munkaleállítási feltételeket. Arra a kérdésre válaszol: „A munka veszélyeit és intézkedéseit mérlegelték, felülvizsgálták, és a résztvevők tudomásul vették?” Az oldal **csak fejlesztési cél**: az itteni jóváhagyás rögzített emberi felülvizsgálat, nem engedély, nem szakértői kockázatelfogadás és nem HSE-kapu teljesítése (13.03 §2, §4).

## Ki használja
- **Kapitány és Vontatóhajó-kapitány:** elkészíttetik az értékelést a hajón vagy vontatón végzett munkához, és meghozzák a munkaleállítási döntést. A döntési jogkör náluk marad (13.03 §2, §3).
- **DPA / HSEQ:** felügyelik a kockázatkezelést, és rögzítik a felülvizsgálati döntést (13.03 §4).
- **Személyzet / OOW / Gépész:** résztvevőként saját maguk nevében tudomásul veszik vagy kifogásolják a jóváhagyott értékelést (13.03 §6).

## Mielőtt elkezdené
- Válasszon hajót a kockázatértékelései megnyitásához. Az olvasáshoz kockázat-olvasási jogosultság szükséges; piszkozat írásához kockázat-írási jogosultság és az adott hajóra vonatkozó szerkesztési hatókör kell. A felülvizsgálati döntéshez kockázat-jóváhagyási, a tudomásulvételhez kockázat-tudomásulvételi jogosultság szükséges.
- A besorolásokhoz a bérlőben közzétett kockázati módszer (mátrix) szükséges. Ennek hiányában a besorolások **Nincs értékelve** állapotban maradnak. A mátrixot a bérlő HSEQ-felelőse állítja be, és egy másik személy teszi közzé (Kockázati sablontár oldal).
- A kockázati parancsokhoz aktuális online kapcsolat szükséges; offline semmi nem kerül sorba és semmi nem kerül jóváhagyásra.

## Lépésről lépésre
1. Válassza ki a hajót, és nyisson meg egy értékelést a listából, vagy nyomja meg az **Új értékelés** gombot.
2. Adja meg az **Értékelés száma** mezőt. Szükség esetén adjon meg egy **Munka típusa** értéket, és az **Indítás közzétett sablonból (csak másolat)** segítségével válasszon megfelelő sablont; a sablon csak bemásolódik az új piszkozatba.
3. Töltse ki a **Cím**, az **Üzemeltetési / munka kontextus**, a **Munka típusa**, a **Munka terjedelme** és a **Kockázati módszer** mezőt. A **Munkaleállítás szükséges** mezőnél válassza az **Igen**, a **Nem** vagy az **Ismeretlen** értéket, és szükség esetén írja le a **Stop-work conditions** mezőben a feltételeket.
4. A **Veszély hozzáadása** gombbal minden veszélyhez adja meg a **Munkalépés**, a **Potential consequences**, a **Kezdeti kockázat** és a **Maradó kockázat** értékét (valószínűség, súlyosság, indoklás). Az **Intézkedés hozzáadása** gombbal adja hozzá a **Kockázatcsökkentő intézkedések**, az **Intézkedés felelőse** és a **Barrier references** adatokat.
5. A **Résztvevők** mezőbe soronként egy szereplőazonosítót és szerepkört írjon. Szükség esetén kapcsoljon kapcsolódó rekordokat a **Kapcsolat hozzáadása** gombbal.
6. Nyomja meg a **Piszkozat mentése**, majd a **Beküldés felülvizsgálatra** gombot. Az **Akadályozó tételek** felsorolja, mi hiányzik.
7. A felülvizsgáló legalább 10 karakteres **Indok** megadása után megnyomja a **Jóváhagyás (emberi felülvizsgálati rekord)** vagy a **Visszaküldés** gombot.
8. A jóváhagyás után minden résztvevő saját maga nevében megnyomja a **Tudomásulvétel** gombot, vagy indokkal a **Kifogás** gombot. Ha módosítás szükséges, használja a **Módosítás (új piszkozatot nyit)** gombot.

## Mit jelentenek az állapotok
- **Piszkozat / Felülvizsgálat alatt / Visszaküldve / Jóváhagyva (emberi rekord):** az értékelés életciklusa. A jóváhagyott állapot emberi felülvizsgálati rekord, nem engedély.
- **Nincs értékelve / Módszer nincs beállítva:** nincs besorolás. A rendszer nem használ alapértelmezett mátrixot, szorzást vagy alacsony/közepes/magas küszöböt.
- **Munkaleállítás szükséges — Ismeretlen:** még nem született döntés; soha nem értelmezhető „Nem”-ként.
- **Visszaigazolás jóváhagyás előtt nem alkalmazható / Visszaigazolások függőben / Minden résztvevő visszaigazolt / Vitatott – tisztázatlan:** a résztvevői tudomásulvétel állapota. Egy kifogást soha nem zár le valaki más tudomásulvétele.
- **Rögzítés óta módosult / Nem érhető el:** egy kapcsolt rekord a rögzített revízió után megváltozott, vagy már nem olvasható; vizsgálja felül újra.

## Amit nem tehet meg / figyelem
- A szerző vagy a felülvizsgálati ciklus beküldője nem hagyhatja jóvá ugyanazt az értékelést (feladatelkülönítés).
- Mindenki csak saját maga nevében és csak a pontosan jóváhagyott revíziót veszi tudomásul.
- Az itteni jóváhagyás nem engedély, nem szakértői kockázatelfogadás és nem HSE-kapu. A munka megkezdéséről vagy leállításáról szóló döntés a Kapitánynál vagy a Vontatóhajó-kapitánynál marad.
- A sablonból indított piszkozat nem tartalmaz besorolást, aláírást, résztvevői megerősítést vagy jóváhagyást.
- Offline állapotban az értékelés nem küldhető be, nem hagyható jóvá és nem vehető tudomásul.

## Ha valami nem sikerül
- **Zárolva, amíg a felsorolt tételek el nem készülnek.** Teljesítse az akadályozó tételeket, és küldje be újra.
- **This record changed elsewhere.** Nyomja meg az **Újratöltés** gombot az aktuális revízió megtekintéséhez, majd ismételje meg a műveletet.
- **Nincs jogosultsága ehhez a művelethez (feladatelkülönítés vagy hatókör).** Kérjen meg egy másik jogosult személyt a felülvizsgálatra, vagy ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **A kockázati munkaterület a jelenlegi hozzáféréssel nem érhető el.** Ellenőrizze jogosultságait.
- Ha egy besorolás váratlannak tűnik, ellenőrizze a kockázati módszert és annak revízióját; kérdezze meg az intézkedés felelősét vagy a HSEQ-felelőst (13.03 §10).

## Kapcsolódó oldalak
