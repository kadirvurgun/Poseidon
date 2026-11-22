---
key: tug-readiness
title: Vontatóhajó készenléti kapu
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A készenléti kapu egy vontatóhajó-kiosztás üzemi készenlétét forrásellenőrzések alapján **kizárólag tanácsként** értékeli, és lehetővé teszi, hogy a kijelölt Vontatóhajó-kapitány vagy Kapitány indokolt **GO/NO-GO döntést** rögzítsen. Arra a kérdésre válaszol: „Mit mutatnak a személyzeti, berendezés-, PMS-, tanúsítvány-, kommunikációs, időjárási, vontatóerő-tartalék-, kiosztási és eligazítási ellenőrzések, és milyen akadályok állnak fenn?” Az értékelés nem döntés: „A rendszer soha nem ad ki GO-t; csak a kijelölt Kapitány vagy Vontatóhajó-kapitány rögzít indokolt GO/NO-GO döntést.”

## Ki használja
- **Vontatóhajó-kapitány / Kapitány:** az értékelés és az akadályok áttekintésére, valamint GO vagy NO-GO döntés rögzítésére (13.03 §3).
- **Vontatási műveleti koordinátor / Parti műveletek:** a készenlét és az akadályok követésére; ők nem döntenek (13.03 §7).

## Mielőtt elkezdené
- Az oldalt egy kiosztásból nyissa meg: a **Kiosztás és ütközések** oldalon használja egy javaslat alatti **Műveleti készenléti kapu** hivatkozást. Kiválasztott kiosztás nélkül az oldal ezt mutatja: „Válasszon kiosztást a munka kiosztási listájából.”
- Döntés rögzítéséhez vontató-írási hozzáférés és az adott vontatóhajóra vonatkozó, szerveren rögzített **Kijelölés** (Vontatóhajó-kapitány vagy Kapitány) szükséges, és a kiosztásnak még Javasolt állapotúnak kell lennie.
- Eligazítás nélkül nincs READY: a **Vontatási kockázat és eligazítás** rekordot ki kell adni, és minden résztvevőnek tudomásul kell vennie (13.03 §3).

## Lépésről lépésre
1. Olvassa el az állapotjelzést az **Értékelés** sorban. A mellette lévő megjegyzés: „Tanácsadó jellegű, nem GO-döntés.”
2. Ellenőrizze a **Szükséges vonóerő (t)**, az **Igazolt rendelkezésre álló vonóerő (t)** és a **Vonóerő-tartalék (t)** értékét. Hiányzó érték esetén „Ismeretlen” jelenik meg.
3. Tekintse át a **Kritikus akadályok**, a **Felülvizsgálati okok** és a **Feltételek** alatti kódokat.
4. A kategóriatáblázatban tekintse meg az egyes kategóriák eredményét (személyzet, vontatási kompetencia, berendezés, vontatóberendezés, PMS és hibák, tanúsítványok, kommunikáció, időjárás, vontatóerő-tartalék, kiosztás, vontatási kockázat és eligazítás). A részletekért olvassa el a **Forrásellenőrzések** táblázat indokkódjait.
5. Ha Ön rendelkezik döntési jogkörrel, a **Kapitányi döntés rögzítése** űrlapon válassza a **NO-GO** vagy a **GO** lehetőséget, adja meg a **Kijelölés** és az **Indok** értékét, és nyomja meg a **Rögzítés** gombot.
6. A rögzített döntéseket a **Kapitányi döntések** listában tekintheti meg.

## Mit jelentenek az állapotok
- **Kész (READY):** minden forrásellenőrzés megfelelt (nincs akadály, ismeretlen érték vagy feltétel). Ez információ, nem döntés; a GO-döntést továbbra is a Vontatóhajó-kapitány vagy a Kapitány rögzíti.
- **Feltételes (CONDITIONAL):** a „Feltételek” részben szereplő feltételek érvényesek. A GO le van tiltva, és az oldal ezt magyarázza: „Ez az értékelés CONDITIONAL. A GO-hoz READY értékelés szükséges: egyetlen forrás sem határozza meg, mely feltételek mellett lenne megengedett a GO, ezért a szerver elutasítja. Oldja meg a feltételeket és értékeljen újra, vagy rögzítsen NO-GO döntést.”
- **Nem kész (NOT_READY):** legalább egy kritikus akadály fennáll.
- **Felülvizsgálat szükséges (REVIEW_REQUIRED):** egyes források ismeretlenek vagy felülvizsgálatra várnak. Egy ismeretlen ellenőrzés soha nem számít „Kész”-nek.
- **Megfelelt / Nem felelt meg / Ismeretlen:** egyetlen kategória vagy ellenőrzés eredménye.
- **GO / NO-GO:** a Vontatóhajó-kapitány vagy a Kapitány által rögzített emberi döntés és indok.

## Amit nem tehet meg / figyelem
- GO csak READY értékeléshez rögzíthető; a szerver elutasítja a GO-t CONDITIONAL, NOT_READY vagy REVIEW_REQUIRED esetén. Az oldal csak READY értékelésnél engedélyezi a GO-t; egyébként ezt mondja: „GO csak READY értékeléshez rögzíthető.” NO-GO indokkal mindig rögzíthető.
- Nincs READY, amíg a kockázat és az eligazítás hiányos, vagy hiányoznak résztvevői tudomásulvételek; ilyenkor a GO lehetőség le van tiltva (13.03 §3).
- Egy parti felhasználó kiosztási javaslata soha nem írja felül a Vontatóhajó-kapitány fedélzeti biztonsági döntését (13.03 §7).
- Ha az értékelés a döntése előtt megváltozik, a döntést a rendszer elutasítja; tekintse át újra az aktuális értékelést.
- Offline állapotban a parancsok ki vannak kapcsolva.

## Ha valami nem sikerül
- **"The assessment changed" üzenet:** használja az **Újratöltés** gombot, tekintse át az aktuális értékelést, és döntsön újra.
- **"The outcome is uncertain" üzenet:** az **Újra** gomb ugyanazt a parancsot küldi el.
- **Unexpected readiness:** (váratlan készenléti eredmény) nyissa meg a kategóriatáblázatot és a forrásellenőrzési indokokat; tekintse át őket a kontroll felelősével vagy a szakterületi szakértővel (13.03 §10).
- **No authorization:** (nincs jogosultság) ellenőrizze szerepkörét, vontatóhajó- és kijelölési kontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
