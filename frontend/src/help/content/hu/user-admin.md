---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## Mire szolgál ez az oldal
A Users and permissions (felhasználók és jogosultságok) oldal a munkaterület **tagságait** kezeli. Arra a kérdésre válaszol: „Mit láthat és tehet ez a személy, és mely hajókon?” Maga a fiók (felhasználónév, jelszó, MFA) a központi identitásszolgáltatóban (IdP) található, nem a POSEIDON-ban. Ez az oldal **meghívja** ezt a fiókot egy tagságba, és szerepkörcsomagokat, jogosultságokat és hajóhatókört ad (13.03 §9). Az új tagot meghívják: a meghívás semmit nem ad, amíg a személy be nem jelentkezik és el nem fogadja.

## Ki használja
- **Tenant administrator:** (bérlőadminisztrátor) a legkisebb jogosultság elve szerint rendel hozzá felhasználói, szerepkör-, hajó- és objektumhatókört, meghív új tagokat, és kezeli a delegálásokat és a felfüggesztést (13.03 §9).
- A tagság-olvasási hozzáféréssel rendelkező többi felhasználó megtekintheti a listát, de nem módosíthatja.
- A **meghívott személy** csak a meghívó hivatkozást nyitja meg; ezt az oldalt soha nem látja.

## Mielőtt elkezdené
- A megtekintéshez tagság-olvasási jogosultság szükséges; a módosításhoz tagságkezelési és bérlőszintű kezelési jogosultság is kell. Csak olvasási hozzáféréssel ezt látja: „Megtekintheti, de nem módosíthatja a tagságokat.”
- A személynek fiókkal kell rendelkeznie az identitásszolgáltatónál. Az **Expected IdP subject (optional)** a meghívást erre az egy fiókra korlátozza; enélkül bárki elfogadhatja, aki a hivatkozással bejelentkezik.
- Tervezzen megbízható csatornát a meghívó hivatkozás átadására (például a szervezete saját üzenetküldő rendszerét). A POSEIDON nem küld e-mailt.
- Csak olyan jogosultságokat adhat, amelyekkel Ön is rendelkezik. A saját tagságát egy másik adminisztrátor szerkeszti.
- A tagságkezeléshez élő szerverkapcsolat szükséges; offline állapotban semmi nem módosítható.

## Lépésről lépésre
1. Tekintse meg a tagokat a **Members** listában. Szűrjön **Állapot** (például **Invited**) és **Role bundle** szerint, majd nyomja meg az **Apply** gombot. Kereséshez adjon meg egy tagság- vagy fiókazonosító-előtagot vagy a pontos IdP-alanyt, és nyomja meg a **Keresés** gombot.
2. Nyisson meg egy tagot a **Megnyitás** gombbal. Tekintse át az **Effective permissions**, a **Vessel scope**, az **Invitation**, a **Grant history** és a **Delegations** részt.
3. Tag meghívásához nyomja meg az **Add member** gombot. Ellenőrizze az **IdP issuer** értékét, szükség szerint adja meg az **Expected IdP subject (optional)** értékét, válasszon legalább egy **Role bundle** értéket, szükség esetén adjon hozzá **Individual permissions** jogosultságokat, és állítsa be a hatókört (bérlőszintű vagy legalább egy hajó). Ezután nyomja meg a **Create membership** gombot.
4. A tag **Invited** állapotban nyílik meg az **Invitation link** hivatkozással. Nyomja meg a **Copy invitation link** gombot, és adja át a hivatkozást. A hivatkozás csak most jelenik meg; egyszer használható, és 24 óra után lejár.
5. Ha a hivatkozás elveszett vagy lejárt, nyomja meg a **Create a new invitation link** gombot; az előző hivatkozás ezzel érvényét veszti. A meghívás visszavonásához nyomja meg a **Revoke invitation** gombot.
6. A meghívott személy megnyitja a hivatkozást, bejelentkezik, ellenőrzi, mit ad a meghívás, és megnyomja az **Accept invitation** gombot. Újbóli bejelentkezés után az Ön által beállított jogosultsággal dolgozik; a tag ezután **Aktív** és **Invitation accepted** állapotot mutat.
7. Egy meglévő tag jogosultságának módosításához nyomja meg az **Edit grant**, majd a **Save grant** gombot. Minden mentés új jogosultság-revíziót hoz létre; a régebbi revíziók a **Grant history** részben maradnak.
8. Ideiglenes átadáshoz használja a **Delegate from this member** lehetőséget, és töltse ki a megbízottat, a **Valid from**, az **Érvényes eddig**, az **Indok** és a **Delegated permissions** mezőt.
9. A hozzáférés leállításához használja a **Lifecycle** részben a **Felfüggesztés** gombot; a **Folytatás** újra megnyitja. A **Revoke membership** végleges.

