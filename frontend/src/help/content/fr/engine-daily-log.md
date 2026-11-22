---
key: engine-daily-log
title: Journal quotidien des machines
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le journal quotidien des machines conserve les **enregistrements de quart machine** du navire sélectionné sous forme de brouillons, avec la version de source enregistrée. Il répond à la question : « Qu’a déclaré le quart machine concernant l’état et les heures des moteurs, la consommation de combustible et d’huile de graissage, l’eau douce, les mesures critiques, les alarmes, les défauts, le soutage, les eaux de cale et les contrôles environnementaux, et le Chef mécanicien a-t-il enregistré un examen de cette révision exacte ? » Une entrée enregistrée est une déclaration non vérifiée. Les données de brouillon ne sont pas transformées en conclusions sur les émissions, l’assurance combustible ou la disponibilité.

## Qui l’utilise
- **Mécanicien de quart (Crew / OOW / Mécanicien) :** pour tenir l’enregistrement du quart machine en temps réel et dans l’ordre chronologique (13.03 §6).
- **Chef mécanicien :** pour examiner le journal enregistré, le lier aux ordres de travail et aux défauts et consigner l’examen comme un acte humain (13.03 §5). Seule une désignation portant la fonction de Chef mécanicien peut enregistrer l’examen d’un journal machine.

## Avant de commencer
- Dans la barre de contexte supérieure, sélectionnez la **bonne société et le bon navire**.
- La lecture requiert `daily.engine.read` ; l’enregistrement requiert `daily.engine.write` et des droits de modification sur ce navire. L’enregistrement d’un examen requiert `daily.engine.write` avec des droits de modification sur ce navire, `daily.engine.review`, ainsi qu’une désignation en cours de Chef mécanicien pour cet enregistrement exact.
- La liaison d’ordres de travail ou de défauts requiert également un accès en lecture à ces enregistrements.

## Étape par étape
1. Choisissez le navire. **Brouillons enregistrés** liste chaque journal par date et révision avec « Brouillon · examen requis ».
2. Appuyez sur **Nouveau brouillon**, ou ouvrez un journal listé pour **Modifier le brouillon enregistré**.
3. Renseignez **Contexte du quart machine**, **État et heures des moteurs**, **Consommation et production**, **Mesures, alarmes et défauts** et **Contexte des travaux et de l’environnement**. Pour chaque champ, choisissez **Connu**, **Inconnu** ou **Manquant**.
4. Appuyez sur **Enregistrer le brouillon**. Chaque enregistrement ajoute une révision à **Historique des révisions enregistrées**.
5. Sur un journal enregistré, utilisez **Ordres de travail et défauts** : choisissez un **Type d’enregistrement** et un **Enregistrement**, puis **Lier**. Un lien épingle une révision exacte ; **Retirer le lien** le supprime.
6. Si nécessaire, ouvrez **Ouvrir un relevé de combustible depuis la source enregistrée** ou **Ouvrir un relevé d’indisponibilité depuis la source enregistrée**. Les quantités et les unités ne sont pas copiées.
7. Le Chef mécanicien ouvre **Enregistrement d’examen humain**, choisit une **Désignation humaine habilitée** et une **Entrée d’examen**, rédige un **Motif de l’examen** et appuie sur **Enregistrer l’entrée d’examen**.

## Signification des états
- **Unverified declaration / Connu / Inconnu / Manquant :** les valeurs déclarées et leur état de données. Inconnu ne signifie ni « Non », ni « Prêt », ni « Sans objet ».
- **En attente · examen non enregistré :** aucun examen humain n’existe pour cette révision.
- **Métadonnées humaines enregistrées · non évaluées :** une entrée d’examen du Chef mécanicien existe pour cette révision. Elle n’établit ni vérification ni validation par le Chef mécanicien.
- **Current revision / Newer revision exists :** indique si un ordre de travail ou un défaut lié a changé depuis sa liaison. Un lien n’est qu’une filiation ; il ne vérifie, ne clôt ni n’approuve cet enregistrement.
- **recherche indisponible — INCONNU :** le type d’enregistrement lié ne peut pas être lu avec votre accès.

## Ce que vous ne pouvez pas faire / prudence
- Le système n’examine ni ne finalise jamais le journal. L’examen est un acte humain du Chef mécanicien désigné (13.03 §5).
- Quiconque a rédigé une révision quelconque du journal ne peut pas l’examiner. Aucune désignation n’est proposée à un auteur et la page indique « You authored this log revision; an independent reviewer must record the review. » ; le serveur refuse également un tel examen au titre de la séparation des tâches.
- Tenez les entrées en temps réel et dans l’ordre chronologique ; ne modifiez pas l’enregistrement examiné de quelqu’un d’autre (13.03 §6).
- Lier un défaut ici n’enregistre pas de restriction et ne le clôt pas ; faites-le sur la page du défaut ou de l’ordre de travail (13.03 §5).
- Les liens requièrent une connexion en ligne active ; rien n’est lié hors ligne. Les conflits hors ligne ne sont jamais fusionnés automatiquement.

## En cas de problème
- **« The log entry changed. Reload it before changing links. » :** rechargez, puis liez de nouveau.
- **« Save outcome unknown » :** utilisez **Réessayer la commande enregistrée exacte** ; ne ressaisissez rien.
- **« Human conflict resolution required » :** ouvrez **Examiner le conflit** ou le centre des conflits et choisissez explicitement (13.03 §10).
- **Aucune désignation disponible ou accès refusé :** vérifiez votre rôle et votre contexte de navire avec votre administrateur de locataire (13.03 §10).

## Pages associées
