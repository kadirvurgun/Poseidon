---
key: sources
title: Forráspiszkozatok
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
A Forráspiszkozatok oldal **verziózott piszkozatrekordként** tárolja azokat a szabályozási és szabályforrásokat (egyezmény, lobogó, osztályozó társaság, vállalati vagy helyi szabály), amelyekre a szervezete támaszkodik. Arra a kérdésre válaszol: „Melyik hivatalos forrás melyik revízióján alapul ez a követelmény?” A felső értesítés érvényes: „Bérlői szintű PISZKOZAT · Megadott és nem ellenőrzött. A dátumok és hashértékek nem ruháznak fel jogi hatállyal. A forráshivatkozások szövegként tárolódnak.” (13.03 §8).

## Ki használja
- **Szabályozási / megfelelőségbiztosítási felhasználó:** nyilvántartásba veszi a hivatalos, verziózott forrást, és megadja annak hatálybalépési dátumát és életciklusát (13.03 §8).
- **DPA / HSEQ:** elolvassa, hogy lássa, melyik forrásrevízióra hivatkozik egy audit vagy megállapítás (13.03 §4).

## Mielőtt elkezdené
- A megtekintéshez bérlőszintű olvasási és szabályozási piszkozat-olvasási jogosultság szükséges. Piszkozatok létrehozásához és mentéséhez ezenfelül szabályozási piszkozat-írási jogosultság is kell; enélkül az **Új piszkozat** gomb nem jelenik meg.
- Az alnézetekhez saját jogosultságok kellenek, mindig bérlőszintű olvasási hatókörrel. Az **Életciklus és alkalmazhatóság** alkalmazhatóság-olvasási jogosultságot igényel; a szabályok szerkesztéséhez szabályírási, a felülvizsgálat kéréséhez szabály-felülvizsgálati, az értékeléshez alkalmazhatóság-értékelési jogosultság kell; a minősítéshez és a tényigazoláshoz szabályozási minősítési vagy tényigazolási jogosultság, valamint a szerveren rögzített kijelölés szükséges. A **Kontrollok és bizonyítékok** kontroll-, bizonyíték-, fájl- és alkalmazhatóság-olvasási jogosultságot igényel; a kapcsoláshoz kontroll- vagy bizonyítékírási jogosultság kell a hajóra vonatkozó szerkesztési joggal, a jóváhagyáshoz pedig kontroll-jóváhagyási jogosultság.
- Tartsa kéznél a forrás hivatalos szövegét és verzió-/módosítási adatait. A POSEIDON nem tölti le és nem ellenőrzi a forrás szövegét; Ön adja meg.
- A mentéshez élő szerverkapcsolat szükséges. Offline állapotban a módosításai a lapon megmaradnak, de nem kerülnek mentésre.

## Lépésről lépésre
1. Tekintse meg a rögzített forráspiszkozatokat a listában. Kereséshez írjon a **Search drafts** mezőbe, és nyomja meg a **Keresés** gombot. Az oldalak között az **Előző** és a **Következő** gombbal léphet.
2. Új forráshoz nyomja meg az **Új piszkozat** gombot.
3. A **Draft fields** részben először minden mezőhöz válasszon **Állapot**ot: **Hiányzik**, **Ismeretlen** vagy **Known (declared)**. Értéket csak a **Known (declared)** kiválasztása után adhat meg. A mezők: **Cím**, **Megadott felhatalmazás**, **Forráshivatkozás (csak szöveg)**, **Verzió- / módosításnyilatkozat**, **Megadott hatálybalépési dátum**, **Nyilatkozat / eredet** és **Forrásmegfigyelés időpontja (UTC-eltolással)**.
4. Nyomja meg a **Piszkozat mentése** gombot. Sikeres mentés esetén ezt látja: „A piszkozat mentve. Nem ellenőrzött.”; nyissa meg a rekordot az **Open saved draft and history** segítségével.
5. Nyisson meg egy meglévő piszkozatot a listából, módosítsa, és mentse újra. Minden mentés új **Revízió**t hoz létre; a régebbi revíziók a tartalmukkal és az **Integritási hashérték (nem felhatalmazás)** értékkel együtt megmaradnak a **Megváltoztathatatlan előzmények** részben.
6. Az alkalmazhatósági szabályokhoz vagy a kontroll–bizonyíték lánchoz a lista feletti **Életciklus és alkalmazhatóság** vagy **Kontrollok és bizonyítékok** részt használja. A visszatéréshez nyomja meg a **Back to register** gombot.

## Mit jelentenek az állapotok
- **PISZKOZAT:** a listában és a rekord fejlécében megjelenő tárolt állapot. Ezen az oldalon minden forrásrekord piszkozat; nem hivatalos elfogadás és nem hatálybalépési döntés.
- **Hiányzik:** a mezőhöz még nem adtak meg információt. A későbbi lépések (például egy alkalmazhatósági értékelés) blokkolva vannak, amíg kötelező adat hiányzik.
- **Ismeretlen:** az információt keresték, de nem ismert. A rendszer az Ismeretlen értéket soha nem értelmezi „nem”-ként vagy „nem alkalmazható”-ként; ha kritikus adat ismeretlen, az eredmény felülvizsgálatban marad (REVIEW_REQUIRED).
- **Known (declared):** Ön adta meg az értéket. Egy nyilatkozat nem ellenőrzött információ.
- **Integritási hashérték (nem felhatalmazás):** azt mutatja, hogy egy revízió tartalma nem változott; nem bizonyítja a jogi érvényességet.

## Amit nem tehet meg / figyelem
- Egy mentett revíziót nem módosíthat. Ha a forrás megváltozik, mentsen új revíziót; a követelmények a régebbi revízióhoz kötve maradnak, és az előzmények megmaradnak (13.03 §1).
- Ne feltételezze, hogy az osztályozó társasági, lobogó szerinti, vállalati és helyi szabályforrások helyettesítik egymást; mindegyiket külön forrásként rögzítse (13.03 §8).
- Ez az oldal hivatalosan nem fogad be forrást, és senkinek nem ad megfelelőségi, engedélyezési vagy készenléti eredményt. A forrás befogadása és a független felülvizsgálat emberi jogkör.
- A hatálybalépési dátumok és a hashértékek nem ruháznak fel jogi hatállyal.

## Ha valami nem sikerül
- **Permission denied.** Ellenőrizze szerepkörét és bérlői hatókörét; szükség esetén forduljon a bérlőadminisztrátorhoz (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Valaki Ön előtt módosította a rekordot. Nyissa meg az aktuális revíziót a **Mentett piszkozat újratöltése** gombbal, és végezze el újra a módosítást.
- **Check field states, bounds, dates and source revision.** Egy mezőérték vagy dátumformátum érvénytelen.
- **Service unavailable. Your draft is retained.** Amikor a kapcsolat helyreáll, küldje el újra ugyanazt a parancsot a **Retry frozen command** gombbal.
- **Sign in to continue; retain your draft.** A munkamenete lejárt; használja a **Bejelentkezés** hivatkozást.
- Ha egy alkalmazáshiba továbbra is fennáll, jelentse a támogatásnak a korrelációs azonosítót, a képernyőt, az időpontot és a lépéseket (13.03 §10).

## Kapcsolódó oldalak
