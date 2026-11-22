---
key: pms-command-center
title: Řídicí centrum PMS
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Řídicí centrum PMS shromažďuje **výjimky** údržby vybraného plavidla na jedné obrazovce. Odpovídá na otázku: „Má toto plavidlo kritické úkoly po termínu, otevřené pracovní příkazy, výstrahy závad nebo chybějící kritické náhradní díly, a proč?“ Stránka je poradním přehledem výjimek; nejde o rozhodnutí o způsobilosti, připravenosti ani bezpečnosti. Položky v pořádku se neuvádějí, pouze se počítají.

## Kdo ji používá
- **Vrchní strojník / Technický inspektor:** sledují úkoly po termínu, otevřené pracovní příkazy, výstrahy závad a chybějící kritické náhradní díly (13.03 §5).
- **Kapitán:** vidí dopad záznamů PMS a závad v podobě provozních omezení (13.03 §2).

## Než začnete
- K zobrazení stránky potřebujete oprávnění ke čtení vybavení (`equipment.read`).
- Každá část závisí na oprávnění ke čtení svého vlastního modulu. Například bez oprávnění ke čtení závad zůstává část **Výstrahy stavu** ve stavu „Vyžadován přístup: nezobrazeno“.
- Nejprve vyberte plavidlo. Bez plavidla stránka zobrazuje pouze seznam **Vyberte plavidlo.**

## Krok za krokem
1. Vyberte plavidlo ze seznamu. Pokud žádné není ve vašem rozsahu, stránka uvádí „Ve vašem rozsahu není žádné plavidlo.“
2. Nahoře si přečtěte odznak **Souhrn výjimek** a čas **Poslední obnovení**.
3. Zkontrolujte části: **Kritické úkoly po termínu**, **Splatné, zpožděné a ke kontrole**, **Otevřené pracovní příkazy**, **Výstrahy stavu** a **Chybějící kritické náhradní díly**.
4. U položky otevřete **Proč**, abyste viděli zaznamenané kódy důvodů a případně čas **Zjištěno**.
5. Pomocí **Otevřít zdrojový záznam** přejdete ke zdroji. Položka závady otevře [Závady a dočasná opatření](/app/defects), položka pracovního příkazu otevře [Centrum pracovních příkazů](/app/work-order-center).
6. Stisknutím **Obnovit** data přečtete znovu.

## Co znamenají stavy
- **Blokováno / Vyžaduje kontrolu / Pozor:** stav souhrnu výjimek. Podrobnosti jsou v položkách jednotlivých částí.
- **Žádné otevřené výjimky (poradní):** přečtené zdroje nevrátily žádnou otevřenou výjimku. Nejde o výsledek připravenosti ani způsobilosti.
- **Kritické / Pozor / Vyžaduje kontrolu:** závažnost položky.
- **Zdroj nedostupný: neznámé, ne nula:** zdroj části nebylo možné přečíst. Neznámé neznamená „nula“, „žádný problém“ ani „Připraveno“.
- **Vyžadován přístup: nezobrazeno:** pro tuto část nemáte oprávnění; nezobrazuje se obsah ani počet.
- **Z tohoto zdroje žádné výjimky:** část byla přečtena a nevrátila žádnou výjimku. Počet položek v pořádku se zobrazuje zvlášť.
- **Zastaralá data / Aktuálnost neznámá:** čtení za položkou není aktuální nebo jeho aktuálnost není známa.

## Co nemůžete dělat / upozornění
- Stránka nemění žádný záznam; pouze čte. Pracovní příkazy, závady a náhradní díly se zpracovávají na svých vlastních stránkách.
- Stránka nikdy neprohlašuje plavidlo za „připravené“ ani „bezpečné“. Rozhodnutí o vyplutí náleží kapitánovi (13.03 §2).
- Závada se počítá jako uzavřená pouze na základě lidského záznamu o uzavření, a to pouze pro revizi prohlášení, která byla uzavřena. Pokud se prohlášení později změní, položka se vrátí k posouzení.
- Data zobrazená v režimu offline mohou být stará a **Obnovit** není dostupné.

## Když se něco pokazí
- **Záznam nebylo možné načíst:** zkontrolujte připojení a použijte **Znovu načíst**.
- **Nemáte serverové oprávnění pro tuto akci nebo záznam:** zkontrolujte svou roli a kontext plavidla; v případě potřeby se obraťte na správce svého tenanta (13.03 §10).
- **Neočekávaná výjimka:** otevřete **Proč** a zdrojový záznam zkontrolujte s odpovědným vlastníkem nebo odborníkem (SME) (13.03 §10).
- **Chyba aplikace:** nahlaste podpoře Correlation ID, obrazovku, čas a postup (13.03 §10).

## Související stránky
