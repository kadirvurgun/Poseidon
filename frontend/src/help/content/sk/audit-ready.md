---
key: audit-ready
title: Stopa pripravená na audit
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Stopa pripravená na audit zobrazuje reťazce stopy, ktoré sú vám pridelené pre plavidlo, fázu po fáze: Požiadavka → kontrola → dôkaz → overenie → rozhodnutie, so stavmi, časovými značkami a deklarovanými odkazmi na zdroj. Odpovedá na otázku: „Na ktorej požiadavke stojí tento dôkaz, kto ho overil a aké rozhodnutie bolo zaznamenané?“ Zaznamenaný reťazec je ľudský záznam, nie schválenie ani vyhlásenie o pripravenosti (13.03 §4, §8).

## Kto ju používa
- **Regulatory / Assurance user:** kontroluje, že v reťazci Požiadavka→Kontrola→Dôkaz→Overenie nezostal žiadny osirelý záznam (13.03 §8).
- **DPA / HSEQ:** pri príprave na audit, PSC alebo inšpekciu ukazuje stopu Zistenie→Opatrenie→Overenie spolu s dôkazmi (13.03 §4).

## Skôr než začnete
- Stránka vyžaduje oprávnenie na čítanie auditu a čítanie ľudskej stopy a navyše platné vymenovanie za čitateľa stopy pre dané plavidlo. Samotné oprávnenie nestačí; bez vymenovania sa nezobrazí žiadny reťazec.
- Vyberte správne plavidlo. Zoznam zobrazuje iba reťazce, ktoré sú pridelené vám.
- Stránka sa načítava cez živé pripojenie. V režime offline môžu byť zobrazené údaje zastarané.

## Krok za krokom
1. Vyberte plavidlo v poli **Plavidlo** (**Vybrať plavidlo**).
2. Otvorte reťazec v časti **Pridelené reťazce**.
3. V časti **Fázy stopy** rozbaľte každú fázu (Požiadavka, Kontrola, Dôkaz, Overenie, Rozhodnutie) a skontrolujte **Stav**, **Zaznamenané**, **Revízia**, **Hash obsahu** a **Odkaz na zdroj (deklarovaný, neoverený)**.
4. Ak sú uvedené **Otvorené položky**, odovzdajte kódy vlastníkovi príslušného záznamu.
5. Ak existujú čitateľné pridelené reťazce, zostavte balík v časti **Auditný balík** tlačidlom **Zostaviť nový balík**, otvorte ho v časti **Zostavené balíky** a v prípade potreby použite **Exportovať JSON**.
6. Na obnovenie údajov stlačte **Skúsiť znova** v hlavičke.

## Čo znamenajú stavy
- **Zaznamenaný reťazec — iba ľudský záznam, nie schválenie:** každá fáza je zaznamenaná. Neznamená to vyhlásenie, že plavidlo alebo operácia sú pripravené alebo v súlade.
- **Vyžaduje kontrolu:** aspoň jedna fáza chýba, je neznáma alebo nedostupná. Neznámy alebo nečitateľný vstup sa nikdy nezobrazuje ako úplný.
- **Nezaznamenané:** pre túto fázu neexistuje žiadny záznam; napríklad ešte nebolo zaznamenané žiadne overenie ani rozhodnutie.
- **Nedostupné / UNKNOWN:** záznam teraz nemožno prečítať; jeho obsah sa nepredpokladá.
- **Zastarané: vstup bol nahradený alebo vypršal; vyžaduje kontrolu.** Dôkaz alebo zdroj sa neskôr zmenil; reťazec skontrolujte znova.
- **Odkaz neuvedený:** nebol zadaný žiadny odkaz na zdroj; odkaz je vyhlásenie, nie overená skutočnosť.
- Overený dôkaz neznamená, že opatrenie bolo účinné: Overené ≠ Účinné (13.03 §4).
- **Auditný balík:** „Systémová kompilácia; nie je to oficiálny záznam“. Položky mimo vašich vymenovaní sa iba spočítajú, nezobrazia sa; položky **Nahradené (uchované pre sledovateľnosť)** sa nikdy nepovažujú za aktuálne.

## Čo nemôžete robiť / upozornenie
- Na tejto stránke nemôžete upravovať záznamy, overovať ani rozhodovať; je iba na čítanie. Overenia a rozhodnutia zaznamenáva vymenovaná osoba na ich vlastných stránkach.
- Nevidíte reťazce, ktoré vám nie sú pridelené; skryté reťazce sa nikdy nepomenúvajú a nedostupné pridelené reťazce sa zobrazujú iba ako počet.
- Hashe integrity a kontextu nie sú oprávnením ani schválením.

## Ak sa niečo pokazí
- **Na toto zobrazenie nemáte oprávnenie.** Skontrolujte svoju rolu, plavidlo a kontext vymenovania; obráťte sa na správcu nájomcu (Tenant Admin) (13.03 §10).
- **Pre toto plavidlo vám nie sú pridelené žiadne reťazce.** Skontrolujte, či ste vybrali správne plavidlo; ak je potrebné vymenovanie, požiadajte zodpovednú osobu.
- **Stopu nebolo možné načítať.** Stlačte **Skúsiť znova**.
- **Reťazec sa nenašiel alebo už pre vás nie je viditeľný.** Vaše vymenovanie mohlo skončiť alebo záznam opustil váš rozsah.
- Ak záznam nemožno overiť, skontrolujte jeho dôkaz a pôvod a v prípade potreby kontaktujte overovateľa (13.03 §10).

## Súvisiace stránky
