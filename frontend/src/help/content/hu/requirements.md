---
key: requirements
title: Követelménypiszkozatok
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Követelménypiszkozatok oldal a forrásrevízióból levezetett **követelményeket** verziózott piszkozatként tárolja, és minden követelményt egy pontos forrásrevízióhoz köt. Arra a kérdésre válaszol: „Melyik forrás melyik revízióján alapul ez a követelmény, és mi a rögzített életciklusa?” A követelmény a Követelmény → Kontroll → Bizonyíték → Ellenőrzés lánc első eleme. A lánc egyetlen rekordja sem maradhat kapcsolat nélkül (árva) (13.03 §8).

## Ki használja
- **Szabályozási / megfelelőségbiztosítási felhasználó:** a követelményt a forrásához köti, kontextusbemenetekkel értékeli az alkalmazhatóságot, és teljesen tartja a láncot (13.03 §8).
- **DPA / HSEQ:** megállapítási és auditmunka során elolvassa egy követelmény alapját (13.03 §4).

## Mielőtt elkezdené
- A megtekintéshez bérlőszintű olvasási és szabályozási piszkozat-olvasási jogosultság szükséges; a szerkesztéshez ezenfelül szabályozási piszkozat-írási jogosultság is.
- Az alnézetekhez saját jogosultságok kellenek, mindig bérlőszintű olvasási hatókörrel. Az **Életciklus és alkalmazhatóság** alkalmazhatóság-olvasási jogosultságot igényel; a szabályok szerkesztéséhez szabályírási, a felülvizsgálat kéréséhez szabály-felülvizsgálati, az értékeléshez alkalmazhatóság-értékelési jogosultság kell; a minősítéshez és a tényigazoláshoz szabályozási minősítési vagy tényigazolási jogosultság, valamint a szerveren rögzített kijelölés szükséges. A **Kontrollok és bizonyítékok** kontroll-, bizonyíték-, fájl- és alkalmazhatóság-olvasási jogosultságot igényel; a kapcsoláshoz kontroll- vagy bizonyítékírási jogosultság kell a hajóra vonatkozó szerkesztési joggal, a jóváhagyáshoz pedig kontroll-jóváhagyási jogosultság.
- Először a **Forráspiszkozatok** oldalon rögzítse a forrást. Követelmény csak meglévő forrásrevízióhoz köthető.
- A mentéshez élő szerverkapcsolat szükséges; offline állapotban a módosítások a lapon megmaradnak.

## Lépésről lépésre
1. Tekintse meg a követelménypiszkozatokat a listában; keressen a **Search drafts** és a **Keresés** segítségével.
2. Új követelményhez nyomja meg az **Új piszkozat** gombot.
3. A **Draft fields** részben minden mezőhöz válasszon **Állapot**ot (**Hiányzik**, **Ismeretlen**, **Known (declared)**), és adja meg az ismert értékeket: **Szabályozási eszköz**, **Verzió- / módosításnyilatkozat**, **Megadott hatálybalépési dátum**, **Nyilatkozat / eredet**, **Forrásmegfigyelés időpontja (UTC-eltolással)**.
4. Az **Immutable source citation** részben írjon a **Find a source** mezőbe, és nyomja meg a **Search sources** gombot. Válassza ki a forrást a **Source draft** mezőben, majd a pontos revíziót az **Exact revision** mezőben. A kiválasztott revízió tartalmát a **Selected immutable source content** részben ellenőrizze.
5. Nyomja meg a **Piszkozat mentése** gombot. Amikor a rekord meg van nyitva, megjelennek a **Rögzített életciklus** és a **Megváltoztathatatlan előzmények** szakaszok.
6. Az alkalmazhatósághoz az **Életciklus és alkalmazhatóság**, a kontroll- és bizonyítékkapcsolatokhoz a **Kontrollok és bizonyítékok** részt használja. A **Kontrollok és bizonyítékok** részen belüli **Kritikus kapcsolatlan bejegyzések jelentése** felsorolja a követelménykapcsolat nélküli kritikus nyilatkozatokat; kapcsolja ezeket a rekordokat egy követelményhez.

## Mit jelentenek az állapotok
- **Rögzített életciklus:** az az állapot, amelyet a szerver a kötött szabályokból vezet le: **Piszkozat**, **Elfogadva**, **Jövőben hatályos**, **Hatályban**, **Felváltva**. Ha nem olvasható, **Nem érhető el** jelenik meg; a rendszer nem feltételez piszkozat vagy hatályos állapotot.
- „A kötött szabályok eltérő életciklus-állapotokat rögzítenek. A megjelenített érték szoftveres elsőbbségi sorrend; szakértői felülvizsgálat szükséges.” A megjelenített érték ilyenkor nem döntés; szakértői felülvizsgálatra van szükség.
- **Ismeretlen:** az információ nem ismert. Ha kritikus adat ismeretlen, az alkalmazhatósági eredmény **Felülvizsgálat szükséges** (REVIEW_REQUIRED) marad; soha nem számít „nem alkalmazható”-nak vagy „nem”-nek (13.03 §8).
- **Alkalmazhatósági felülvizsgálat szükséges / Hiányzik a követelménykapcsolat / Az ellenőrzés nem történt meg:** nyitott tételek a kontroll–bizonyíték láncban.
- **A teljesítés ismeretlen · Nem ellenőrzött · A hatásosság ismeretlen:** a teljesítés, az ellenőrzés és a hatásosság külön állapotok. Egy teljesített rekord ettől még nem ellenőrzött, és egy ellenőrzött rekord ettől még nem hatásos.

## Amit nem tehet meg / figyelem
- Követelményt nem köthet „a legújabb” forráshoz; mindig egy **Exact revision** kerül kiválasztásra. Amikor a forrás új revíziót kap, a régi kötés megmarad az előzményekben.
- Az alkalmazhatósági előnézet nem állapít meg megfelelőséget, készenlétet, engedélyt vagy jogi jóváhagyást: „ELŐNÉZET / PISZKOZAT. Az előrejelzések nem állapítanak meg megfelelőséget, készenlétet, engedélyt vagy jogi jóváhagyást.”
- Az életciklus módosítása az **Emberi felülvizsgálat kérése** útján történik; a forrás befogadásáról és egy minősített független felülvizsgáló dönt. A rendszer soha nem zárja le magától.
- Ne feltételezze, hogy az osztályozó társasági, lobogó szerinti, vállalati és helyi szabályforrások helyettesítik egymást (13.03 §8).

## Ha valami nem sikerül
- **Source selection unavailable.** Nyomja meg a **Retry sources** gombot.
- **Conflict. Your edits are retained. Reload only after discarding them.** Nyissa meg az aktuális revíziót a **Mentett piszkozat újratöltése** gombbal.
- **Check field states, bounds, dates and source revision.** Ha forrást választott, válassza ki annak pontos revízióját is.
- **History unavailable. Editor retained.** Nyomja meg a **Retry history** gombot.
- Ha az életciklus váratlan, tekintse át a kötött szabályokat és forrásrevíziókat; szükség esetén forduljon a kontroll felelőséhez vagy a szakterületi szakértőhöz (SME) (13.03 §10).
- Jogosultság hiányában ellenőrizze szerepkörét és bérlői hatókörét, és forduljon a bérlőadminisztrátorhoz (13.03 §10).

## Kapcsolódó oldalak
