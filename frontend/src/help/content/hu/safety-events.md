---
key: safety-events
title: Biztonsági események és majdnem-balesetek
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: [1]
reviewed: false
---
## Mire szolgál ez az oldal
A Biztonsági események és majdnem-balesetek oldalon gyorsan bejelenthet egy hajón történt biztonsági eseményt, és követheti az azt követő emberi lépéseket: előzetes besorolás, vizsgálat, független felülvizsgálat, helyesbítő és megelőző intézkedések, ellenőrzés, hatékonyság és lezárás. Arra a kérdésre válaszol: „Mi történt, ki foglalkozik vele, és valóban működött-e az intézkedés?” Itt semmi nem dönt automatikusan a súlyosságról, a kiváltó okról vagy a lezárásról; mindegyik külön emberi rekord. Az oldal a Megállapítás → Intézkedés → Ellenőrzés láncot követi, és **a lezárt nem ugyanaz, mint a hatékony** (13.03 §4).

## Ki használja
- **Személyzet / OOW / Gépész és Kapitány:** bejelentik a majdnem-baleseteket, váratlan eseményeket, baleseteket, fáradtsági, környezeti vagy kiberbiztonsági eseményeket (13.03 §2, §6).
- **DPA / HSEQ:** biztonsági összekötőként figyelemmel kísérik a kritikus eseményeket és a lejárt intézkedéseket, és kezelik a vizsgálatot és az intézkedések hatékonyságát (13.03 §4).
- Az előzetes besorolás, a vizsgálat, a felülvizsgálat, a személyazonosság olvasása, az intézkedések írása, az ellenőrzés és a hatékonyság felülvizsgálata külön jogosultságok; minden felhasználó csak azokat a részeket látja, amelyekhez jogosultsága van.

## Mielőtt elkezdené
- Válassza ki a megfelelő hajót (**Válasszon hajót**). Hozzáférés nélkül ezt látja: „Nincs hozzáférése ennek a hajónak a biztonsági eseményeihez.”
- A bejelentéshez bejelentési jogosultság, a lista olvasásához olvasási jogosultság szükséges.
- A **Bizalmas** bejelentéshez és a **Bejelentés elküldése** művelethez kapcsolat szükséges. Offline csak névvel ellátott piszkozatot menthet, és csak előzetesen előkészített eszközön.
- Az időpontot UTC szerint kell megadni.

## Lépésről lépésre
1. Tekintse meg a hajó eseményeit az **Események** listában. A **Csak a saját jelentéseim** segítségével csak a saját bejelentéseit látja.
2. Új rekordhoz nyomja meg az **Esemény bejelentése** gombot. A **Jelentési mód** mezőben válassza a **Névvel** vagy a **Bizalmas** lehetőséget. Nyomon követést igénylő bizalmas bejelentésnél válassza a **Kérek privát nyomonkövetési kódot** lehetőséget.
3. Töltse ki az **Esemény típusa**, **Mikor történt?**, **Művelet**, **Helyszín a hajón**, **Összefoglaló**, **Mi történt**, **Történt azonnali intézkedés?** mezőket, és szükség szerint **Az Ön súlyossági becslése** mezőt. Ha az időpont nem ismert, válassza az **Ismeretlen** értéket, és töltse ki a **Miért ismeretlen az időpont?** mezőt.
4. Hiányos állapotban is használhatja a **Piszkozat mentése** gombot. Amikor a **Beküldés előtt még szükséges** lista üres, nyomja meg a **Bejelentés elküldése** gombot. Egy el nem küldött piszkozat a **Piszkozat visszavonása** gombbal és indokkal visszavonható.
5. Ha nyomonkövetési kódot kapott, az **Az Ön bizalmas nyomonkövetési kódja** mezőben látható kódot azonnal tárolja el bizalmasan, és nyomja meg az **Elmentettem** gombot. A kód csak egyszer jelenik meg. Később a **Bizalmas nyomon követés** alatt adja meg **Nyomonkövetési kód**ként, és nyomja meg a **Megnyitás** gombot a csapat válaszainak elolvasásához.
6. Az előzetes besoroló az **Előzetes besorolás** részben indokkal rögzíti a súlyosságot és a **Vizsgálat szükséges** értéket, majd megnyomja az **Osztályozás rögzítése** gombot, vagy a **Visszaküldés a bejelentőnek** gombbal visszaküldi.
7. A vizsgáló megnyomja a **Vizsgálat megnyitása** gombot, kitölti a **Közreható tényezők**, a **Kiváltó okok**, a **Védelmi gátak** és a **Bizonytalanságok** mezőt, majd megnyomja a **Vizsgálat mentése** és a **Beküldés felülvizsgálatra** gombot. A **Független felülvizsgálat** részben egy másik személy a **Jóváhagyás**, a **Visszaküldés** vagy az **Elutasítás** lehetőséget választja.
8. A **Helyesbítő és megelőző intézkedések** részben az **Intézkedés hozzáadása** gombbal adjon meg felelőst, határidőt és azt a megállapítást, amelyre az intézkedés vonatkozik. A felelős rögzíti a **Teljesítés jelentése** lépést, egy másik személy az **Ellenőrzés** lépést, egy harmadik pedig a **Hatékonyság felülvizsgálata** lépést.
9. Amikor a **Lezárási akadályok** lista üres, egy felülvizsgáló (felülvizsgálati jogosultsággal), aki sem a bejelentő, sem a vizsgálat szerzője, megnyomja az **Esemény lezárása** gombot. Az **Üzenetek** részben **Csak a csapat** vagy **Megosztva a bejelentővel** láthatósággal küldhet üzeneteket.

