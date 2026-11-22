---
key: pilot-gate
title: Preskúmanie go/no-go pilotu
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka Preskúmanie go/no-go pilotu zobrazuje na jednom mieste stav brán potrebných pre pilot, počet otvorených P0 a pripravenosť podpory a zaznamenáva rozhodnutie go/no-go vymenovaného vlastníka produktu. Odpovedá na otázku: „Bola každá brána pilotu splnená s dôkazmi a bolo zaznamenané ľudské rozhodnutie?“ Platí upozornenie v hornej časti: „Advisory review only. Unknown or deferred gates and an unknown or open P0 recommend NO-GO. Only the appointed Product Owner records a decision.“ (Iba poradné preskúmanie. Neznáme alebo odložené brány a neznáme alebo otvorené P0 odporúčajú NO-GO. Rozhodnutie zaznamenáva iba vymenovaný vlastník produktu.)

## Kto ju používa
- **Vlastník produktu:** číta výsledok preskúmania a zaznamenáva rozhodnutie go/no-go s odôvodnením. Rozhodovanie vyžaduje platné vymenovanie za vlastníka produktu.
- **Podpora / Tenant Admin:** označujú položky pripravenosti podpory a pohotovosti s referenciou na dôkaz.
- 13.03 pre túto stránku nedefinuje samostatnú rolu; platia zásady správy nájomcu (najnižšie oprávnenia, riadená publikácia) (13.03 §9).

## Skôr než začnete
- Zobrazenie vyžaduje čítanie v rámci celého nájomcu a čítanie brán pilotu. Označenie položky podpory vyžaduje oprávnenie na záznam podpory; rozhodovanie vyžaduje oprávnenie rozhodovať a navyše platné vymenovanie za vlastníka produktu zriadené na serveri. Samotné oprávnenie nestačí.
- Podporu môže označiť alebo rozhodnúť iba ľudský účet; vymenovanie musí zodpovedať vašej aktívnej role a byť v období platnosti.
- Vstupy brán pochádzajú z iných stránok; napríklad brány Ship UAT a Tug UAT sa počítajú z potvrdených behov na stránke **Záznamy UAT podľa rolí**.
- Zaznamenávanie vyžaduje živé pripojenie; v režime offline je vypnuté.

## Krok za krokom
1. V časti **Preskúmanie brány pilotu** si prečítajte odporúčanie (**Poradné odporúčanie**), počet **Otvorené P0** a tabuľku brán: **Brána**, **Stav**, **Zdroj**, **Dôvody**.
2. V časti **Pripravenosť podpory a pohotovosti** vyberte pri každej položke **Stav**, pri stave **Pripravené** zadajte **Referencia dôkazu**, napíšte **Poznámka** a stlačte **Označiť**.
3. Ak ste vlastník produktu, vyberte rozhodnutie v časti **Zaznamenať rozhodnutie go/no-go** (**NO-GO**, **Podmienené GO**, **GO**), v poli **Vymenovanie** vyberte svoje vymenovanie za vlastníka produktu a napíšte **Odôvodnenie**. Pri **Podmienené GO** vyplňte **Podmienky (jedna na riadok)**.
4. Stlačte **Zaznamenať**. Rozhodnutie sa zobrazí v časti **Rozhodnutia vlastníka produktu** so svojím poradovým číslom.
5. Stlačením **Obnoviť** znova načítate údaje.

## Čo znamenajú stavy
- **Evidence threshold met (PASS):** zdroj brány spĺňa prahovú hodnotu dôkazov. Nie je to schválenie; rozhoduje človek.
- **Fail (FAIL)**, **Unknown (UNKNOWN)**, **Deferred (DEFERRED):** brána nie je splnená (nesplnená, neznáma, odložená). Neznáma alebo odložená brána sa nikdy nepočíta ako splnená a odporúča NO-GO.
- **Odporúča sa NO-GO:** aspoň jedna brána nie je PASS alebo je počet otvorených P0 neznámy alebo vyšší ako nula.
- **Spôsobilé na GO; čaká sa na ľudské rozhodnutie:** každá brána je PASS a počet otvorených P0 je nula. Systém nikdy nerozhoduje o GO; zaznamenáva ho iba vlastník produktu.
- **Neoznačené / Pripravené / Nepripravené:** posledný označený stav položky podpory.
- **Aktuálne / Zastarané: preskúmanie sa zmenilo:** rozhodnutie je viazané na preskúmanie v čase, keď bolo prijaté. Ak sa preskúmanie neskôr zmenilo, rozhodnutie sa zobrazí ako zastarané a treba ho prehodnotiť.

## Čo nemôžete robiť / upozornenie
- „GO je možné iba vtedy, keď je každá brána PASS a počet otvorených P0 je nula.“ Inak je možnosť GO vypnutá.
- Podmienené GO vyžaduje rovnaký stav ako GO (každá brána PASS, počet otvorených P0 je známy a rovný nule) a aspoň jednu podmienku. Nemožno ho zaznamenať, kým je niektorá brána nesplnená, neznáma alebo odložená, ani kým je P0 otvorené alebo neznáme. NO-GO možno zaznamenať vždy, bez podmienok.
- Systém, AI ani automatizácia nikdy nerozhodujú o pilote a nikdy nevymýšľajú brány ani schválenia. Brány pilotu a rozhodnutia sú ľudské záznamy.
- Zaznamenané rozhodnutie nemôžete zmeniť; nové rozhodnutie sa pridá s novým poradovým číslom.

## Ak sa niečo pokazí
- **Nemáte platné vymenovanie za vlastníka produktu. Rozhodnutia sú iba na čítanie.** Vymenovania sa zriaďujú na serveri; obráťte sa na správcu nájomcu (Tenant Admin) (13.03 §10).
- **K týmto záznamom nemáte prístup.** Skontrolujte svoju rolu a rozsah nájomcu.
- **Záznam sa zmenil alebo už bol použitý. Znova načítajte.** Preskúmanie sa zmenilo počas vášho rozhodovania; aktuálne preskúmanie otvorte pomocou **Znova načítať**. Zobrazuje sa to aj vtedy, keď sa stavy brán zmenili tak, že rozhodnutie už nie je povolené.
- **Výsledok je neistý. Opakovanie odošle rovnaký príkaz.** Pomocou **Skúsiť znova** bezpečne znova odošlete ten istý príkaz.
- **Odmietnuté pravidlami.** Niektorá brána je odložená.
- **Nepovolené.** Vaše vymenovanie nie je platné alebo váš účet nie je ľudský účet.
- **Služba nie je dostupná. Skúste to znova.** Ak problém pretrváva, nahláste podpore ID korelácie, čas a kroky (13.03 §10).

## Súvisiace stránky
