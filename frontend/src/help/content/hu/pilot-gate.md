---
key: pilot-gate
title: Pilot go/no-go áttekintés
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Pilot go/no-go áttekintés oldal egy helyen mutatja a pilothoz szükséges kapuk állapotát, a nyitott P0-ok számát és a támogatási készenlétet, és rögzíti a kinevezett termékgazda go/no-go döntését. Arra a kérdésre válaszol: „Minden pilotkapu teljesült-e bizonyítékkal, és rögzítettek-e emberi döntést?” A felül látható figyelmeztetés érvényes: „Csak tanácsadó áttekintés. Ismeretlen vagy elhalasztott kapuk és ismeretlen vagy nyitott P0 esetén NO-GO a javaslat. Döntést csak a kinevezett termékgazda rögzít.”

## Ki használja
- **Termékgazda:** elolvassa az áttekintés eredményét, és indoklással rögzíti a go/no-go döntést. A döntéshez érvényes termékgazdai kinevezés szükséges.
- **Támogatás / bérlői adminisztrátor:** bizonyítékhivatkozással jelöli a támogatási és ügyeleti készenléti tételeket.
- A 13.03 ehhez az oldalhoz nem határoz meg külön szerepkört; a bérlőadminisztrációs elvek (legkisebb jogosultság, ellenőrzött közzététel) érvényesek (13.03 §9).

## Mielőtt elkezdené
- A megtekintéshez bérlőszintű olvasási és pilotkapu-olvasási jogosultság szükséges. Támogatási tétel jelöléséhez támogatásrögzítési jogosultság kell; a döntéshez döntési jogosultság, valamint a szerveren létrehozott érvényes termékgazdai kinevezés. A jogosultság önmagában nem elég.
- Támogatási jelölést és döntést csak emberi fiók végezhet; a kinevezésnek egyeznie kell az aktív szerepkörével, és az érvényességi időszakán belül kell lennie.
- A kapuk bemenetei más oldalakról származnak; például a hajó-UAT és a vontatóhajó-UAT kaput a **Szerepkörös UAT-rekordok** oldalon tanúsított futtatásokból számítja a rendszer.
- A rögzítéshez élő kapcsolat szükséges; offline állapotban le van tiltva.

## Lépésről lépésre
1. A **Pilot kapu áttekintése** részben olvassa el a javaslatot (**Tanácsadó javaslat**), a **Nyitott P0** darabszámát és a kaputáblázatot: **Kapu**, **Állapot**, **Forrás**, **Okok**.
2. A **Támogatási és ügyeleti készenlét** részben minden tételnél válasszon **Állapot** értéket, **Kész** esetén adjon meg **Bizonyítékhivatkozás** értéket, írjon **Megjegyzés** szöveget, és nyomja meg a **Jelölés** gombot.
3. Ha Ön a termékgazda, a **Go/no-go döntés rögzítése** részben válassza ki a döntést (**NO-GO**, **Feltételes GO**, **GO**), a **Kinevezés** mezőben válassza ki a termékgazdai kinevezését, és írja meg az **Indoklás** szövegét. **Feltételes GO** esetén töltse ki a **Feltételek (soronként egy)** mezőt.
4. Nyomja meg a **Rögzítés** gombot. A döntés a sorszámával együtt megjelenik a **Termékgazdai döntések** alatt.
5. Az adatok újratöltéséhez nyomja meg a **Frissítés** gombot.

## Mit jelentenek az állapotok
- **Bizonyítéki küszöb teljesült (PASS):** a kapu forrása eléri a bizonyítéki küszöböt. Ez nem jóváhagyás; egy ember dönt.
- **Nem teljesült (FAIL)**, **Ismeretlen (UNKNOWN)**, **Elhalasztva (DEFERRED):** a kapu nem teljesült. Az ismeretlen vagy elhalasztott kapu soha nem számít teljesítettnek, és NO-GO javaslatot eredményez.
- **NO-GO javasolt:** legalább egy kapu nem PASS, vagy a nyitott P0 ismeretlen vagy nullánál nagyobb.
- **GO-ra alkalmas; emberi döntésre vár:** minden kapu PASS, és a nyitott P0 nulla. A rendszer soha nem dönt GO-ról; azt csak a termékgazda rögzíti.
- **Nincs jelölve / Kész / Nem kész:** egy támogatási tétel legutóbb jelölt állapota.
- **Aktuális / Elavult: az áttekintés megváltozott:** egy döntés a meghozatala időpontjában érvényes áttekintéshez kötődik. Ha az áttekintés később megváltozott, a döntés elavultként jelenik meg, és újra kell gondolni.

## Amit nem tehet meg / figyelem
- „GO csak akkor lehetséges, ha minden kapu PASS és nincs nyitott P0.” Egyébként a GO lehetőség le van tiltva.
- A Feltételes GO ugyanazt az állapotot igényli, mint a GO (minden kapu PASS, a nyitott P0 ismerten nulla), és legalább egy feltételt. Nem rögzíthető, amíg bármely kapu nem teljesült, ismeretlen vagy elhalasztott, illetve amíg P0 nyitott vagy ismeretlen. A NO-GO mindig rögzíthető, feltételek nélkül.
- A rendszer, a mesterséges intelligencia vagy az automatizálás soha nem hozza meg a pilotdöntést, és soha nem hamisít kapukat vagy jóváhagyásokat. A pilotkapuk és döntések emberi feljegyzések.
- Egy rögzített döntést nem módosíthat; az új döntés új sorszámmal kerül hozzáadásra.

## Ha valami nem sikerül
- **Nincs érvényes termékgazdai kinevezése. A döntések csak olvashatók.** A kinevezéseket a szerveren hozzák létre; forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **Nincs hozzáférése ezekhez a rekordokhoz.** Ellenőrizze szerepkörét és bérlői hatókörét.
- **A rekord megváltozott vagy már felhasználták. Töltse újra.** Az áttekintés megváltozott, miközben Ön döntött; nyissa meg az aktuális áttekintést az **Újratöltés** gombbal. Ez akkor is megjelenik, ha a kapuk állapota úgy változott, hogy a döntés már nem engedélyezett.
- **Az eredmény bizonytalan. Az újrapróbálás ugyanazt a parancsot küldi.** Az **Újra** gombbal biztonságosan újraküldheti ugyanazt a parancsot.
- **A szabályok elutasították.** Egy kapu elhalasztott.
- **Nem engedélyezett.** A kinevezése nem érvényes, vagy a fiókja nem emberi fiók.
- **A szolgáltatás nem érhető el. Próbálja újra.** Ha a hiba továbbra is fennáll, jelentse a korrelációs azonosítót, az időpontot és a lépéseket a támogatásnak (13.03 §10).

## Kapcsolódó oldalak
