---
key: document-compare
title: Dokumentumrevíziók összevetése
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## Mire szolgál ez az oldal
Ez az oldal a kiválasztott hajóra vonatkozóan **egy szabályozott dokumentum két revízióját mutatja egymás mellett, csak olvasható módon**. Arra a kérdésre válaszol: „Miben különbözik ez a két revízió fájl- és metaadatszinten, és mi az egyes revíziók közzétételi, ellenőrzési és őrzési előzménye?” Az oldal a különbségeket mutatja; a változásokat nem hagyja jóvá, nem teszi közzé és nem ítéli meg. Egy revíziót nem léptet hatályba, és nem teszi egy korábbi revízió felváltójává. A bizonyítékok eredetijei soha nem változnak meg csendben; amikor egy új revízió felvált egy korábbit, a korábbi revízió közzétételi állapota Felváltva lesz, és az esemény megjelenik az őrzési előzményében (13.03 §1).

## Ki használja
- **Kapitány / gépüzemvezető / személyzet:** a hatályos revízió összevetésére az előzővel, és az olvasási tudomásulvétel rögzítésére (13.03 §2, §5, §6).
- **DPA / szabályozási és megfelelőségbiztosítási felhasználó:** a revízióelőzmények és az ellenőrzési rekordok felülvizsgálatára (13.03 §4, §8).

## Mielőtt elkezdené
- Ha nincs kiválasztva hajó, az oldal ezt írja: **Válasszon hajót a hatókörében.**, és listázza a hajókat. Válassza ki a megfelelő hajót.
- Az oldal megtekintéséhez fájlolvasási hozzáférés szükséges. A korlátozott tartalomhoz és az ISPS-korlátozott dokumentumokhoz ezenfelül saját jogosultság is kell.
- A tartalom betöltése és a tudomásulvétel rögzítése csak online működik.

## Lépésről lépésre
1. Nyisson meg egy dokumentumot a **Szabályozott dokumentumok** listából. A soron a besorolás és az **ISPS-korlátozott** címke látható.
2. A **Válasszon két revíziót** alatt válassza ki a **Bal (alap)** és a **Jobb (vizsgált)** revíziót. Az oldal először jobbra a legújabb, balra az azt megelőző revíziót javasolja.
3. Nyomja meg az **Összevetés** gombot. A felcseréléshez használja az **Oldalcsere** gombot.
4. Olvassa el a bájtszintű megállapítást és a **Metaadatok összevetése** táblázatot: **Fájltípus**, **Méret (bájt)**, **Tartalom SHA-256**, **Befogadás**, **Közzététel**, **Ellenőrzés** és az időmezők.
5. Tekintse át mindkét oldalon a **Jóváhagyási és őrzési előzmények** részt.
6. A tartalom megtekintéséhez nyomja meg a **Tartalom betöltése (hozzáférés naplózva)** gombot. A tartalom szerzőt azonosító vízjellel jelenik meg.
7. A **Load content** után, ha elolvasta a hatályos revíziót, jelölje be az **Elolvastam ezt a revíziót, és hozzáfértem a tartalmához.** jelölőnégyzetet, és nyomja meg a **Tudomásulvétel rögzítése** gombot.

## Mit jelentenek az állapotok
- **Nem közzétett / Hatályos / Felváltva:** a revízió közzétételi állapota. Egy nem hatályos revízió az **Üzemileg nem hatályos** jelölést mutatja.
- **Nem ellenőrzött / Érvényesnek ellenőrizve / Érvénytelennek ellenőrizve / Az ellenőrzés nem döntő:** a revízió ellenőrzési rekordja. A nem ellenőrzött nem jelenti azt, hogy „érvényes”.
- **Azonos / Változott / Nem összevethető:** egy metaadatsor eredménye. A nem összevethető nem jelenti azt, hogy „Azonos”.
- **Byte-identical files:** csak a bájtok azonosak; ez nem tartalmi felülvizsgálat.
- **Files differ at byte level:** azt, hogy melyik szabály változott, és mennyire jelentős, egy személynek kell felülvizsgálnia.
- **A tartalom az Ön szerepköre számára korlátozott; csak metaadatok.** a tartalom ennél a szerepkörnél nem nyílik meg.

## Amit nem tehet meg / figyelem
- Ezen az oldalon nem tehet közzé, nem hagyhat jóvá, nem léptethet hatályba és nem válthat fel dokumentumot.
- Az oldal nem értelmezi automatikusan a tartalmi különbségeket; azok jelentőségéről egy felhatalmazott személy dönt.
- Tudomásulvétel csak a hatályos revízióra rögzíthető, személyenként és revíziónként egyszer. Ez nem jóváhagyás és nem ellenőrzés.
- A nyomtatott példány nem szabályozott. A tartalom nem kerül gyorsítótárba, és nem marad meg offline.
- Offline állapotban az összevetés elavult, és új tartalom nem tölthető be.

## Ha valami nem sikerül
- **This document is not available to you:** ellenőrizze hajókontextusát és jogosultságát; szükség esetén forduljon a bérlői adminisztrátorhoz (13.03 §10).
- **You do not have permission for this action:** ellenőrizze szerepkörét és hajókontextusát (13.03 §10).
- **The record changed meanwhile:** használja az **Újratöltés** gombot a legújabb verzió megnyitásához.
- **A revision cannot be verified:** vizsgálja meg a bizonyítékot és az eredeti előzményeit; beszéljen egy felhatalmazott ellenőrzővel (13.03 §10).
- **The request failed:** küldje el a képernyőt, az időpontot és a lépéseket a támogatásnak (13.03 §10).

## Kapcsolódó oldalak
