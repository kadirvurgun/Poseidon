---
key: companies
title: Vállalatok
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## Mire szolgál ez az oldal
A Vállalatok oldal a munkaterület azon **vállalati rekordjait** listázza, amelyek megtekintésére Ön jogosult. Arra a kérdésre válaszol: „Mely vállalatok vannak meghatározva ebben a munkaterületen, és mi a kódjuk és a nevük?” A hajó- és vontatóhajó-rekordok egy vállalathoz tartoznak, ezért a megfelelő vállalat kiválasztása a kontextusválasztás első lépése.

## Ki használja
- **Bérlői adminisztrátor:** vállalati rekordok létrehozására és szerkesztésére a legkisebb jogosultság elve szerint (13.03 §9).
- **Parti üzemeltetés:** a megfelelő vállalati kontextus megtalálására.

## Mielőtt elkezdené
- Az oldal megtekintéséhez vállalatolvasási jogosultság szükséges. Enélkül a „Nincs hozzáférése ehhez a munkaterülethez.” üzenet jelenik meg.
- Rekord létrehozásához létrehozási jogosultság szükséges. Az, hogy egy rekordot szerkeszthet-e, attól függ, milyen jogosultságot ad a szerver az adott rekordra.

## Lépésről lépésre
1. A listában láthatja az egyes vállalatok kódját és nevét. A további oldalakhoz használja az **Előző** / **Következő** gombot.
2. Kereséshez írjon a **Keresés kód vagy név alapján** mezőbe, és nyomja meg a **Keresés** gombot.
3. Válasszon ki egy vállalatnevet a **Részletek** rész megnyitásához.
4. Új vállalathoz nyomja meg az **Új rekord** gombot, adja meg a kódot és a nevet, majd nyomja meg a **Mentés** gombot. Minden mező kötelező.
5. Meglévő rekord módosításához használja a részletekben a **Szerkesztés** gombot.
6. A jogi személy adatainak megtekintéséhez nyissa meg a **Jogi személy rekordjának megjelenítése** részt; ez a rész csak olvasható.

## Mit jelentenek az állapotok
- **No records found:** a keresésére vagy jogosultsági hatókörére nem érkezett rekord.
- **Legal entity: Piszkozat – hiányos, nem aktív / Aktív / Inaktív:** a jogi személy rekordjának állapota. A Piszkozat azt jelenti, hogy az aktiválás előtt még hiányoznak értékek.
- **Ismeretlen:** az érték nem szerepel a rekordban. Az ismeretlen nem jelenti azt, hogy „Nem” vagy „érvényes”.
- **Saved:** a rekordot a rendszer a szerverre írta. Ez a rekord nem hoz létre operatív jóváhagyást.

## Amit nem tehet meg / figyelem
- Egy vállalati rekord létrehozása vagy szerkesztése nem operatív, megfelelőségi vagy biztonsági jóváhagyás.
- A jogi személy értékei ezen az oldalon nem módosíthatók; kizárólag auditált HR-parancsokkal változnak. A nyilvántartási azonosítók maszkolva jelennek meg.
- Ugyanazt a kódot két vállalat nem használhatja.
- Ha nem mentett piszkozattal hagyja el az oldalt, a rendszer megkérdezi, hogy elveti-e, vagy folytatja a szerkesztést.

## Ha valami nem sikerül
- **This record changed in another operation:** a piszkozata megmarad. Vesse el a piszkozatot az aktuális rekord betöltéséhez, majd végezze el ismét a módosítást.
- **This code is already in use:** adjon meg másik kódot.
- **The outcome could not be confirmed:** a mezők zárolva maradnak, amíg az eredmény ismertté nem válik; biztonságosan próbálja újra ugyanazt a parancsot.
- **Your permission may have changed / No access:** ellenőrizze szerepkörét és munkaterületi kontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **The service is unavailable:** használja az „Újra” lehetőséget; ha a probléma továbbra is fennáll, jelentse a támogatási hivatkozással.

## Kapcsolódó oldalak
