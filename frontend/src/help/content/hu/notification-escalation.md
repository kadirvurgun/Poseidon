---
key: notification-escalation
title: Értesítések
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Az értesítések oldal a kiválasztott hajóra vagy vontatóhajóra vonatkozó **alkalmazáson belül beállított forráskivételeket** listázza (esedékes karbantartás, karbantartási felülvizsgálat szükséges, a karbantartási forrás megváltozott vagy nem érhető el). Arra a kérdésre válaszol: „Mely karbantartási értesítések jutottak el a szerepkörömhöz, mennyire súlyosak, melyeket eszkalálták, mert senki nem vette tudomásul őket időben, és melyik forrásrekordon alapulnak?” A tudomásulvétel nem ellenőrzi a bizonyítékot, és nem hagy jóvá műveleteket.

## Ki használja
- **Gépüzemvezető / műszaki felügyelő:** a karbantartási határidők és felülvizsgálati kérések követésére és a forrástervhez való átlépésre (13.03 §5).
- **Vontatóhajó-kapitány / kapitány / személyzet:** a szerepkörükhöz irányított értesítések megtekintésére és tudomásulvételére (13.03 §6).

## Mielőtt elkezdené
- Értesítés-, karbantartás- és berendezésolvasási hozzáférés szükséges. A tudomásulvételhez és a beállítások mentéséhez külön jogosultságok kellenek; ezek nélkül a kapcsolódó gombok nem jelennek meg.
- Válasszon jogosult hajót vagy vontatóhajót az **Értesítés hajója** listából.
- Offline állapotban sem tudomásulvételi, sem beállítási parancs nem küldhető.

## Lépésről lépésre
1. Válasszon hajót az **Értesítés hajója** listából.
2. Olvassa el a szabálysort: „Beállítva · nem ellenőrzött forrásszabály · csak alkalmazáson belüli kézbesítés”.
3. Minden értesítésnél olvassa el a címet, a súlyossági jelvényt (**Információ**, **Figyelmeztetés**, **Kritikus**) és az állapotjelvényt; ellenőrizze a **Forrás / revízió**, a **Határidő** és a **Létrehozás időpontja** értékét. Az **Útválasztási szabály** a szabály kódját mutatja.
4. A kapcsolódó karbantartási tervhez a **Forrásterv megnyitása** segítségével léphet.
5. Annak rögzítéséhez, hogy látta az értesítést, nyomja meg a **Tudomásulvétel** gombot (csak **Nyitott** vagy **Eszkalálva** állapotban).
6. Ha van beállítási hozzáférése, szerkessze a **Nem kötelező tájékoztató értesítések némítása** és a **Csendes órák** (kezdet, vég, IANA időzóna) beállítást, és nyomja meg a **Tájékoztató beállítás mentése** gombot.
7. További oldalakhoz használja az **Előző értesítések** / **További értesítések** gombot, az ismételt beolvasáshoz pedig a **Beérkezések újratöltése** gombot.

## Mit jelentenek az állapotok
- **Nyitott:** az értesítés tudomásulvételre vár.
- **Tudomásul véve:** egy személy rögzítette, hogy látta. Ez nem jelenti azt, hogy a karbantartást elvégezték, vagy a bizonyítékot ellenőrizték.
- **Eszkalálva:** az értesítést nem vették tudomásul időben, és a szabálya szerinti eszkalációs szerepkörhöz irányították.
- **Forrás megváltozott / Forrás nem érhető el:** az alapul szolgáló rekord megváltozott, vagy nem erősíthető meg. A rögzített alap megmarad; ez nem törli és nem élesíti újra az értesítést.
- **Kötelező útvonal · nem némítható:** a beállítások nem némíthatják ezt az értesítést.
- **Ismeretlen (Határidő):** a forrásnak nincs határideje. Ez nem jelenti azt, hogy semmi sem esedékes.

## Amit nem tehet meg / figyelem
- A tudomásulvétel nem ellenőrzi a bizonyítékot, és nem hagy jóvá műveleteket.
- A kötelező és kritikus útvonalakat a tájékoztató beállítások nem némíthatják. A csendes órák csak az opcionális értesítéseket késleltetik.
- Az üres oldal nem jelenti azt, hogy minden rendben van. Ha a szabály vagy a források nem érhetők el, az oldal ezt külön jelzi.
- Nincs automatikus újrapróbálás; a bizonytalan kimenetelű parancs megmarad ezen a lapon, és a kontextusváltás zárolva marad.

## Ha valami nem sikerül
- **„Outcome unknown” üzenet:** használja a **Pontos értesítési parancs megismétlése** gombot a megőrzött parancs ismételt elküldéséhez.
- **„Notification version changed” üzenet:** használja a **Beérkezések újratöltése** gombot az aktuális lista betöltéséhez.
- **„Notification sources unavailable or access denied.” üzenet:** ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **„The saved quiet hours could not be applied” üzenet:** minden értesítés megjelenik; ellenőrizze a csendes órák beállításait.

## Kapcsolódó oldalak
