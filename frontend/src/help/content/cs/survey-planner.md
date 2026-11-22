---
key: survey-planner
title: Plánovač prohlídek
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## K čemu tato stránka slouží
Plánovač prohlídek zobrazuje deklarovaná okna prohlídek v kalendáři flotily spolu s vhodností plavby a přístavu. Odpovídá na otázku: „Které certifikáty na kterých plavidlech mají ve zvoleném rozsahu otevírající se nebo uplynulé okno prohlídky a existuje kontrolní seznam přípravy?“ Je to pouze plánovací pomůcka: není to harmonogram úřadu, rezervace ani výsledek prohlídky (13.03 §8).

## Kdo ji používá
- **Kapitán, Vrchní strojník / Technical Superintendent:** vidí nadcházející prohlídky a včas zahajují kontrolní seznam přípravy (13.03 §5).
- **Uživatel Regulatory / Assurance:** sleduje deklarovaná okna a chybějící deklarace v celé flotile (13.03 §8).

## Než začnete
- Zobrazení vyžaduje čtení certifikátů. Zobrazují se pouze plavidla, ke kterým máte přístup.
- Okna pocházejí z deklarací v záznamu certifikátu. Pokud není deklarováno žádné okno, řádek se zobrazí v části **Okno nedeklarováno**; zadejte data ze zdrojového dokumentu v [Certificate command center](/app/certificate-command-center).
- Stránka vyžaduje živé čtení. Offline uvidíte „Offline: je vyžadováno aktuální online čtení.“

## Krok za krokem
1. V **Rozsah kalendáře** zvolte **Od** a **Do** a stiskněte **Zobrazit**.
2. Přečtěte si počty otevřených, nadcházejících, opožděných a neznámých položek v souhrnném řádku.
3. U každého řádku zkontrolujte **Certifikát**, **Vydavatel**, **Deklarovaná vlajka**, **Právní základ**, **Deklarovaná platnost do**, **Okno prohlídky** a **Vhodnost plavby/přístavu**.
4. Stiskněte **Otevřít certifikát**, abyste viděli certifikát.
5. Pokud seznam přípravy existuje, použijte **Otevřít kontrolní seznam**, pokud ne, použijte **Zahájit kontrolní seznam**, a přejděte tak na [Pre-survey checklist](/app/pre-survey-checklist).

## Co znamenají stavy
- **Nadcházející / Okno otevřené:** poloha vůči deklarovanému oknu.
- **Okno uplynulo, splnění nezaznamenáno:** okno skončilo a žádný záznam neukazuje, že prohlídka byla provedena. Neprokazuje to ani, že prohlídka byla zmeškána; zkontrolujte záznamy.
- **Okno neznámé:** datum chybí nebo je nečitelné. Neznámé okno nikdy neznamená „žádný problém“; vyžaduje posouzení.
- Vhodnost plavby/přístavu: **Podmíněné**, **Nevhodné**, **Neznámé**. Stránka nikdy nedává schválení „vhodné“.
- Připravenost kontrolního seznamu: **Blokováno**, **Neúplné**, **Připraveno, čeká na lidské potvrzení**.

## Co nemůžete dělat / upozornění
- Plánovač nic nerezervuje a nic nepodává u úřadu. Koordinace s klasifikační společností, vlajkou a úřady je externí a není zde simulována.
- To, co kalendář zobrazuje, není výsledek prohlídky ani platnost certifikátu. Rozhodnutí o plavbě a prohlídce náleží oprávněným osobám.
- Stránka je pouze pro čtení; změny se provádějí na stránkách certifikátu a kontrolního seznamu.

## Když se něco pokazí
- **Zobrazena je jen část flotily; zužte rozsah.** Zvolte kratší rozsah kalendáře.
- **V tomto rozsahu nejsou certifikáty s okny prohlídek.** Rozšiřte rozsah nebo zkontrolujte deklarace certifikátů.
- **Načtení se nezdařilo. Zkuste to znovu.** Stiskněte **Zkusit znovu**. Pokud problém přetrvává, nahlaste jej podpoře s obrazovkou, časem a kroky (13.03 §10).
- **K tomuto zobrazení nemáte přístup.** Zkontrolujte svou roli a rozsah plavidel; v případě potřeby kontaktujte Tenant Admin (13.03 §10).

## Související stránky
