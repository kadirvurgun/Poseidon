---
key: voice-event-draft
title: Hangalapú eseménytervezet
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Hangalapú eseménytervezet oldal egy rövid szóbeli vagy begépelt jegyzetből **tervezetjavaslatokat** készít egy biztonsági eseményről szóló bejelentéshez. Arra a kérdésre válaszol: „Helyesen vette-e át a rendszer az esemény típusát, helyszínét, időpontját, a történteket és az azonnali intézkedést abból, amit mondtam vagy írtam?” A javaslatok rögzített helyi szabályokból származnak, mindegyik visszavezethető az Ön saját szavaira, megbízhatósági értékkel. A tervezetet egy személy szerkeszti és erősíti meg; semmi nem kerül automatikusan beküldésre, lezárásra vagy bejelentésre (13.03 §1, §6).

## Ki használja
- **Személyzet / OOW / Gépész és Kapitány:** a helyszínen gyors jegyzetet készítenek, és elindítanak egy biztonsági eseménybejelentést (13.03 §2, §6).
- **DPA / HSEQ:** figyelemmel kísérik a **Mért használhatóság (ez a hajó)** értékeit. Ezek a számok üzemi megfigyelések, nem pontossági állítások.

## Mielőtt elkezdené
- Válasszon a hatókörébe tartozó hajót. Tervezet létrehozásához hangalapú tervezet írási jogosultság és az adott hajóra vonatkozó szerkesztési hatókör szükséges.
- Egy megerősített tervezet biztonsági eseménytervezetként való továbbadásához biztonsági esemény bejelentési jogosultság is szükséges.
- Adatvédelem: a hangfelvétel nem kerül tárolásra. A beszédfelismerés csak ennek a telepítésnek a helyi motorját használja; ha nincs telepítve, gépelje be vagy illessze be a jegyzetet. Kerülje a neveket és a személyes egészségügyi adatokat.
- Offline állapotban semmi nem kerül elküldésre.

## Lépésről lépésre
1. Nyomja meg az **Új hang- vagy szöveges jegyzet** gombot.
2. Ha helyi motor be van állítva, nyomja meg a **Felvétel helyi motorral**, majd a **Leállítás és átírás** gombot. Ellenkező esetben írjon rövid jegyzetet a **Jegyzet szövege** mezőbe: mi, hol, mikor történt, és mit tett.
3. Ellenőrizze **A jegyzet dátuma** értékét. Ezt csak az olyan szavak feloldására használja a rendszer, mint a „ma” vagy a „tegnap”.
4. Nyomja meg a **Tervezetjavaslatok létrehozása** gombot.
5. Ellenőrizzen minden mezőt: **Esemény típusa**, **Mikor történt (helyi idő)**, **Helyszín a hajón**, **Hely részletei**, **Mi történt**, **Azonnali intézkedés**. Minden mező alatt látja a javaslat megbízhatóságát, szabályát és a jegyzet megfelelő részét. Olvassa el a **Szabály-megjegyzések** részt is.
6. Javítsa ki a hibás javaslatokat, és nyomja meg a **Módosítások mentése** gombot. Amikor a **Still required before confirmation:** lista üres, nyomja meg a **Tervezet megerősítése** gombot. A megerősítés azt rögzíti, hogy Ön minden mezőt ellenőrzött; nem küld be és nem zár le eseményt.
7. A megerősítés után, ha létrejött biztonsági eseménytervezet, a **Biztonsági esemény-tervezet megnyitása** hivatkozással lépjen a Biztonsági események és majdnem-balesetek oldalra, és ott egészítse ki és küldje be. Ha még nem jött létre, és rendelkezik a jogosultsággal, használja a **Biztonsági esemény tervezetének létrehozása most** gombot.
8. Ha a jegyzet nem esemény, vagy hibás, válasszon **Indok** értéket, szükség esetén adja meg a **Megjegyzés (Egyébnél kötelező)** mezőt, és nyomja meg a **Tervezet elutasítása** gombot.

## Mit jelentenek az állapotok
- **Tervezet (nem megerősített):** még egyetlen személy sem erősítette meg a javaslatokat.
- **Személy által megerősítve:** a mezőket ellenőrizték; ez nem biztonsági eseménybejelentés, és semmi nem került beküldésre.
- **Elutasítva:** az elutasítás indokát a kiértékeléshez rögzítik.
- **Javasolt, változatlan / Ön módosította / Ön adta meg / Üres:** egy mező eredete. A Javasolt, változatlan olyan javaslat, amelyet egy személynek még ellenőriznie kell.
- **Begépelt vagy beillesztett / Helyi beszédmotor:** a jegyzet bevitelének módja. A helyi motorból származó szöveg szerkesztése begépelt jegyzetté alakítja azt.
- Az olyan szabály-megjegyzések, mint az **Idő nap nélkül; dátum nem lett feltételezve**, hiányzó információt jeleznek; a rendszer nem találgatja ki a hiányzó értéket.
- Egy olyan megjegyzés, mint a **2 missing** (2 hiányzik): azt mutatja, hogy a tervezet hány kötelező mezője üres még a listában.

## Amit nem tehet meg / figyelem
- A javaslatok csak tervezetek; a megbízhatósági érték nem garantálja a helyességet. A megerősítés előtt ellenőrizzen saját maga minden mezőt.
- Ez az oldal nem küld be, nem zár le és nem jelent be biztonsági eseményt. A bejelentést a Biztonsági események és majdnem-balesetek oldalon küldi be.
- Ha bizonyítékot említ, csatolja azt saját maga; az oldal nem csatol bizonyítékot.
- A **Tervezet megerősítése** nem érhető el, amíg vannak nem mentett módosítások; először mentse őket.
- Biztonsági esemény bejelentési jogosultság nélkül egy megerősített tervezetből nem jön létre biztonsági eseménytervezet.

## Ha valami nem sikerül
- **Speech capability unavailable: no local engine is configured.** (A beszédfunkció nem érhető el: nincs helyi motor beállítva.) vagy **Transcription was not completed.** (Az átírás nem fejeződött be.) Gépelje be vagy illessze be a jegyzetet.
- **A mikrofonhozzáférés nem engedélyezett.** Ellenőrizze a böngésző engedélyét, vagy gépelje be a jegyzetet.
- **The record changed meanwhile.** (A rekord időközben megváltozott.) Nyomja meg az **Újratöltés** gombot, és végezze el újra a módosítást.
- **You are offline. Nothing was sent.** (Offline állapotban van. Semmi nem került elküldésre.) Online állapotban használja az **Ugyanazon parancs újrapróbálása** gombot.
- **Nincs jogosultsága ehhez a művelethez.** Ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
