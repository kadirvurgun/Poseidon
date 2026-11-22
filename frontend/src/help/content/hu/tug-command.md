---
key: tug-command
title: Vontatóirányítás
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Mire szolgál ez az oldal
A menüben **Vontatóirányítás** néven jelenik meg; ez az oldal a kiválasztott vontatóhajó **forráskontextusát** mutatja. Arra a kérdésre válaszol: „Milyen szerepkör-, típus-, vontatóerő- (bollard pull, BP) és felszerelésnyilatkozatok vannak rögzítve ehhez a vontatóhajóhoz, honnan származnak, és mennyire aktuálisak?” Az oldal csak a nyilatkozatokat és azok forrásait mutatja; nem ad üzemi készenléti, alkalmassági, engedélyezési vagy biztonsági jóváhagyást. A felső jelvény is ezt mondja: „Forrásnyilatkozatok · az üzemi készenlét ismeretlen”.

## Ki használja
- **Vontatóhajó-kapitány:** a saját vontatóhajója szerepkör-, BP- és felszerelési kontextusának megerősítésére a munka megkezdése előtt (13.03 §3).
- **Vontatási műveleti koordinátor / Parti műveletek:** annak biztosítására, hogy a megfelelő vontatóhajó-kontextusban vannak, mielőtt a feladatáttekintőre és a feladatfelvételre lépnek (13.03 §7).
- **Személyzet:** a saját szerepkör-nézetük és a számukra nyitott Hajómód-képernyők megtekintésére (13.03 §6).

## Mielőtt elkezdené
- Vontatóhajó-mód és vontató-olvasási hozzáférés szükséges. Ennek hiányában az oldal ezt mutatja: „A hozzáférés nem érhető el.”
- Ha nincs kiválasztott kontextus, az oldal a **Vontató kiválasztása** alatt felsorolja a jogosult vontatóhajókat; nyisson meg egyet.
- A felszereléslistához felszerelés-olvasási hozzáférés is szükséges; enélkül a felszerelés forrása „nem érhető el vagy nincs engedélyezve” állapotú.

## Lépésről lépésre
1. Nyissa meg a vontatóhajót a **Vontató kiválasztása** listából. A címsor alatti **Vontatói kontextus** sor mutatja a vontatóhajó kódját, nevét és módját.
2. A **Szerepkör (szerver-hozzárendelés)** és a **Vontatóhajó-mód menünézet** részben olvassa el a szerver által Önhöz rendelt szerepkört és menünézetet.
3. A **Mód láthatósága** részben nézze meg, hogy a Hajómód-képernyők nyitva vannak-e. A **Hajómód képernyői** rész csak az Önnek engedélyezett képernyőkre hivatkozik.
4. A **Vontató típusa**, a **Típusforrás / revízió** és a **Típusforrás megfigyelése / szabály** alapján ellenőrizze, melyik forrásból és revízióból származik a típus.
5. Olvassa el a **Profil BP (nem ellenőrzött)**, a **BP-forrás / verzió / eredet** és a **BP-frissesség / megfigyelés** értékét. Ez megadott érték, nem mért érték.
6. A **Felszerelési kontextus** részben tekintse meg a felszerelésnyilatkozatokat, azok szintjeit, revízióit és eredetét.
7. Az oldal lapfüleivel lépjen a **Feladatáttekintő** vagy (írási hozzáféréssel) a **Feladatfelvétel** oldalra, vagy (írási hozzáféréssel) nyomja meg az **Új igény** gombot.

## Mit jelentenek az állapotok
- **Ismeretlen — nincs szerepkör-hozzárendelés:** a szervernek nincs Önre vonatkozó szerepkör-hozzárendelése ezen a vontatóhajón. Ez nem azt jelenti, hogy „nincs szerepkör, nincs probléma”; adminisztrátori hozzárendelés szükséges.
- **A szerepkörnevek nem érhetők el / A menünézet nem érhető el:** a forrás jelenleg nem olvasható. A hiányzó információt a rendszer nem tekinti üresnek.
- **Vontatóhajó-módhoz nincs nézet beállítva:** ebben a módban nincs Önre vonatkozó menünézet meghatározva.
- **Vontatóhajó-módban rejtve. Módok közötti szerepkör szükséges.:** a Hajómód-képernyőkhöz kifejezetten engedélyezett, módok közötti szerepkör szükséges.
- **Profil BP (nem ellenőrzött):** a megadott vontatóerő. Nem ellenőrzött vizsgálati eredmény.
- **Nem érkezett felszerelésnyilatkozat. Az ellenőrzött üzemi alkalmasság ismeretlen marad.:** az üres lista soha nem jelenti azt, hogy a felszerelés alkalmas.
- **Ismeretlen / Nem érhető el:** a forrásérték hiányzik vagy nem olvasható. Az Ismeretlen soha nem helyettesíti a „Kész” vagy a „Nem” értéket.

## Amit nem tehet meg / figyelem
- Ezen az oldalon semmilyen mező nem szerkeszthető, és nem jön létre készenléti eredmény. Az üzemi döntés és a biztonsági jogkör a fedélzeten tartózkodó Vontatóhajó-kapitánynál marad (13.03 §3).
- Egy parti kiosztási javaslat soha nem írja felül a Vontatóhajó-kapitány fedélzeti biztonsági döntését (13.03 §7).
- Még megadott, módok közötti jogosultság esetén is minden Hajómód-képernyőhöz saját szerveroldali engedély szükséges.
- Offline állapotban az aktuális profilforrások nem jelennek meg; csatlakozzon újra a hozzáférés és a forrásverziók újraellenőrzéséhez.

## Ha valami nem sikerül
- **Access is not available:** (a hozzáférés nem érhető el) ellenőrizze szerepkörét, vontatóhajó- és objektumkontextusát; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **"Current identity and vessel access must be checked" riasztás:** nyomja meg a **Hozzáférés újraellenőrzése** gombot.
- **Data unavailable:** (az adatok nem érhetők el) ellenőrizze a kapcsolatot, és nyomja meg az **Újra** gombot.
- **Unexpected BP or equipment values:** (váratlan BP- vagy felszerelésértékek) jegyezze fel a forrás- és verziómezőket, és tekintse át őket a forrás felelősével; ha egy rekord nem ellenőrizhető, vizsgálja meg a bizonyítékait és eredetét (13.03 §10).

## Kapcsolódó oldalak
