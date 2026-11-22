---
key: today
title: Ma
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## Mire szolgál ez az oldal
A „Ma” oldal a kiválasztott hajóra vagy vontatóhajóra vonatkozóan egy listába gyűjti a **most az Ön figyelmét igénylő riasztásokat**. Arra a kérdésre válaszol: „Ezen a hajón melyik rekord vár ma döntésre vagy ellenőrzésre, és melyik forrás mondja ezt?” Az oldal semmiről nem dönt; a riasztást, annak forrását és a következő lépést mutatja.

## Ki használja
- **Kapitány** és **Vontatóhajó-kapitány:** a nap kivételeinek, azok felelősének és a következő lépésnek a megtekintésére (13.03 §2, §3).
- **Gépüzemvezető:** a karbantartási terv riasztásainak követésére (13.03 §5).
- **Crew / OOW:** a rájuk eső munkák megtekintésére (13.03 §6).

## Mielőtt elkezdené
- A felső kontextussávban ellenőrizze, hogy a **megfelelő vállalat és hajó** van-e kiválasztva (13.03 §1). A megjelenő riasztás a kiválasztott hajóhoz tartozik.
- Az oldal megnyitásához olvasási hozzáférés szükséges a munkaterülethez.
- Ha nincs kiválasztott hajó, az oldal először a „Hajó kiválasztása” listát mutatja.

## Lépésről lépésre
1. Válassza ki azt a hajót vagy vontatóhajót, amelyen dolgozik. A kiválasztott hajó „Aktuális hajó” néven jelenik meg; a módosításhoz használja a „Hajó váltása” lehetőséget.
2. Tekintse meg a felső **Forráslefedettség** részt. Ez mutatja, mely források (szabályozási, tervezett karbantartás, vontatóműveletek) voltak olvashatók.
3. Olvassa el a riasztáskártyákat fentről lefelé. Minden kártya egy forrásból származik, és következő lépést javasol (például „Piszkozat áttekintése”, „Karbantartási terv áttekintése”, „Vontatási munka megtekintése”).
4. Nyomja meg a **Miért?** gombot egy kártya mögötti ok megtekintéséhez. A rész a szabályt, a forrásrekordot, annak revízióját, az olvasás idejét és az esetleges ismeretlen bemeneteket mutatja.
5. Kövesse a kártya hivatkozását a rekord saját oldalára, és ott cselekedjen a jogosultságai keretein belül.
6. Az adatok újraolvasásához használja a **Frissítés** gombot.

## Mit jelentenek az állapotok
- **Elérhető (nem ellenőrzött):** a forrást elolvasták. Ez nem jelenti azt, hogy a tartalmát ellenőrizték.
- **Hiányzik / Ebben a kontextusban nem érhető el:** a forrás ehhez a hajóhoz nem volt olvasható. Ha nem lát riasztást, az nem jelenti azt, hogy minden rendben van.
- **Partial view:** (részleges nézet) egyes források nem voltak olvashatók. A nulla darabszám nem jelenti azt, hogy „nincs probléma”.
- **Every listed source was read and returned no items:** (minden felsorolt forrást elolvastak, és egyik sem adott vissza tételt) nem érkezett riasztás. Ez nem biztonsági jóváhagyás.
- **Ismeretlen:** az érték hiányzik a forrásból, vagy nem volt meghatározható. Az Ismeretlen nem jelenti sem a „Nem”, sem a „Kész”, sem a „Nem alkalmazható” értéket.
- **Felülvizsgálat szükséges / Tervezési figyelem szükséges / Tájékoztató:** a kártya prioritása. Egy felülvizsgálatot igénylő kártya arra vár, hogy egy jogosult személy megnézze.
- **Completed ≠ Verified:** (Befejezve ≠ Ellenőrizve) egy rekord befejezése nem jelenti azt, hogy egy jogosult ellenőrző megerősítette (13.03 §1).

## Amit nem tehet meg / figyelem
- Ez az oldal nem hoz indulási, üzemi vagy biztonsági döntést. Ez a döntés a jogosult Kapitányt vagy Vontatóhajó-kapitányt illeti.
- Erről az oldalról semmilyen parancs nem fut; a műveletek a rekord saját oldalán történnek.
- Az offline állapotban megjelenített adatok elavultak és nem ellenőrzöttek. A „Miért?” részletei offline nem nyílnak meg.
- Az olvasási vagy mentési időpont nem bizonyítja, hogy a forrás helyes.

## Ha valami nem sikerül
- **Offline:** folytassa a kritikus munkáját, és figyelje a szinkronizálás állapotát. Ha ütközés jelenik meg, lépjen az Ütközésközpontba (13.03 §10).
- **Access denied / Vessel unavailable in your scope:** (hozzáférés megtagadva / a hajó nem érhető el az Ön hatókörében) ellenőrizze szerepkörét és hajókontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz.
- **Unexpected alert:** (váratlan riasztás) nyissa meg a „Miért?” részt; ha a szabály vagy a forrás hibásnak tűnik, adja tovább a kontroll felelősének vagy a szakterületi szakértőnek.
- **Forrás nem érhető el / Too many requests:** (túl sok kérés) várjon egy kicsit, és használja az „Újra” lehetőséget.
- **The server response failed validation:** (a szerver válasza nem felelt meg az ellenőrzésen) semmi nem jelenik meg belőle; jelentse a támogatásnak képernyőképpel, az időponttal és a lépéseivel.

## Kapcsolódó oldalak
