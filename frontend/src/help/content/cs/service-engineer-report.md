---
key: service-engineer-report
title: Servisní a technické zprávy
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka zaznamenává a uvádí **zprávy ze servisních návštěv a návštěv techniků** na zvoleném plavidle. Odpovídá na otázku: „Kdo provedl jakou práci na kterém zařízení a pro který pracovní příkaz, jaké díly a měření byly zaznamenány a je zpráva ověřena?“ Zpráva je propojena s ID zařízení, pracovním příkazem a závadami, takže je svázána s historií pracovního příkazu a zařízení (13.03 §5). Ověřená zpráva je lidský záznam; neuzavírá závadu ani pracovní příkaz.

## Kdo ji používá
- **Vrchní strojník / Technical Superintendent:** propojuje zprávy s historií zařízení a pracovních příkazů, sleduje, co chybí, a ověřuje zprávy, které sám nevytvořil (13.03 §5).
- **Strojník:** po servisu zadá zprávu jako koncept a odešle ji k ověření (13.03 §6).

## Než začnete
- Pokud není zvoleno žádné plavidlo, stránka zobrazí **Vyberte plavidlo.** a uvede plavidla ve vašem rozsahu. Zvolte správné plavidlo.
- K zobrazení zpráv potřebujete přístup ke čtení servisních zpráv a zařízení. Tlačítko **Nová zpráva** se zobrazuje pouze s přístupem k zápisu a při online připojení.
- Mějte připravená ID zařízení, pracovního příkazu, závady a souboru s doklady. Pole pracovního příkazu, závady a dokladů se zobrazují pouze tehdy, pokud můžete tyto záznamy číst.

## Krok za krokem
1. Otevřete zprávu ze seznamu **Zprávy**, nebo zahajte nový koncept pomocí **Nová zpráva**.
2. Vyplňte **Č. zprávy**, **Poskytovatel služby**, **Strojník**, **Datum servisu**, **Důvod a rozsah** a **Provedená práce**.
3. V případě potřeby použijte **Přidat díl** a **Přidat měření** k zadání použitých dílů a měření. Pokud výsledek měření neznáte, ponechte jej jako **Neznámé**.
4. V části **Vazby** zadejte **ID zařízení**, **ID pracovního příkazu** a ID propojených závad a souborů s doklady (jedno na řádek).
5. Uložte pomocí **Uložit koncept**. Pokud zpráva zobrazuje seznam **Chybí před odesláním**, nejprve tyto položky doplňte.
6. Odešlete zprávu pomocí **Odeslat k ověření**.
7. U odeslané zprávy, kterou jste nevytvořili, napište **Důvod** o délce alespoň 10 znaků a použijte **Zaznamenat ověření** nebo **Vrátit autorovi**.
8. Chcete-li změnit ověřenou zprávu, napište důvod a použijte **Opravit (nová revize)**. Každá revize zůstává v **Neměnná historie**.

## Co znamenají stavy
- **Draft / Submitted / Returned:** životní cyklus zprávy. Odeslání není ověření.
- **Verified (recorded human decision) / Verified by a non-author:** oprávněná osoba, která zprávu nevytvořila ani neodeslala, zaznamenala ověření. Dokončeno a Ověřeno není totéž.
- **Čeká na ověření:** zpráva byla odeslána a dosud není ověřena.
- **Ověřena dřívější revize, tato ne:** po opravě vyžaduje nová revize opět ověření.
- **current revision / changed since linked / unavailable:** stav propojeného zařízení, pracovního příkazu nebo závady ve srovnání s revizí, která byla propojena.
- **Přijatý přenos, ne ověřený důkaz:** přiložený soubor je pouze přijatý přenos.
- Výsledek měření **Neznámé** neznamená „V mezích“.

## Co nemůžete dělat / upozornění
- Nemůžete ověřit zprávu, kterou jste vytvořili nebo odeslali; server to odmítne.
- Ověřená zpráva neuzavírá závadu ani pracovní příkaz a neukazuje, že je zařízení bezpečné.
- Originály dokladů se nemění; oprava otevírá novou revizi a stará revize zůstává v historii (13.03 §1).
- Offline se nic nezaznamenává. Když se připojení obnoví, **Zopakovat stejný příkaz** odešle stejný příkaz znovu.

## Když se něco pokazí
- **Required data or records are missing:** doplňte seznam chybějících položek a uložte znovu.
- **Someone else changed this record:** pomocí **Znovu načíst** otevřete nejnovější verzi a poté uložte znovu.
- **You do not have the server permission for this action or record:** zkontrolujte svou roli a kontext plavidla; v případě potřeby kontaktujte administrátora tenanta (13.03 §10).
- **A report cannot be verified:** zkontrolujte propojené záznamy a soubory s doklady; obraťte se na oprávněného ověřovatele (13.03 §10).
- **The record could not be loaded:** pošlete podpoře obrazovku, čas a kroky (13.03 §10).

## Související stránky