## Mit jelentenek az állapotok
- **Invited:** a tagság a meghívott személyre vár. Semmit nem ad, és a fiókot sehol nem foglalja le, más szervezetben sem.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** a legutóbbi meghívó hivatkozás állapota. Egy lejárt, lecserélt vagy visszavont hivatkozás soha nem fogadható el.
- **Aktív / Felfüggesztve / Visszavonva:** a tagság életciklusa. Egy felfüggesztett vagy visszavont tag következő kérését a rendszer elutasítja.
- **Grant source — Managed here:** a jogosultságot ezen az oldalon adták, és itt módosítható.
- **Grant source — Operator configuration:** a jogosultság a szerverkonfigurációból származik (bootstrap/break-glass), és itt nem szerkeszthető.
- **Grant source — No grant:** létezik rekord, de semmit nem ad.
- **Not held by you / You do not hold every permission of this bundle:** ezt a jogosultságot vagy csomagot nem adhatja, mert Ön nem rendelkezik vele.
- **Saved. The change takes effect on the member's next request.** (Mentve. A módosítás a tag következő kérésétől lép életbe.) Egy meghívott tag hozzáférése az elfogadás utáni következő bejelentkezéskor kezdődik.

## Amit nem tehet meg / figyelem
- Nem adhat olyan jogosultságot, hajót vagy bérlőszintű hozzáférést, amellyel Ön nem rendelkezik.
- A saját tagságát nem módosíthatja; az utolsó aktív adminisztrátor nem távolítható el.
- Egy fiók csak egy szervezetben rendelkezhet elfogadott tagsággal. Egy meghívás soha nem helyez át olyan fiókot, amely már máshová tartozik; a meghívott személy ilyenkor csak azt látja, hogy a meghívás nem fogadható el.
- Kezelje a meghívó hivatkozást kulcsként: bárki, aki birtokolja és bejelentkezik, elfogadhatja, hacsak nem adott meg várt alanyt. Ne tegye közzé megosztott csatornákon. Mindig a teljes hivatkozást adja át: a **#** utáni titkos részt csak a böngésző olvassa, és soha nem jut el a szerverhez vagy annak naplóihoz.
- A döntési, ellenőrzési és jóváhagyási jogosultságok soha nincsenek csomagban. Ezeket csak olyan adminisztrátor adhatja meg egyenként, aki maga is rendelkezik velük. A kijelöléshez kötött műveletekhez (például vontatási vagy indulási döntések) üzemeltetői kijelölés is szükséges. Egy szerepkörcsomag megadása senkit nem jelöl ki Kapitánynak vagy ellenőrzőnek.
- Nem hagyhat jóvá olyan delegálást, amelyben Ön a forrás vagy a megbízott. Egy delegálás soha nem haladja meg a forrás aktuális jogkörét, és soha nem bérlőszintű.
- A POSEIDON nem tárol jelszavakat, a meghívó hivatkozásból csak ujjlenyomatot őriz meg, és az IdP-alanyokat soha nem jeleníti meg a listában.

## Ha valami nem sikerül
- **This identity is configured by the operator, already a member here or already invited here.** (Ezt az identitást az üzemeltető konfigurálta, vagy már tag, vagy már meghívták ide.) Nyissa meg inkább a meglévő tagot, vagy először vonja vissza a régi meghívást.
- **The invited person sees "This invitation cannot be accepted":** (a meghívott személy azt látja, hogy a meghívás nem fogadható el) a hivatkozás lejárt, felhasználták, lecserélték vagy visszavonták, másik fiókhoz tartozik, vagy a fiókja már egy szervezethez tartozik. Ellenőrizze az **Invitation** állapotát, és szükség esetén nyomja meg a **Create a new invitation link** gombot.
- **The link was not copied:** (a hivatkozás nem lett kimásolva) jelölje ki a hivatkozásmezőt, és másolja ki kézzel.
- **Exceeds your own authority:** (meghaladja a saját jogkörét) válasszon szűkebb csomagot vagy hatókört, vagy kérjen meg egy adminisztrátort, aki rendelkezik a jogosultsággal.
- **The record changed meanwhile:** (a rekord időközben megváltozott) nyomja meg a **Reload current state** gombot, és végezze el újra a módosítást.
- **The outcome is unknown. Retry with the same request or reload.** (Az eredmény ismeretlen. Próbálja újra ugyanazzal a kéréssel, vagy töltse újra.) Ugyanazt a parancsot biztonságosan újraküldheti.
- **Not the trusted identity provider:** (nem a megbízható identitásszolgáltató) ellenőrizze a kibocsátót. Ha a fiók másik IdP-ből származik, forduljon a rendszeradminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