## Mit jelentenek az állapotok
- **Piszkozat / Beküldve / Visszaküldve / Osztályozva / Vizsgálat alatt / Intézkedések követése / Lezárva / Visszavonva:** az esemény életciklusa.
- **Súlyosság — Nincs osztályozva / Nincs értékelve:** még nincs emberi besorolás. **A bejelentő becslése** csak javaslat. A **Severity policy: not configured – human classification only** (súlyossági szabály nincs beállítva – csak emberi besorolás) azt jelzi, hogy a rendszer nem vezet le súlyosságot.
- **A vizsgálat szükségessége UNKNOWN:** még nem született döntés; ez nem jelent „nem”-et, és akadályozza a lezárást.
- **Intézkedés — Nyitott / Befejezve / Ellenőrizve:** a Befejezve nem azonos az Ellenőrizve állapottal, és az Ellenőrizve nem azonos a Hatékony állapottal.
- **Hatékonyság — Függőben / Hatékony / Nem hatékony / Nem egyértelmű / Nincs intézkedés / Nem látható:** a hatékonyság intézkedésenkénti emberi felülvizsgálat, és független a lezárástól. A **Nincs intézkedés** nem jelenti azt, hogy hatékony.
- **Nyitott biztonsági események** összesítés: **Nyitott, nagy súlyosságú események**, **Felülvizsgálat szükséges** vagy **Nincs nyitott, nagy súlyosságú esemény**. Ez csak figyelmeztetés; nem ad készenléti döntést.
- **Bizalmas (védett):** a bejelentő személyazonossága korlátozott hozzáférésű tárolóban van, és az olvasók nem látják.
- **Offline rögzítve / Szinkronizálásra vár / Mentve ezen az eszközön. Még nincs elküldve.** A sorba állított nem azonos az elküldöttel.

## Amit nem tehet meg / figyelem
- A bejelentő nem végezheti el saját bejelentésének előzetes besorolását. A vizsgálat szerzője, a beküldő vagy a bejelentő nem adhatja meg a független felülvizsgálatot.
- Csak az intézkedés felelőse jelentheti a teljesítést. Sem a felelős, sem a teljesítő nem ellenőrizheti; sem a felelős, sem a teljesítő, sem az ellenőrző nem vizsgálhatja felül a hatékonyságot. Ezek különböző személyek külön rekordjai.
- A kiváltó okok emberi megállapítások; a rendszer soha nem javasol kiváltó okot.
- Egy beküldött bejelentést a szerzője soha nem vonhat vissza és nem törölhet.
- Bizalmas bejelentésnél a személyazonosságot csak szükségszerű megismerés alapján nyitja meg az, akinek személyazonosság-olvasási jogosultsága van, és aki megad egy **Cél** értéket; minden olvasást a céljával együtt rögzítenek. Az anonim bejelentéshez a nyilvános bejelentési csatorna szükséges, amely nincs beállítva.
- Egy Nem egyértelmű felülvizsgálat megismételhető; a Hatékony és a Nem hatékony végleges; egy nem működő intézkedéshez új követő intézkedés szükséges.

## Ha valami nem sikerül
- **The server refused the command:** (a szerver elutasította a parancsot) teljesítse az alatta felsorolt hiányzó vagy akadályozó tételeket.
- **A rekord időközben megváltozott. Töltse be újra, és ellenőrizze, mielőtt újra próbálkozik.** Nyomja meg az **Újratöltés** gombot.
- **Az eredmény ismeretlen (a kapcsolat megszakadt).** Használja az **Ugyanazon parancs újrapróbálása** gombot; a parancs nem kerül kétszer alkalmazásra.
- **Ez a művelet az Ön számára nem engedélyezett.** Ellenőrizze szerepkörét, hajó- és objektumkontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **The device key is unavailable** (az eszközkulcs nem érhető el) vagy **Online re-authorisation is needed** (online újrahitelesítés szükséges): a rögzített adatok megmaradnak; online állapotban hitelesítse magát újra. Alkalmazáshiba esetén forduljon a támogatáshoz a korrelációs azonosítóval, a képernyővel, az időponttal és a lépésekkel (13.03 §10).

## Kapcsolódó oldalak
