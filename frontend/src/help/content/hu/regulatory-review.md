---
key: regulatory-review
title: Szabályozási SME-áttekintés
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Szabályozási SME-áttekintés olyan kampányokat rögzít, amelyekben egy emberi szakértő felülvizsgálja egy hajó vagy vontatóhajó forrás- és alkalmazhatósági tételeit. Arra a kérdésre válaszol: „Ennek a hajónak mely forrás- és követelménytételeit erősítette meg egy felhatalmazott felülvizsgáló, és melyek nyitottak még és követettek?” Ez kizárólag emberi felülvizsgálati feljegyzés: a rendszer nem hagy jóvá követelményt, nem aktivál szabályt vagy engedélyt, és nem állít osztályozótársasági vagy típusjóváhagyást (13.03 §8).

## Ki használja
- **Szabályozási / megfelelőségbiztosítási felhasználó:** felülvizsgálati kampányokat nyit, döntéseket rögzít a tételekről, és lezárja a kampányt (13.03 §8).
- **Kinevezett felülvizsgáló (SME):** a saját forráshatósági körén belül megerősít vagy követ tételeket.
- **DPA / HSEQ:** figyeli a nyitott és nem követett tételeket (13.03 §4).

## Mielőtt elkezdené
- A megtekintéshez szabályozási felülvizsgálat-olvasási jogosultság szükséges. Kampányok megnyitásához és lezárásához kezelési jogosultság kell; döntések rögzítéséhez döntési jogosultság és erre a hatókörre érvényes felülvizsgálói kinevezés.
- Válassza a **Hajó** vagy a **Vontatóhajó** fület a **Felülvizsgálat hatóköre** részben, majd nyissa meg a megfelelő hajót a **Válasszon hajót** listában (13.03 §1).
- A forrásokat hivatalosként és verziózottként kell regisztrálni a [Forráspiszkozatok](/app/sources) oldalon (13.03 §8).
- Offline állapotban az adatok elavultak lehetnek, és a parancsok le vannak tiltva.

## Lépésről lépésre
1. Válassza ki a hatókört és a hajót. Ha nincs kampány, nyomja meg a **Felülvizsgálati kampány megnyitása** gombot.
2. A **Forrás-felelősségi mátrix** részben tekintse át minden forráshatóság kategóriáját, nyitott és megerősített darabszámait és felelős szerepköreit.
3. Válasszon ki egy tételt a **Felülvizsgálati tételek** közül.
4. A **Felülvizsgálati döntés rögzítése** űrlapon töltse ki a **Felülvizsgálói kinevezés**, a **Döntés**, a **Forráshatóság** és a **Felelős szerepkör** mezőt. Ha a tételnek nincs regisztrált forrása, adja meg a **Hiteles forrás hivatkozása** és a **Forrás verziója / kiadása** értékét. Nyitva maradó tételnél adjon meg **Követett ügy azonosítója** értéket.
5. Írjon 10–2000 karakteres indoklást a **Felülvizsgálói megjegyzés** mezőbe, és nyomja meg a **Rögzítés** gombot.
6. Ha minden tétel rendeződött, a **Kampány lezárása** részben válasszon **Lezárás eredménye** értéket, írja meg a **Lezárás indoka** szövegét, és nyomja meg a **Kampány lezárása** gombot.

## Mit jelentenek az állapotok
- Kampány: **Nyitott**, **Felülvizsgálat alatt**, **Nyitott tételekkel lezárva**, **Lezárva**.
- Döntés: **Megerősítve**, **Nem alkalmazandó (megerősítve)**, **Felülvizsgálat szükséges**, **Kétértelmű**. Ha kritikus adat ismeretlen, a tétel **Felülvizsgálat szükséges** marad; ismeretlen tétel soha nem számít „Nem alkalmazandó” tételnek.
- **Jelölt (nem ellenőrzött):** a forrás még nincs ellenőrzött hivatalos forráshoz kötve.
- **Elavult — rögzítés megváltozott** és **A hajóprofil megváltozott:** a forrás vagy a hajóprofil a döntés után megváltozott; a tételt újra felül kell vizsgálni.
- **Nem követett nyitott tételek:** követett ügy nélküli nyitott tételek. Amíg számuk nullánál nagyobb, a kampány nem zárható le.
- A **Jogszabály**, az **Osztályozói szabály**, a **Vállalati SMS** és az **Útmutató — nem jogszabály** kategóriák soha nem helyettesítik egymást (13.03 §8).

## Amit nem tehet meg / figyelem
- Aki egy kampányt megnyitott, nem rögzíthet benne döntéseket; felülvizsgálói kinevezés nélkül a döntések csak olvashatók.
- Az a felülvizsgáló, akinek a képesítése nem ellenőrzött, követhet nyitott tételeket, de nem erősíthet meg. Elavult tétel nem erősíthető meg.
- A megerősítéshez regisztrált forrás, vagy hiteles forráshivatkozás és annak kiadása szükséges. Ne hagyjon árva vagy forrás nélküli rekordot a Követelmény → Kontroll → Bizonyíték → Ellenőrzés láncban (13.03 §8).
- A **Lezárva** állapothoz minden tételnek megerősítettnek kell lennie; ellenkező esetben nyitott tételekkel zárja le, mindegyiket követve.
- A megjegyzésben ne állítson osztályozótársasági vagy típusjóváhagyást. A vontatóhajó-hatókörben ellenőrzött forrás nélküli vontatási határ nem minősül jogi alapértelmezésnek.
- Döntéseket csak emberi felhasználók rögzítenek; a mesterséges intelligencia és az automatizálási fiókok nem rögzíthetnek döntést.

## Ha valami nem sikerül
- **A rekord megváltozott. Folytatás előtt töltse újra és ellenőrizze.** Nyomja meg az **Újratöltés** gombot.
- **Az eredmény bizonytalan. Az újrapróbálás ugyanazt a parancsot küldi.** Használja az **Újra** gombot.
- **A felülvizsgálati szabályok elutasították ezt a parancsot.** Ellenőrizze, hogy a tétel elavult-e, valamint a forráshivatkozást és a követett ügyet.
- **Nincs hozzáférése ennek a hajónak a szabályozási felülvizsgálatához.** vagy **Nincs jogosultsága ehhez a művelethez.** Ellenőrizze szerepkörét és hatókörét; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- Ha az alkalmazhatóság váratlannak tűnik, ellenőrizze a forrás- és kontextusbemeneteket, és beszéljen a kontroll felelősével vagy a szakterületi szakértővel (13.03 §10).

## Kapcsolódó oldalak
