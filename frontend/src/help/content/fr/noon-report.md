---
key: noon-report
title: Rapport de midi
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page du rapport de midi conserve les **déclarations quotidiennes de midi** du navire sélectionné sous forme de brouillons, avec la version de source enregistrée. Elle répond à la question : « Qu’avons-nous déclaré concernant l’heure du rapport, le voyage, la position, la distance, la météo, la cargaison, les heures des machines, le combustible et le ROB, et le capitaine a-t-il enregistré un examen de cette révision exacte ? » Un brouillon enregistré est une déclaration non vérifiée. Son enregistrement n’établit pas d’approbation du capitaine ou du Chef mécanicien, et la page n’en tire aucune conclusion sur les émissions, l’assurance combustible ou la disponibilité.

## Qui l’utilise
- **Officier préparant le rapport (Crew / OOW) :** pour renseigner et enregistrer le brouillon de midi pour le bon navire et la bonne date (13.03 §6).
- **Capitaine :** pour examiner le rapport enregistré et consigner cet examen comme un acte humain (13.03 §2). Seule une désignation portant la fonction de capitaine peut enregistrer l’examen d’un rapport de midi.

## Avant de commencer
- Dans la barre de contexte supérieure, sélectionnez la **bonne société et le bon navire**. Si aucun navire n’est sélectionné, la page affiche « Sélectionner un navire ».
- La lecture requiert `daily.noon.read`. La création et l’enregistrement de brouillons requièrent `daily.noon.write` et des droits de modification sur ce navire ; sans eux, vous voyez « Accès en lecture seule. La modification et l’enregistrement ne sont pas autorisés. »
- L’enregistrement d’un examen requiert `daily.noon.write` avec des droits de modification sur ce navire, `daily.noon.review`, ainsi qu’une désignation de capitaine en cours pour cet enregistrement exact.

## Étape par étape
1. Choisissez le navire. La liste **Brouillons enregistrés** affiche chaque rapport par date et révision avec le badge « Brouillon · examen requis ». Utilisez **Brouillons précédents** / **Autres brouillons** pour naviguer entre les pages.
2. Appuyez sur **Nouveau brouillon** pour ouvrir **Nouveau brouillon quotidien**, ou ouvrez un rapport listé pour **Modifier le brouillon enregistré**.
3. Renseignez les groupes (**Rapport et voyage**, **Position et activité**, **Météo et cargaison**, **Machines, combustible et ROB**). Pour chaque champ, choisissez un **État de la donnée** : **Connu**, **Inconnu** ou **Manquant**. Les brouillons incomplets peuvent être enregistrés.
4. Appuyez sur **Enregistrer le brouillon**. La page confirme « Draft saved. Human review and operational approval remain separate. » Chaque enregistrement ajoute une révision sous **Historique des révisions enregistrées**.
5. Sur un rapport enregistré, le capitaine ouvre **Enregistrement d’examen humain**, choisit une **Désignation humaine habilitée**, une **Entrée d’examen** (« Enregistrer la déclaration examinée (non évaluée) » ou « Renvoyer pour correction ») et rédige un **Motif de l’examen**, puis appuie sur **Enregistrer l’entrée d’examen**.
6. Si du combustible ou un événement portuaire doit être enregistré, utilisez **Utiliser la source opérationnelle enregistrée** et **Ouvrir un relevé de combustible depuis la source enregistrée** ou **Ouvrir un événement portuaire depuis la source enregistrée**. Les quantités, les unités et les approbations ne sont pas copiées.

## Signification des états
- **Draft · declarations unverified / Unverified declaration :** les valeurs sont déclarées par une personne ; personne ne les a vérifiées.
- **Connu / Inconnu / Manquant :** l’état des données d’un champ. Inconnu ne signifie ni « Non », ni « Prêt », ni « Sans objet », et Manquant n’est pas zéro.
- **En attente · examen non enregistré :** aucun examen humain n’a été enregistré pour cette révision.
- **Human metadata recorded · unassessed / Recorded · unassessed :** une entrée d’examen du capitaine existe pour la révision affichée. L’enregistrement de l’examen n’établit ni vérification, ni soumission, ni approbation de sécurité par le capitaine.
- **Historical revision :** l’entrée d’examen appartient à une révision antérieure et n’examine pas le brouillon actuellement enregistré.

## Ce que vous ne pouvez pas faire / prudence
- Le système n’examine ni ne finalise jamais le rapport. L’enregistrement d’un examen est un acte humain du capitaine désigné (13.03 §2).
- Quiconque a rédigé une révision quelconque du journal ne peut pas l’examiner. Aucune désignation n’est proposée à un auteur et la page indique « You authored this log revision; an independent reviewer must record the review. » ; le serveur refuse également un tel examen au titre de la séparation des tâches. Si le capitaine a rédigé le rapport, un examinateur indépendant disposant de l’autorité d’examen doit tout de même enregistrer l’examen.
- Un nouvel enregistrement crée une nouvelle révision ; l’examen antérieur reste attaché à sa propre révision. Examinez de nouveau après des modifications.
- Une référence saisie en texte libre ne peut pas remplacer une recherche autorisée ; laissez le champ à Manquant ou Inconnu.
- Hors ligne, seuls les champs explicitement classés peuvent aller dans la file d’attente chiffrée. Les champs d’examen, d’approbation et de vérification en sont exclus.

## En cas de problème
- **« The record changed. Your inputs are retained. » :** quelqu’un a enregistré une version plus récente. Abandonnez explicitement et rechargez avant une nouvelle tentative.
- **« Save outcome unknown » :** les champs sont figés ; utilisez **Réessayer la commande enregistrée exacte**, ne ressaisissez rien.
- **« No current source-authorized human appointment is available for this record. » :** vérifiez votre rôle et votre contexte de navire avec votre administrateur de locataire (13.03 §10).
- **« Human conflict resolution required » :** une saisie hors ligne est en conflit avec le serveur. Ouvrez **Examiner le conflit** ou le centre des conflits et choisissez explicitement ; rien n’est fusionné automatiquement (13.03 §10).
- **Sources quotidiennes indisponibles ou accès refusé :** utilisez **Réessayer la lecture des sources** ; si le problème persiste, signalez l’heure et les étapes effectuées au support.

## Pages associées
