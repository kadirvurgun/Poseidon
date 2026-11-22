---
key: audit-ready
title: Stopa připravená k auditu
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka Stopa připravená k auditu zobrazuje řetězce stopy, které vám byly pro plavidlo přiděleny, fázi po fázi: Požadavek → kontrolní opatření → důkaz → ověření → rozhodnutí, se stavy, časovými údaji a deklarovanými odkazy na zdroj. Odpovídá na otázku: „Na kterém požadavku tento důkaz stojí, kdo ho ověřil a jaké rozhodnutí bylo zaznamenáno?“ Zaznamenaný řetězec je lidský záznam, nikoli schválení ani prohlášení o připravenosti (13.03 §4, §8).

## Kdo ji používá
- **Regulatory / Assurance user:** kontroluje, že v řetězci Požadavek→Kontrolní opatření→Důkaz→Ověření nezůstal žádný osiřelý záznam (13.03 §8).
- **DPA / HSEQ:** při přípravě na audit, PSC nebo inspekci předkládá stopu Zjištění→Opatření→Ověření s jejími důkazy (13.03 §4).

## Než začnete
- Stránka vyžaduje oprávnění ke čtení auditu a ke čtení lidské stopy a navíc platné jmenování čtenáře stopy pro dané plavidlo. Samotné oprávnění nestačí; bez jmenování se nezobrazí žádný řetězec.
- Vyberte správné plavidlo. Seznam zobrazuje pouze řetězce přidělené vám.
- Stránka se načítá přes živé připojení. V režimu offline mohou být zobrazená data zastaralá.

## Krok za krokem
1. Vyberte plavidlo v poli **Plavidlo** (**Vybrat plavidlo**).
2. Otevřete řetězec ze seznamu **Přidělené řetězce**.
3. V části **Fáze stopy** rozbalte každou fázi (Požadavek, Kontrolní opatření, Důkaz, Ověření, Rozhodnutí) a zkontrolujte **Stav**, **Zaznamenáno**, **Revize**, **Hash obsahu** a **Odkaz na zdroj (deklarovaný, neověřený)**.
4. Pokud jsou uvedeny **Otevřené položky**, předejte kódy vlastníkovi příslušného záznamu.
5. Pokud existují čitelné přidělené řetězce, sestavte v části **Auditní balíček** balíček pomocí **Sestavit nový balíček**, otevřete ho ze seznamu **Sestavené balíčky** a v případě potřeby použijte **Exportovat JSON**.
6. Stiskněte **Zkusit znovu** v záhlaví, abyste data obnovili.

## Co znamenají stavy
- **Zaznamenaný řetězec — pouze lidský záznam, nikoli schválení:** každá fáze je zaznamenána. To neprohlašuje plavidlo ani operaci za připravené nebo vyhovující.
- **Vyžaduje kontrolu:** alespoň jedna fáze chybí, je neznámá nebo nedostupná. Neznámý nebo nečitelný vstup se nikdy nezobrazuje jako úplný.
- **Nezaznamenáno:** pro tuto fázi neexistuje žádný záznam; například dosud nebylo zaznamenáno žádné ověření ani rozhodnutí.
- **Nedostupné / NEZNÁMÉ:** záznam nyní nelze přečíst; jeho obsah se nepředpokládá.
- **Zastaralé: vstup byl nahrazen nebo vypršel; vyžaduje kontrolu.** Důkaz nebo zdroj se později změnil; zkontrolujte řetězec znovu.
- **Odkaz neuveden:** nebyl zadán žádný odkaz na zdroj; odkaz je deklarace, nikoli ověřený fakt.
- Ověřený důkaz neznamená, že opatření bylo účinné: Ověřeno ≠ Účinné (13.03 §4).
- **Auditní balíček:** „Systémová kompilace; nejde o oficiální záznam“. Položky mimo vaše jmenování se pouze započítávají, nezobrazují se; položky **Nahrazeno (uchováno pro sledovatelnost)** se nikdy nepovažují za aktuální.

## Co nemůžete dělat / upozornění
- Na této stránce nemůžete upravovat záznamy, ověřovat ani rozhodovat; je pouze pro čtení. Ověření a rozhodnutí zaznamenává jmenovaná osoba na jejich vlastních stránkách.
- Nevidíte řetězce, které vám nebyly přiděleny; skryté řetězce se nikdy nejmenují a nedostupné přidělené řetězce se zobrazují pouze jako počet.
- Hashe integrity a kontextu nejsou pravomocí ani schválením.

## Když se něco pokazí
- **Pro toto zobrazení nemáte oprávnění.** Zkontrolujte svou roli, plavidlo a kontext jmenování; obraťte se na Tenant Admin (13.03 §10).
- **Pro toto plavidlo vám nejsou přiděleny žádné řetězce.** Zkontrolujte, zda jste vybrali správné plavidlo; pokud je jmenování potřeba, obraťte se na odpovědnou osobu.
- **Stopu nelze načíst.** Stiskněte **Zkusit znovu**.
- **Řetězec nenalezen nebo již pro vás není viditelný.** Vaše jmenování mohlo skončit nebo záznam opustil váš rozsah.
- Pokud záznam nelze ověřit, zkontrolujte jeho důkazy a původ a v případě potřeby se obraťte na ověřovatele (13.03 §10).

## Související stránky
