---
key: offline-conflict-center
title: Lényeges ütközések központja
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A lényeges ütközések központja a kiválasztott hajóra vagy vontatóhajóra vonatkozóan listázza az **offline módosításai és az aktuális szerverrekord közötti lényeges ütközéseket**, és lehetővé teszi, hogy egy személy indokolt döntéssel feloldja őket. Arra a kérdésre válaszol: „Mely offline módosításaim ütköztek egy újabb szerverrekorddal, és hogyan, ki által és milyen indokkal lesznek feloldva?” Itt semmi nem íródik felül és nem oldódik fel automatikusan.

## Ki használja
- **Személyzet / vontatóhajó-kapitány / kapitány / gépüzemvezető:** a saját ütköző rekordjaik megtekintésére és feloldására offline munka után. Ha ütközés jelenik meg, ne feltételezze, hogy automatikusan az Ön rekordja érvényesül (13.03 §6).
- A lényeges ütközés emberi feloldást igényel; ez az oldal a 13.03 §10 „Lényeges ütközés → ütközésközpont” folyamatának célpontja.

## Mielőtt elkezdené
- Az oldal csak az Ön által rögzített ütközéseket listázza, és legalább egy olvasható forráshoz szükséges olvasási hozzáférés (például személyzeti beosztás, vontatási műveletek, napi rekord, munkarendelési bizonyíték (berendezés- és karbantartás-olvasással), bizonyítványmegújítási piszkozat). Bármelyik olvasható forrás megnyitja az oldalt; a felsorolt jogosultságok alternatívák.
- A listához és a döntésekhez aktív szerverkapcsolat szükséges. Offline állapotban a helyi módosításai megmaradnak az eszközön.
- Válassza ki a hajót vagy vontatóhajót a **Hajó** listából.

## Lépésről lépésre
1. Válasszon hajót a **Hajó** listából. Megnyílik a táblázat a **Forrás**, **Rekord**, **Verziók**, **Rögzítve**, **Kiszolgáló fogadta** és **Állapot** oszloppal.
2. Ha „Nem szerepel:” megjegyzés látható, azok a források, amelyekhez nincs olvasási jogosultsága, nem szerepelnek a listában.
3. A **Nyitott — döntés szükséges** állapotú soron nyomja meg az **Áttekintés** gombot.
4. Az **Ütközés áttekintése** részben vesse össze minden mezőnél a **Rögzítés előtt**, a **Rögzített** és a **Szerver** értéket. Egyik lehetőség sincs előre kiválasztva.
5. Minden értéknél kifejezetten válassza a **Szerverérték megtartása** vagy a **Rögzített érték használata** lehetőséget, töltse ki a **Feloldási indok (kötelező)** mezőt, és nyomja meg a **Feloldás benyújtása** gombot.
6. Ha csak a helyi másolatot szeretné eltávolítani erről az eszközről, jelölje be a megerősítést a **Helyi módosításom elvetése** részben, és nyomja meg a **Helyi módosítás elvetése** gombot.
7. A listához való visszatéréshez használja az **Áttekintés bezárása** gombot.

## Mit jelentenek az állapotok
- **Nyitott — döntés szükséges:** az ütközés emberi döntésre vár.
- **Újabb ütközés váltotta fel:** ugyanahhoz a rekordhoz újabb ütközés létezik; az újat vizsgálja felül.
- **Kifejezett döntéssel megoldva:** valaki indokolt döntéssel feloldotta. Ez nem üzemi jóváhagyás.
- **Visszatartva / Nincs megadva / Ismeretlen:** az érték jogosultsági okból rejtett, nem rögzítették, vagy nem olvasható. Az ismeretlen értéket a rendszer soha nem tekinti üresnek vagy „Nem” értéknek.
- **Server receipt recorded for your explicit decision:** a szerver megkapta a döntését. Ez a nyugta nem üzemi jóváhagyás.

## Amit nem tehet meg / figyelem
- Az ütközéseket a rendszer soha nem egyesíti és nem oldja fel automatikusan; minden értékhez kifejezett emberi választás és indok szükséges (13.03 §1).
- A központ a személyzeti beosztási ütközéseket közvetlenül oldja fel. Más forrásoknál ezt mutatja: „Resolve this conflict from the owning record's offline panel.”; például a vontatóhajó-rekordokat az **Offline vontatóműveletek** oldalon kell feloldani.
- Egy helyi módosítás elvetése csak az eszközön lévő másolatot távolítja el; a szerverrekord változatlan marad, és az ütközés rögzítve marad, amíg indokolt döntés nem születik.
- Egy másik eszközön rögzített módosítást azon az eszközön kell feloldani; semmi nem kerül elvetésre.
- A benyújtás újra ellenőrzi a hozzáférését és a rekordverziókat; nem ad üzemi jóváhagyást.

## Ha valami nem sikerül
- **„The submission outcome is unknown” üzenet:** az ismételt benyújtás előtt használja a **Benyújtási eredmény ellenőrzése** gombot.
- **„The server record changed after your review” / „The server record changed again” üzenet:** a döntése nem lett alkalmazva; vizsgálja felül újra az ütközést.
- **„A lényeges ütközéseket nem sikerült betölteni. Semmi sem oldódott meg.”:** nyomja meg az **Újra** gombot.
- **„Nincs hozzáférése az ütközésközponthoz.” / „Az ütközésekhez való hozzáférés nem érhető el ehhez a hajóhoz.”:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
