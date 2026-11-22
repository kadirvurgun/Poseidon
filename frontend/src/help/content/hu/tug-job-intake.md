---
key: tug-job-intake
title: Feladatfelvétel
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A feladatfelvétel a kiválasztott vontatóhajó-kontextusban egy új **vontatási munkaigényt piszkozatként** strukturál. Arra a kérdésre válaszol: „Ehhez a munkához mi a segített hajó, a munka típusa, az időablak, a helyszín és a szükséges erőforrások (vontatóhajók száma, vontatóerő)?” A mentett igény piszkozatnyilatkozat; nem üzemi kiosztás, nem alkalmassági, engedélyezési vagy biztonsági jóváhagyás.

## Ki használja
- **Vontatási műveleti koordinátor / Parti műveletek:** az igény strukturálására, valamint a segített hajó, a munka típusa, az időablak és a szükséges képesség megadására (13.03 §7).

## Mielőtt elkezdené
- A felső kontextussávban válassza ki a **megfelelő vállalatot és vontatóhajót**.
- Vontató-írási hozzáférés szükséges. Enélkül az űrlap nem nyílik meg, és „A hozzáférés nem érhető el.” üzenet jelenik meg.
- Hiányos piszkozatok menthetők. A kötelező üzemi adatokat és a jogosult felülvizsgálatot a beküldés előtt rendezni kell.

## Lépésről lépésre
1. Az **Igény és segített hajó** részben töltse ki az **Igényforrás**, **Ügyfélhivatkozás**, **Segített hajó**, **Segített hajó IMO-ja**, **Feladattípus**, **Prioritás** és **Igénygazda** mezőket. A választás nélküli listák „Ismeretlen” értéken maradnak.
2. Az **Igényelt időpont és hely** részben adja meg az **Igényelt kezdet (UTC)**, az **Igényelt vég (UTC, feltételes)**, a **Műveleti terület**, az **Átvételi pont** és a **Célpont** értékét. Az időpontok UTC szerintiek.
3. Az **Erőforrásigények és megjegyzések** részben adja meg a **Szükséges BP (tonna, pontos tizedes szám)**, a **Szükséges vontatószám**, a **Különleges követelmények** és a **Megjegyzések** értékét.
4. Nyomja meg a **Piszkozat mentése** gombot. Sikeres mentés esetén megnyílik a munka részletes nézete ezzel az üzenettel: „A piszkozat mentve. Az üzemi jóváhagyás továbbra sem érhető el.”
5. A módosításai elvetéséhez használja a **Módosítások elvetése** gombot.
6. A munka részletes nézetében a **Kérés beküldése** gombbal rögzítse az igényt tervezésre. Ha kötelező adat hiányzik, a beküldés blokkolva van, és a hiányzó mezők megjelennek.

## Mit jelentenek az állapotok
- **Piszkozat:** az igény mentve van és szerkeszthető; még nincs beküldve.
- **Beküldve:** az igény tervezésre rögzítve van. Ez nem jóváhagyás, kiosztás vagy készenléti döntés.
- **Visszavonva:** az igény le van zárva; nem indítható újra.
- **Ismeretlen:** a mezőhöz nem választottak értéket. Az Ismeretlen nem alapértelmezett érték, és soha nem helyettesíti a „Nem” vagy a „Nem alkalmazható” értéket.
- **Ellenőrizze ezt az értéket.:** a szerver elutasította ezt a mezőt; javítsa ki, és mentsen újra.

## Amit nem tehet meg / figyelem
- Ez az oldal nem oszt ki vontatóhajót, és soha nem nyilvánítja késznek a munkát. A kiosztási javaslat külön lépés, és soha nem írja felül a Vontatóhajó-kapitány fedélzeti biztonsági döntését (13.03 §7).
- Amíg kötelező adat hiányzik, az igény nem küldhető be; egy hiányos piszkozat ettől még menthető.
- Egy beküldött igény mezői nem módosíthatók ebből az űrlapból; egy visszavont igény nem indítható újra, helyette hozzon létre újat.
- Offline állapotban a piszkozat csak ezen az oldalon marad meg, és nem menthető.

## Ha valami nem sikerül
- **"Save outcome uncertain" üzenet:** a pontos kísérlet megmarad; az újracsatlakozás után az **Újra** gombbal küldje el ugyanazt a parancsot. A helyi bevitelek elvetése nem vonja vissza a már elküldött parancsot.
- **"Conflict" üzenet:** a piszkozata megmarad; új kísérlet előtt vesse el, és töltse újra.
- **Mezők ellenőrzése:** javítsa ki a megjelölt mezőket.
- **Access is not available:** (a hozzáférés nem érhető el) ellenőrizze szerepkörét és vontatóhajó-kontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
