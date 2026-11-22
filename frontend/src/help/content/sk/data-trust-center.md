---
key: data-trust-center
title: Centrum dôveryhodnosti údajov
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Centrum dôveryhodnosti údajov zobrazuje **signály kvality údajov** pre kontroly, dôkazy a polia profilu plavidla vybraného plavidla. Odpovedá na otázku: „Ktoré záznamy na tomto plavidle chýbajú, sú zastarané, rozporné, neoverené alebo osirelé a aké informácie o zdroji a aktuálnosti to uvádzajú?“ Signál dôveryhodnosti nie je overením príslušného záznamu. Stránka nikdy nič neoznačí ako pripravené.

## Kto ju používa
- **Regulatory / Assurance user:** aby v reťazci Požiadavka → Kontrola → Dôkaz → Overenie nezostali osirelé ani neúplné záznamy (13.03 §8).
- **DPA:** sleduje medzery v dôkazoch a overeniach (13.03 §4).
- **Technický inšpektor / Kapitán:** vidia signály, ktoré držia posúdenie pripravenosti v stave vyžadujúcom posúdenie (13.03 §2, §5).

## Skôr než začnete
- V hornom kontextovom paneli skontrolujte, že je vybraná **správna spoločnosť** (13.03 §1).
- Stránka vyžaduje prístup na čítanie kontrol, dôkazov, súborov, uplatniteľnosti a regulačných konceptov nájomcu, všetko spolu, s rozsahom čítania pre celého nájomcu.
- Ak nie je vybrané žiadne plavidlo, stránka najprv zobrazí zoznam **Vybrať plavidlo**.

## Krok za krokom
1. Vyberte plavidlo zo zoznamu **Vybrať plavidlo**.
2. V časti **Zhrnutie** si prečítajte celkový stav (**Vyžaduje posúdenie** alebo **Žiadny podstatný signál**) a počty **Predmety podľa signálu**.
3. V tabuľke **Kontroly a dôkazy** skontrolujte pri každom predmete stĺpce „Zdroj / pôvod“, „Aktuálnosť“, „Overenie“ a „Signály“.
4. Všimnite si kód dôvodu vedľa odznaku signálu a záznam skontrolujte na jeho vlastnej stránke.
5. Signály polí profilu si pozrite v tabuľke **Polia profilu plavidla**.
6. Na ďalšie predmety sa dostanete pomocou **Predchádzajúce** / **Ďalšie**.
7. Ak sa zobrazí „Tieto signály môžu byť zastarané.“, stlačte **Znova načítať**.

## Čo znamenajú stavy
- **Chýba / Zastarané / Rozporné / Neoverené / Osirelé:** druh signálu na predmete. Signál je ukazovateľ kvality; nedokazuje, že záznam je správny alebo nesprávny.
- **Žiadny signál:** na tomto predmete sa nenašiel žiadny signál. Neznamená to, že záznam je overený.
- **Vyžaduje posúdenie:** existuje podstatný signál. Kým existujú podstatné signály, pripravenosť sa drží v stave vyžadujúcom posúdenie.
- **Žiadny podstatný signál:** nie je to vyhlásenie o pripravenosti.
- **Overené človekom:** oprávnená osoba overila dôkaz. Pochádza to z vlastného záznamu dôkazu; centrum nič neoveruje.
- **Neoverené / Overenie vypršalo / Overenie zamietnuté:** stav overenia dôkazu.
- **Pre kontroly neplatí:** zobrazuje sa v riadkoch kontrol; stav overenia sa týka riadkov dôkazov.
- **Čas pozorovania neznámy / Bez zásady aktuálnosti / Datované do budúcnosti:** aktuálnosť nebolo možné určiť alebo je dátum v budúcnosti. Žiadny z týchto stavov neznamená „aktuálne“.

## Čo nemôžete robiť / upozornenie
- Záznamy sa na tejto stránke neopravujú, neoverujú ani neuzatvárajú; to robí oprávnená osoba na vlastnej stránke záznamu.
- Ak boli posúdené iba prvé predmety, zhrnutie zostane v stave vyžadujúcom posúdenie.
- Predmety, ktoré nemôžete čítať, sa započítajú, ale nezobrazia, a zhrnutie potom zostane v stave **Vyžaduje posúdenie**. Záznam, ktorý nevidíte, neznamená, že problém neexistuje.
- V režime offline nemožno signály dôveryhodnosti údajov obnoviť.

## Ak sa niečo pokazí
- **Nemáte oprávnenie čítať signály dôveryhodnosti údajov:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).
- **Záznam nemožno overiť:** skontrolujte dôkaz a jeho pôvod; obráťte sa s tým na oprávneného overovateľa.
- **Neočakávaný signál:** odovzdajte kód dôvodu vlastníkovi kontroly alebo odbornému expertovi.
- **Signály dôveryhodnosti údajov nie sú dostupné:** použite **Skúsiť znova**; ak problém pretrváva, nahláste ho podpore so snímkou obrazovky, časom a vašimi krokmi.

## Súvisiace stránky
