---
key: audit
title: Műveleti előzmények
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: [1, 2, 3]
reviewed: false
---
## Mire szolgál ez az oldal
A Műveleti előzmények oldal az Ön jogosult hatókörébe tartozó üzleti rekordok változásainak **csak olvasható**, szereplőhöz rendelt nyilvántartását mutatja. Arra a kérdésre válaszol: „Mikor változott ez a rekord, milyen művelettel, és milyen szereplő és szerepkör-kontextus mellett?” A biztonsági események külön korlátozottak, és itt nem jelennek meg (13.03 §4, §9).

## Ki használja
- **DPA / HSEQ:** megállapítás-, intézkedés- és ellenőrzési rekordok nyomon követése (13.03 §4).
- **Szabályozási / megfelelőségbiztosítási felhasználó:** a rekordelőzmények ellenőrzése audit vagy felülvizsgálat előtt (13.03 §8).
- **Bérlői adminisztrátor:** a tagsági és jogosultsági változások, valamint a meghatalmazások kezdési és befejezési időpontjainak auditálása (13.03 §9).

## Mielőtt elkezdené
- A megtekintéshez auditolvasási jogosultság szükséges. Enélkül a „Auditjogosultság szükséges.” üzenet jelenik meg.
- Csak azok a rekordok jelennek meg, amelyek abba a bérlői és hajóhatókörbe tartoznak, amelyre Ön jogosult.
- Ha egy adott rekordot keres, tartsa kéznél a **Hajóazonosító** vagy az **Objektumazonosító** értékét. Más oldalak előzményhivatkozásai ezt az oldalt már szűrve nyitják meg.

## Lépésről lépésre
1. Töltse ki a szűrőket: **Hajóazonosító**, **Objektumazonosító**, **Objektumtípus** és **Művelet** (**Mind**, **Létrehozva**, **Frissítve**, **Törölve**).
2. Nyomja meg a **Szűrés** gombot. Az alaphelyzetbe állításhoz használja a **Szűrők törlése** gombot.
3. Tekintse át az **Idő**, az **Objektum** és a **Művelet** oszlopot a **Jogosult rekordok** részben. Az oldalak között az **Első oldal** és a **Következő** gombbal léphet.
4. Egy soron nyomja meg az objektum hivatkozását a **Rekord részletei** megnyitásához. Olvassa el a **Szereplőhivatkozás**, a **Hozzárendelt szerepek hivatkozásai**, a **Megbízható jogosultságok**, az **Indok**, a **Metaadatok**, az **Erőforrás verziója** és a **Korreláció** értékét.
5. Ha létezik módosíthatatlan revízióbizonyíték, nyomja meg a **Pontos módosíthatatlan revízió megnyitása** gombot, hogy a rekordot az akkori állapotában lássa. A részletek bezárásához nyomja meg a **Bezárás** gombot.

## Mit jelentenek az állapotok
- **Létrehozva / Frissítve / Törölve:** a rekordon végzett művelet fajtája.
- **Rögzítve:** a metaadatokat teljes körűen rögzítették.
- **Korábbi metaadatok hiányosak / Korábbi azonosító nem érhető el / Korábbi kontextus nem érhető el:** a régebbi rekordok nem őrizték meg ezt az információt. A hiányzó információt a rendszer soha nem tölti ki találgatással.
- **Értékek visszatartva:** a mező értéke adatvédelmi okból rejtett; csak a változás ténye látható.
- **Ehhez a rekordhoz nem érhető el módosíthatatlan revízióbizonyíték.** Ehhez a rekordtípushoz nincs revíziós pillanatkép.

## Amit nem tehet meg / figyelem
- Az oldal csak olvasható. A műveleti előzmények bejegyzései nem szerkeszthetők és nem törölhetők.
- Az, hogy egy rekord megjelenik az előzményekben, nem jelenti azt, hogy a munkát ellenőrizték vagy hatékony volt; Ellenőrzött ≠ Hatékony (13.03 §4).
- Ez az oldal az üzleti változásokat rögzíti. Önmagában nem teljes körű felhasználói műveletaudit és nem bizonyítja az adatvédelmi megfelelést; a biztonsági eseményeket külön tárolják.
- A lista csak azt a hatókört mutatja, amelyre Ön jogosult. A hatókörén kívüli rekordok nem jelennek meg; ha nem lát egy rekordot, az nem jelenti azt, hogy soha nem változott.

## Ha valami nem sikerül
- **Auditjogosultság szükséges.** Ellenőrizze szerepkörét és hatókörét; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **A folytatáshoz jelentkezzen be.** Jelentkezzen be újra.
- **Az előzmények nem érhetők el. Ellenőrizze a szűrőket, és próbálja újra.** Ellenőrizze, hogy az azonosítók teljesek és helyesek-e, majd nyomja meg az **Újra** gombot.
- **Ezeknek a szűrőknek nem felel meg jogosult rekord.** Bővítse a szűrőket.
- Ha egy alkalmazáshiba továbbra is fennáll, jelentse a támogatásnak a korrelációs azonosítóval, a képernyővel, az időponttal és a lépésekkel (13.03 §10).

## Kapcsolódó oldalak
