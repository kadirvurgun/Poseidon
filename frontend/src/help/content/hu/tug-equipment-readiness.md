---
key: tug-equipment-readiness
title: Vontatóberendezés készenléte
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Ez az oldal a kiválasztott vontatóhajó **vontatóberendezés-nyilatkozatait** (vontatócsörlő, vontatóhorog, vontatókötél, vészkioldó, FiFi tűzoltó berendezés) és azok **Gépüzemvezetői ellenőrzések** rekordjait mutatja. Arra a kérdésre válaszol: „Mi az egyes tételek SWL-értéke, vizsgálati dátumai, állapota és kritikussága, ki ellenőrizte, és mit mutat a készenléti ellenőrzés?” Az értékelés csak tanács, és a készenléti kapu bemenete; nem GO-döntés.

## Ki használja
- **Vontatóhajó-kapitány:** a csörlő, a vontatókötél, a vontatóhorog / láncstopper, a vészkioldó, a FiFi és a kritikus hibák bizonyítékainak ellenőrzésére (13.03 §3).
- **Gépüzemvezető:** a nyilatkozatok ellenőrzésére vagy nem ellenőrzöttként való rögzítésére, valamint egy kritikus hiba készenlétre gyakorolt hatásának rögzítésére (13.03 §5).

## Mielőtt elkezdené
- Nyissa meg a vontatóhajót a **Vontatóműveletek** részből; ehhez a képernyőhöz nem kell kiosztás.
- Nyilatkozat hozzáadásához vagy szerkesztéséhez vontató-írási és berendezés-írási hozzáférés szükséges.
- Ellenőrzés rögzítéséhez az adott vontatóhajóra vonatkozó, szerveren rögzített **Gépüzemvezető** kijelölés szükséges.

## Lépésről lépésre
1. Olvassa el az összesített jelzést az **Értékelés** sorban.
2. Minden berendezéskártyán ellenőrizze az eredményjelzést, az indokkódot, az **SWL (t)**, a **Következő vizsgálat esedékes**, az **Állapot** és a **Kritikus** értékét.
3. Új nyilatkozathoz nyomja meg a **Berendezés hozzáadása** gombot; töltse ki a **Berendezés típusa**, **Címke**, **SWL (t)**, **Utolsó vizsgálat**, **Következő vizsgálat esedékes**, **Állapot**, **Kritikus**, **PMS eszközazonosító**, **Tanúsítványazonosító** és **Megjegyzések** mezőket, majd nyomja meg a **Nyilatkozat mentése** gombot.
4. Egy meglévő nyilatkozat módosításához használja a kártyáján a **Szerkesztés** gombot.
5. Gépüzemvezetőként válassza ki a **Kijelölés** értékét, írjon indokot, és az **Ellenőrizve** vagy a **Nem ellenőrzött** lehetőséggel használja az **Ellenőrzés rögzítése** gombot. A „Nem ellenőrzött” lehetőséghez indok szükséges.
6. Az ellenőrzési előzményeket revízióval és időponttal a **Gépüzemvezetői ellenőrzések** alatt, a **Forrásellenőrzések** táblázatot pedig az oldal alján találja.

## Mit jelentenek az állapotok
- **Kész / Feltételes / Nem kész / Felülvizsgálat szükséges:** a tanácsadó jellegű értékelés. A Kész nem GO-döntés.
- **Megfelelt / Nem felelt meg / Ismeretlen:** egy tétel ellenőrzési eredménye.
- **Üzemképes / Korlátozott / Meghibásodott / Ismeretlen:** a megadott állapot. Ha nincs kiválasztott állapot, „Ismeretlen” marad; nem számít üzemképesnek.
- **Nincs értékelve:** a kritikusságot nem értékelték. Ez nem jelent „Nem”-et.
- **Ellenőrizve / Nem ellenőrzött:** a Gépüzemvezető emberi rekordja.
- **későbbi változat felülírta:** az ellenőrzés a nyilatkozat egy régebbi revíziójához tartozott; az aktuális revízióhoz új ellenőrzés szükséges.

## Amit nem tehet meg / figyelem
- A mentés nem ellenőrzött nyilatkozatot rögzít. Minden módosításhoz új gépüzemvezetői ellenőrzés szükséges.
- Egy rekord befejezése és ellenőrzése nem ugyanaz a művelet; az ellenőrzés a jogosult személyre vár (13.03 §1).
- A berendezés típusa mentés után nem módosítható; más típushoz adjon hozzá új nyilatkozatot.
- Az oldal soha nem nyilvánítja biztonságosnak a vontatóberendezést; a döntés a Vontatóhajó-kapitánynál marad (13.03 §3).
- Offline állapotban a parancsok ki vannak kapcsolva.

## Ha valami nem sikerül
- **"The record changed" üzenet:** az **Újratöltés** gombbal tekintse át az aktuális nyilatkozatot.
- **"The outcome is uncertain" üzenet:** az **Újra** gomb ugyanazt a parancsot küldi el.
- **Record cannot be verified:** (a rekord nem ellenőrizhető) ellenőrizze a bizonyítékot és a PMS-/tanúsítványforrást; forduljon az ellenőrzőhöz (13.03 §10).
- **No authorization:** (nincs jogosultság) ellenőrizze szerepkörét, vontatóhajó- és kijelölési kontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
