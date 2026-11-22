---
key: operational-downtime
title: Provozní odstávky
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Tato stránka zaznamenává **deklarovaná provozní přerušení** lodi (odstávky) spolu s původem jejich zdroje. Odpovídá na otázku: „Které přerušení bylo na této lodi deklarováno, kdy, z jaké příčiny a s jakou propojenou závadou nebo pracovním příkazem, a kde je jeho posouzení a uzavření?“ Stránka nerozhoduje o nároku na loss of hire, bezpečném provozu ani o žádném bezpečnostním uvolnění.

## Kdo ji používá
- **Vrchní strojník / Technický inspektor:** zaznamenávají odstávky způsobené technickou poruchou nebo údržbou a propojují je s historií závad nebo pracovních příkazů (13.03 §5).
- **Kapitán:** sleduje provozní dopad odstávky a posuzuje ji, je-li jmenován (13.03 §2).
- **Jmenovaný posuzovatel (kapitán, první důstojník, vrchní strojník, manažer flotily, DPA):** ověřuje záznam odstávky, vrací ho k opravě nebo ho uzavírá.

## Než začnete
- V horním kontextovém pruhu zkontrolujte, že je vybrána **správná společnost**; stránka uvádí pouze lodě ve vašem rozsahu.
- Čtení vyžaduje přístup ke čtení odstávek; otevření konceptu vyžaduje přístup k zápisu. Akce posouzení vyžadují také přístup k posuzování a platné jmenování do funkce pro tuto loď.
- Chcete-li otevřít propojenou odstávku, můžete na stránce závady nebo pracovního příkazu použít **Otevřít propojený koncept odstávky**.

## Krok za krokem
1. Vyberte loď ze seznamu „Vybrat loď“. Otevře se seznam **Zaznamenaná provozní přerušení**.
2. Pomocí **Hledat příčiny na této straně** zúžíte záznamy na této stránce.
3. Pro nový záznam stiskněte **Nový koncept odstávky**.
4. U každého pole nejprve zvolte stav hodnoty: **Chybí**, **Neznámé** nebo **Deklarovaná hodnota**. Hodnotu zadáváte pouze tehdy, když je vybráno „Deklarovaná hodnota“.
5. Do polí „Začátek přerušení“ a „Konec přerušení“ napište úplný časový údaj se sekundami a výslovným posunem vůči UTC.
6. Stiskněte **Uložit neposouzený koncept**. Neúplný koncept lze uložit.
7. V části **Kontrola a uzavření odstávky** v případě potřeby propojte doklady k nároku pomocí **Vyberte důkaz** a pak stiskněte **Odeslat ke kontrole**.
8. Pokud jste jmenovaný posuzovatel, zvolte své „Jmenování“ a použijte **Ověřit**, **Vrátit k opravě** nebo **Uzavřít**.
9. Dřívější revize zkontrolujte v části **Neměnná historie konceptů**.

## Co znamenají stavy
- **Koncept / Vyžaduje kontrolu:** zaznamenaný obsah je neposouzený. Důvody jsou uvedeny v části „Proč tento koncept zůstává neposouzený“.
- **Neodesláno / Otevřeno / V kontrole:** stav životního cyklu záznamu.
- **Ověřeno (člověkem):** ověřila ho jmenovaná osoba odlišná od osoby, která ho zaznamenala. Dokončeno není Ověřeno (13.03 §1).
- **Uzavřeno:** odstávka byla uzavřena. Ověřené uzavření není bezpečnostním uvolněním.
- **Neznámé:** hodnota nebyla deklarována nebo ji nebylo možné určit. Neznámé neznamená „Ne“ ani „Nepoužije se“. Z tohoto důvodu loss of hire a uzavření zůstávají „Neznámé“.
- **Kandidát Loss of Hire:** pouze kandidát; neuvádí žádný nárok ani částku.
- **Zaznamenaný interval:** neposouzené technické pozorování; nenahrazuje deklarované minuty.

## Co nemůžete dělat / upozornění
- Kdo odstávku zaznamenal, nemůže ji ověřit ani uzavřít; musí to udělat jiná jmenovaná osoba.
- Odeslání, ověření a uzavření nejsou možné, dokud chybějí povinné údaje.
- Propojení dokladů k nároku není ověřením.
- Z vlastního zdroje (závady, pracovního příkazu) se nekopíruje žádný popis, čas ani rozhodnutí o uzavření; uchovává se pouze odkaz na přesnou revizi.
- V režimu offline nelze aktuální zdroje číst a neexistuje žádné offline úložiště; vaše zadání zůstávají pouze v otevřené stránce.

## Když se něco pokazí
- **Výsledek uložení je neznámý:** váš původní příkaz je chráněn. Se stejným účtem použijte „Zopakovat přesný příkaz odstávky“.
- **Uložený koncept se změnil:** záznam změnil někdo jiný. Vaše zadání zůstanou zachována; pro opětovné načtení použijte „Načíst aktuální koncept“, nebo změny zahoďte.
- **Záznamy odstávek nebo aktuální přístup nejsou dostupné:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Zdroj se změnil:** pro nový koncept použijte aktuální revizi vlastního zdroje.
- **Příkaz nebyl přijat:** nahlaste to podpoře se snímkem obrazovky, časem a postupem.

## Související stránky
