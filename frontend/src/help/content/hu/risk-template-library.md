---
key: risk-template-library
title: Kockázati sablontár
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Kockázati sablontár revíziókkal kezeli a bérlő egészében használt kockázatértékelési sablonokat (munkatípus, veszélyek, alapértelmezett intézkedések, kötelező védelmi gátak, SMS- és követelményforrások), valamint a kockázati módszereket (mátrix). Arra a kérdésre válaszol: „Melyik közzétett sablon vonatkozik erre a munkatípusra, és melyik revízió hatályos?” Az oldal **csak fejlesztési cél**: az itteni közzététel rögzített HSEQ emberi rekord, nem engedély, nem szakértői kockázatelfogadás és nem HSE-kapu teljesítése (13.03 §4).

## Ki használja
- **DPA / HSEQ:** a kockázatkezelés részeként előkészítik, felülvizsgálják és közzéteszik a sablonrevíziókat, a védelmi gátakat és a kockázati mátrixot (13.03 §4).
- **Kapitány, Vontatóhajó-kapitány és személyzet:** elolvassák a közzétett sablonokat, és **Személyzeti javaslatok** formájában javaslatot küldenek egy sablonhoz.

## Mielőtt elkezdené
- Az olvasáshoz sablon-olvasási jogosultság szükséges. A sablonok írásához és közzétételéhez bérlőszintű hatókör, valamint sablonírási vagy -közzétételi jogosultság kell. Javaslat küldéséhez csak sablonjavaslati jogosultság szükséges.
- A kockázati módszer létrehozása és közzététele külön jogosultság, és bérlőszintű hatókört igényel.
- A kockázati parancsokhoz aktuális online kapcsolat szükséges; offline semmi nem kerül sorba és semmi nem kerül jóváhagyásra.

## Lépésről lépésre
1. Tekintse meg a sablonokat a listában. Minden sor mutatja az állapotot, az **Aktuális** revíziót és a **Suggestions** számát. Nyisson meg egy sablont.
2. Új sablonhoz nyomja meg az **Új sablon** gombot. Töltse ki a **Sablonkód**, **Cím**, **Munka típusa**, **Hajó / vontató hatókör**, **Jóváhagyás felelőse**, **Módosítás oka**, **Required barriers**, **SMS / requirement sources** és **Veszélyek** mezőket, majd nyomja meg a **Piszkozat mentése** gombot.
3. Egy piszkozat revízión nyomja meg a **Beküldés felülvizsgálatra** gombot. A hiányzó tételek akadályozó tételként jelennek meg.
4. A közzétevő megadja a **Hatályos ettől** dátumot és egy legalább 10 karakteres **Indok** szöveget, majd megnyomja a **Közzététel (HSEQ emberi rekord)** vagy a **Visszaküldés** gombot.
5. Egy közzétett sablon módosításához írjon indokot, és nyomja meg az **Új revízió indítása** gombot; a régebbi revízió nem változik.
6. A személyzet a **Javaslat küldése** részben megírja és elküldi a javaslatát. Egy jogosult személy az **Elfogadás** vagy az **Elutasítás** gombbal és indokkal vizsgálja felül.
7. A **Kockázati módszerek (mátrix)** részben az **Új módszer** gombbal adja meg a **Módszerkód**, a **Likelihood codes**, a **Severity codes** és a **Matrix cells** adatokat; egy másik személy teszi közzé a **Módszer közzététele** gombbal.

## Mit jelentenek az állapotok
- **Piszkozat / Felülvizsgálat alatt / Visszaküldve / Közzétéve / Felváltva:** a sablonrevízió életciklusa.
- **Aktuális / Ütemezve / Nem hatályos:** a hatályossági nézet. Az a legutóbb közzétett revízió, amelynek hatálybalépési dátuma elérkezett, **Aktuális**; a jövőbeli dátumú **Ütemezve** állapotú. Egy jövőbeli revízió soha nem váltja fel idő előtt a régebbit.
- **Ismeretlen:** a mező nincs kitöltve; egy üres érték soha nem töltődik ki alapértelmezett értékkel.
- **No published risk method: ratings stay not assessed.** (Nincs közzétett kockázati módszer: a besorolások értékelés nélkül maradnak.) A rendszer nem kínál alapértelmezett mátrixot vagy küszöböt.

## Amit nem tehet meg / figyelem
- Egy revízió szerzője vagy beküldője nem teheti közzé ugyanazt a revíziót; a közzététel külön emberi rekord (feladatelkülönítés).
- A közzététel nem engedély, nem szakértői kockázatelfogadás és nem jóváhagyott éles katalógus.
- A sablon csak **másolatként** kerül egy kockázatértékelésbe; nem tartalmaz besorolást, aláírást, résztvevői megerősítést, jóváhagyást vagy tudomásulvételt. Ha egy sablon megváltozik, a meglévő értékelések nem változnak meg maguktól.
- Egy közzétett revíziót soha nem szerkesztenek; a módosításához indítson új revíziót.

## Ha valami nem sikerül
- **Zárolva, amíg a felsorolt tételek el nem készülnek.** Teljesítse az akadályozó tételeket, és küldje be újra.
- **This record changed elsewhere.** Nyomja meg az **Újratöltés** gombot az aktuális revízió megtekintéséhez.
- **Nincs jogosultsága ehhez a művelethez (feladatelkülönítés vagy hatókör).** A közzétételt egy másik jogosult személynek kell elvégeznie, aki nem szerzője a revíziónak; ha jogosultság hiányzik, forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **Ez a művelet már nem érvényes a jelenlegi állapotra. Töltse újra.** A revízió állapota megváltozott.

## Kapcsolódó oldalak
