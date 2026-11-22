---
key: offline-conflict-center
title: Centrum podstatných konfliktov
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Centrum podstatných konfliktov uvádza pre vybrané plavidlo alebo remorkér **podstatné konflikty medzi vašimi offline zmenami a aktuálnym záznamom na serveri** a umožňuje osobe vyriešiť ich odôvodneným rozhodnutím. Odpovedá na otázku: „Ktoré z mojich offline zmien sa stretli s novším záznamom na serveri a ako, kým a s akým dôvodom sa to vyrieši?“ Nič sa tu neprepisuje ani nerieši automaticky.

## Kto ju používa
- **Posádka / Kapitán remorkéra / Kapitán / Hlavný strojník:** po práci offline vidia a riešia svoje vlastné konfliktné záznamy. Keď sa objaví konflikt, nepredpokladajte, že váš vlastný záznam automaticky vyhrá (13.03 §6).
- Podstatný konflikt vyžaduje ľudské riešenie; táto stránka je cieľom postupu „Podstatný konflikt → Centrum konfliktov“ v 13.03 §10.

## Skôr než začnete
- Stránka uvádza iba konflikty, ktoré ste zaznamenali vy, a vyžaduje prístup na čítanie aspoň jedného čitateľného zdroja (napríklad pridelenie posádky, operácie remorkérov, denný záznam, dôkaz pracovného príkazu (s čítaním vybavenia a údržby), koncept obnovenia osvedčenia). Stránku otvorí ktorýkoľvek jeden čitateľný zdroj; uvedené oprávnenia sú alternatívy.
- Zoznam a rozhodnutia vyžadujú aktuálne pripojenie k serveru. V režime offline zostávajú vaše lokálne zmeny uchované v zariadení.
- Vyberte plavidlo alebo remorkér zo zoznamu **Plavidlo**.

## Krok za krokom
1. Vyberte plavidlo v poli **Plavidlo**. Otvorí sa tabuľka so stĺpcami **Zdroj**, **Záznam**, **Verzie**, **Zachytené**, **Prijaté serverom** a **Stav**.
2. Ak sa zobrazí poznámka „Nezahrnuté:“, zdroje, na ktoré nemáte oprávnenie na čítanie, nie sú v zozname.
3. V riadku v stave **Otvorené — vyžaduje rozhodnutie** stlačte **Posúdiť**.
4. V časti **Posúdenie konfliktu** porovnajte pri každom poli hodnoty **Pred záznamom**, **Zachytené** a **Server**. Žiadna voľba nie je predvolená.
5. Pri každej hodnote výslovne vyberte **Ponechať hodnotu servera** alebo **Použiť zachytenú hodnotu**, vyplňte **Dôvod riešenia (povinný)** a stlačte **Odoslať riešenie**.
6. Ak chcete odstrániť iba lokálnu kópiu v tomto zariadení, zaškrtnite potvrdenie v časti **Zahodiť moju lokálnu zmenu** a stlačte **Zahodiť lokálnu zmenu**.
7. Pomocou **Zavrieť posúdenie** sa vrátite do zoznamu.

## Čo znamenajú stavy
- **Otvorené — vyžaduje rozhodnutie:** konflikt čaká na ľudské rozhodnutie.
- **Nahradené novším konfliktom:** pre ten istý záznam existuje novší konflikt; posúďte ten nový.
- **Vyriešené výslovným rozhodnutím:** niekto ho vyriešil odôvodneným rozhodnutím. Nie je to prevádzkové schválenie.
- **Nesprístupnené / Neposkytnuté / Neznáme:** hodnota je skrytá na základe oprávnení, nebola zaznamenaná alebo ju nebolo možné prečítať. Neznáma hodnota sa nikdy nepovažuje za prázdnu ani za „Nie“.
- **Server receipt recorded for your explicit decision:** server prijal vaše rozhodnutie. Toto potvrdenie o prijatí nie je prevádzkovým schválením.

## Čo nemôžete robiť / upozornenie
- Konflikty sa nikdy nezlučujú ani neriešia automaticky; každá hodnota vyžaduje výslovnú ľudskú voľbu a dôvod (13.03 §1).
- Centrum rieši konflikty pridelenia posádky priamo. Pri iných zdrojoch zobrazí „Resolve this conflict from the owning record's offline panel.“; napríklad záznamy remorkérov sa riešia v časti **Offline operácie remorkérov**.
- Zahodenie lokálnej zmeny odstráni iba kópiu v zariadení; záznam na serveri sa nezmení a konflikt zostáva zaznamenaný až do odôvodneného rozhodnutia.
- Zmena zachytená v inom zariadení sa musí vyriešiť v tom zariadení; nič sa nezahodí.
- Odoslanie znova overí váš prístup a verzie záznamov; neudeľuje prevádzkové schválenie.

## Ak sa niečo pokazí
- **Správa „The submission outcome is unknown“:** pred opätovným odoslaním použite **Skontrolovať výsledok odoslania**.
- **Správa „The server record changed after your review“ / „The server record changed again“:** vaše rozhodnutie nebolo uplatnené; posúďte konflikt znova.
- **„Material conflicts could not be loaded. Nothing was resolved.“:** stlačte **Skúsiť znova**.
- **„You do not have access to the conflict center.“ / „Conflict access is unavailable for this vessel.“:** skontrolujte svoju rolu a kontext plavidla; v prípade potreby sa obráťte na správcu nájomcu (13.03 §10).

## Súvisiace stránky
