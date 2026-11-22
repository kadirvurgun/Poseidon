---
key: port-activity-sof
title: Prístavná činnosť a SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## Na čo slúži táto stránka
Stránka prístavnej činnosti a SOF uchováva **prístavné udalosti** vybranej lode ako prevádzkový záznam s referenciami na dôkazy a umožňuje vymenovanej osobe interne finalizovať **Statement of Facts (SOF)**. Odpovedá na otázku: „Ktoré prístavné udalosti sme deklarovali, v akom poradí a intervale UTC, s akými referenciami na náklad, ponor, lodivoda, remorkér, oneskorenie a protest, a finalizovala vymenovaná osoba túto revíziu SOF?“ Stránka nevytvára žiadny podpis, laytime ani demurrage a zaznamenané udalosti nie sú pokyny ani schválené plánované okná.

## Kto ju používa
- **Kapitán:** vedie príchod a prístavnú činnosť ako prevádzkový záznam s dôkazmi a finalizuje SOF (13.03 §2).
- **Prvý dôstojník / Hlavný strojník:** zaznamenávajú prístavné udalosti a s platným vymenovaním finalizujú SOF.

## Skôr než začnete
- V hornom kontextovom paneli vyberte **správnu spoločnosť a loď**. Ak nie je vybraná žiadna loď, stránka zobrazí „Vybrať loď“.
- Čítanie vyžaduje `operations.port.read`; ukladanie konceptov vyžaduje `operations.port.write` a práva na úpravy na tejto lodi.
- Finalizácia vyžaduje `operations.port.write`, práva na úpravy na tejto lodi a `operations.port.finalize` a navyše platné vymenovanie na finalizáciu (kapitán, prvý dôstojník alebo hlavný strojník) pre túto loď.

## Krok za krokom
1. Vyberte loď. **Zaznamenaná časová os prístavných udalostí** uvádza vrátené udalosti v poradí UTC servera. **Filtrovať vrátenú stranu** zúži iba túto stranu.
2. Pod zoznamom si prečítajte **Časová os Statement of Facts**: každú udalosť so **Zaznamenaný interval**, **Dôvod oneskorenia** a **Poznámky**.
3. Stlačte **Nový koncept záznamu**. Vyplňte **Kontext návštevy prístavu**, **Zaznamenaná činnosť a načasovanie**, **Vyhlásenia nákladu a ponoru** a **Podpora, výnimky a dôkazy**. Časy udalostí vyžadujú Z alebo výslovný posun UTC.
4. Stlačte **Uložiť neoverený koncept**. Každé uloženie pridá revíziu do **Zaznamenaná história revízií**.
5. Na uloženom zázname otvorte **Finalizácia Statement of Facts**. Ak sa zobrazí **Required facts missing**, najprv doplňte tieto polia.
6. Zaškrtnite každú uvedenú nevyriešenú referenciu, čím ju beriete na vedomie, vyberte svoje **Vymenovanie** a stlačte **Finalizovať SOF**. Systém zaznamená, kto finalizoval, kedy, vo funkcii a s akým dôvodom.

## Čo znamenajú stavy
- **KONCEPT:** SOF nie je finalizovaný.
- **FINÁLNY (interný):** vymenovaná osoba finalizovala túto revíziu. Je interný: nie je to podpis ani externá dohoda.
- **Podpis nezaznamenaný:** zobrazuje sa vždy; na tejto stránke žiadny podpis neexistuje.
- **Zmenené po FINÁLNOM: je potrebná nová kontrola a finalizácia.:** záznam bol po finalizácii znova uložený a vrátil sa do stavu KONCEPT.
- **UNKNOWN (unresolved references):** pre tieto povinné referencie neexistuje žiadne vlastné vyhľadanie. Ich vzatie na vedomie z nich nerobí známe.
- **Vyžaduje posúdenie / Neoverený koncept záznamu:** prístavné udalosti sú vyhlásenia; nič nie je overené.
- **Koncept (nefinalizované) / Konečný (interný; nejde o podpis):** stav SOF, ktorý server pre túto revíziu zaznamenal, zobrazený v súhrne záznamu. Konečný vzniká iba zo zaznamenanej ľudskej finalizácie.

## Čo nemôžete robiť / upozornenie
- Systém SOF nikdy nefinalizuje. Finalizácia je ľudský úkon vymenovaného kapitána, prvého dôstojníka alebo hlavného strojníka (13.03 §2).
- **Finalizovať SOF** zostáva vypnuté, kým chýbajú povinné skutočnosti, neexistuje vymenovanie alebo nevyriešené referencie nie sú vzaté na vedomie.
- Finalizácia nie je podpis, nie je externá dohoda a nie je laytime ani demurrage.
- Päta stránky uvádza: „SOF finalization is an internal human act recorded in the finalization panel by an appointed Master, Chief Officer or Chief Engineer. Signatures, operational/legal approval and offline capture remain unavailable.“ (Finalizácia SOF je interný ľudský úkon, ktorý v paneli finalizácie zaznamenáva vymenovaný kapitán, prvý dôstojník alebo hlavný strojník. Podpisy, prevádzkové/právne schválenie a offline zaznamenávanie zostávajú nedostupné.)
- Ukladanie konceptov a finalizácia vyžadujú aktuálnu online reláciu; táto stránka nemá offline zaznamenávanie.

## Ak sa niečo pokazí
- **„No current finalization appointment for this vessel.“:** skontrolujte svoju rolu a kontext lode so správcom nájomcu (13.03 §10).
- **„Outcome unknown: retry sends the same command once.“:** použite **Skúsiť znova**; nezačínajte novú finalizáciu.
- **„Command not accepted“:** prečítajte si zobrazený kód, znova načítajte záznam pomocou **Načítať aktuálny uložený záznam** a skúste to znova.
- **„The saved source changed.“:** pred ďalším uložením výslovne znova načítajte alebo zahoďte zmeny.

## Súvisiace stránky
