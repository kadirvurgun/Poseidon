---
key: defects
title: Závady a ochranná opatření
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka závad a dočasných opatření uchovává **zaznamenané závady, omezení a deklarovaná dočasná opatření** vybraného plavidla. Odpovídá na otázku: „Jak kritická je tato závada, jaké ochranné opatření nebo provozní omezení je zaznamenáno, bylo nápravné opatření ověřeno a co stále brání uzavření?“ Každý krok je lidský záznam; stránka neuděluje žádnou bezpečnostní ani provozní pravomoc.

## Kdo ji používá
- **Vrchní strojník / Technický inspektor:** zaznamenávají ochranné opatření nebo omezení a dopad kritické závady na připravenost a sledují nápravné opatření (13.03 §5).
- **Kapitán:** vidí dopad závady v podobě provozního omezení (13.03 §2).
- **Strojník / posádka:** deklarují závadu, kterou našli (13.03 §6).

## Než začnete
- Čtení vyžaduje `defect.read` a `equipment.read`. Deklarace a záznamy životního cyklu vyžadují `defect.write`.
- Ověření vyžaduje `defect.verify`, účinnost `defect.effectiveness` a uzavření `defect.close`, vždy s přihlášeným lidským uživatelem.
- Vyberte správné plavidlo v **Vybrat plavidlo nebo remorkér**.

## Krok za krokem
1. Vyberte plavidlo. V případě potřeby vyfiltrujte **Zaznamenaná prohlášení** pomocí **Filtrovat vrácená pozorování vypršení** a otevřete prohlášení.
2. Pro novou závadu stiskněte **Nové prohlášení o závadě**, vyplňte pole a stiskněte **Uložit neposouzené prohlášení**. Neúplné koncepty lze uložit.
3. U uloženého prohlášení stiskněte **Zahájit životní cyklus** v části **Životní cyklus závady**.
4. Pomocí **Zaznamenat posouzení** zvolte kritičnost a napište vyjádření (nejméně 10 znaků).
5. Použijte **Zaznamenat opatření nebo omezení** a jako **Typ opatření** zvolte **Ochranné opatření** nebo **Provozní omezení**. U dočasného opatření zadejte vypršení (UTC).
6. Pomocí **Zaznamenat nápravné opatření** popište provedenou práci.
7. Oprávněná osoba, která nezaznamenala žádné nápravné opatření, zaznamená **Zaznamenat ověření** a poté **Zaznamenat účinnost**.
8. Když nezůstávají žádné **Překážky uzavření**, oprávněná osoba, která rovněž nezaznamenala žádné nápravné opatření, zaznamená **Uzavřít a vrátit do provozu**. Záznamy se zobrazí v části **Záznamy životního cyklu**.

## Co znamenají stavy
- **Zjištění / Posouzení / Ochranné opatření / Akce / Ověření / Účinnost:** fáze životního cyklu.
- **Uzavřeno (zaznamenané lidské rozhodnutí):** uzavření je lidské rozhodnutí; není to zelený výsledek „opraveno“.
- **Platí pravidlo kritičnosti:** kritickou závadu nelze uzavřít bez ochranného opatření.
- **Aktivní / Vypršelo — blokuje / Zrušeno / Ukončeno uzavřením:** stav ochranného opatření nebo omezení. Vypršelé dočasné opatření musí být obnoveno nebo zrušeno.
- **Kritická závada bez aktivního opatření / Kritičnost neznámá a žádné opatření:** výstrahy. Tyto závady se zobrazují také v části **Výstrahy stavu** v [centru PMS](/app/pms-command-center).
- **Neznámé:** kritičnost nebyla posouzena. Neznámé neznamená „Nízká“ ani „Nekritická“.

## Co nemůžete dělat / upozornění
- Pokud jste zaznamenali nápravné opatření, nemůžete zaznamenat ověření, účinnost ani návrat do provozu; ty vyžadují vlastní pravomoc a jinou osobu.
- Ověřeno, Účinné a Uzavřeno jsou samostatné lidské záznamy. Deklarovaná fáze nebo opatření neopravňuje k provozu, neověřuje účinnost ani nepovoluje uzavření.
- Pokud se prohlášení po ověření změní, uzavření se zablokuje a záznam se vrátí k posouzení.
- Kritičnost zařízení se nepřebírá jako posouzení této závady.
- V režimu offline nejsou aktuální zdroje závad dostupné; neexistuje žádné offline úložiště.

## Když se něco pokazí
- **Někdo jiný tento záznam změnil:** pomocí **Znovu načíst** přečtěte aktuální záznam a zaznamenejte znovu.
- **Výsledek uložení je neznámý:** **Zopakovat stejný příkaz** odešle stejný příkaz.
- **Nemáte serverové oprávnění pro tuto akci nebo záznam:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Překážky uzavření:** doplňte chybějící záznam v seznamu. Pokud záznam nelze ověřit, zkontrolujte důkazy s ověřovatelem (13.03 §10).

## Související stránky
