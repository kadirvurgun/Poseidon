---
key: notification-escalation
title: Oznámení
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Stránka oznámení uvádí **výjimky z nakonfigurovaných zdrojů v aplikaci** (údržba k termínu, údržba vyžaduje posouzení, zdroj údržby se změnil nebo není dostupný) pro vybrané plavidlo nebo remorkér. Odpovídá na otázku: „Která oznámení o údržbě dorazila k mé roli, jak jsou závažná, která byla eskalována, protože je nikdo včas nepotvrdil, a na kterém zdrojovém záznamu jsou založena?“ Potvrzení přijetí neověřuje doklady ani neschvaluje provoz.

## Kdo ji používá
- **Vrchní strojník / Technický inspektor:** sledují termíny údržby a žádosti o posouzení a přecházejí ke zdrojovému plánu (13.03 §5).
- **Kapitán remorkéru / Kapitán / Posádka:** vidí oznámení směrovaná na jejich role a potvrzují jejich přijetí (13.03 §6).

## Než začnete
- Potřebujete přístup ke čtení oznámení, údržby a vybavení. Potvrzování a ukládání nastavení vyžadují samostatná oprávnění; bez nich se příslušná tlačítka nezobrazují.
- V poli **Plavidlo oznámení** vyberte oprávněné plavidlo nebo remorkér.
- V režimu offline nelze odeslat žádný příkaz potvrzení ani nastavení.

## Krok za krokem
1. V poli **Plavidlo oznámení** vyberte plavidlo.
2. Přečtěte si řádek pravidel: „Nastaveno · neověřená pravidla zdroje · doručování pouze v aplikaci“.
3. U každého oznámení si přečtěte název, odznak závažnosti (**Informace**, **Varování**, **Kritické**) a odznak stavu; zkontrolujte **Zdroj / revize**, **Termín splatnosti** a **Vytvořeno**. **Pravidlo směrování** ukazuje kód pravidla.
4. Pomocí **Otevřít zdrojový plán** přejdete k souvisejícímu plánu údržby.
5. Chcete-li zaznamenat, že jste oznámení viděli, stiskněte **Potvrdit** (pouze ve stavu **Otevřeno** nebo **Eskalováno**).
6. Pokud máte přístup k nastavení, upravte **Ztlumit nepovinná doporučující oznámení** a **Tiché hodiny** (začátek, konec, časové pásmo IANA) a stiskněte **Uložit nastavení doporučení**.
7. Pro další stránky použijte **Předchozí oznámení** / **Další oznámení** a pro opětovné načtení **Znovu načíst doručená oznámení**.

## Co znamenají stavy
- **Otevřeno:** oznámení čeká na potvrzení.
- **Potvrzeno přijetí:** osoba zaznamenala, že ho viděla. To neznamená, že údržba byla provedena nebo doklady ověřeny.
- **Eskalováno:** oznámení nebylo včas potvrzeno a bylo směrováno na eskalační roli svého pravidla.
- **Zdroj se změnil / Zdroj není dostupný:** podkladový záznam se změnil nebo ho nelze potvrdit. Zaznamenaný podklad se uchovává; oznámení se tím neruší ani znovu neaktivuje.
- **Povinná cesta · nelze ztlumit:** nastavení toto oznámení nemohou ztlumit.
- **Neznámé (Termín splatnosti):** zdroj nemá termín. To neznamená, že nic není k termínu.

## Co nemůžete dělat / upozornění
- Potvrzení přijetí neověřuje doklady ani neschvaluje provoz.
- Povinné a kritické cesty nelze ztlumit doporučujícími nastaveními. Tiché hodiny pouze odkládají volitelná upozornění.
- Prázdná stránka neznamená, že je vše v pořádku. Pokud pravidla nebo zdroje nejsou dostupné, stránka to uvádí zvlášť.
- Neexistuje žádné automatické opakování; nejistý příkaz se uchovává v této kartě a změny kontextu zůstávají uzamčeny.

## Když se něco pokazí
- **Zpráva „Výsledek neznámý“:** pomocí **Zopakovat přesný příkaz oznámení** znovu odešlete uchovaný příkaz.
- **Zpráva „Verze oznámení se změnila“:** pomocí **Znovu načíst doručená oznámení** načtěte aktuální seznam.
- **Zpráva „Zdroje oznámení nejsou dostupné nebo byl přístup odepřen.“:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Zpráva „Uložené tiché hodiny nebylo možné použít“:** zobrazují se všechna upozornění; zkontrolujte nastavení tichých hodin.

## Související stránky
