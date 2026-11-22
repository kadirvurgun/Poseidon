---
key: departure-signoff
title: Préparation au départ et validation du capitaine
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: [1, 2, 3, 4, 5, 6, 7, 9]
reviewed: false
---
## À quoi sert cette page
Cette page affiche une **évaluation consultative de la préparation** pour le navire sélectionné et la période de départ prévue, et enregistre la décision du capitaine désigné. Elle répond à la question : « Que disent les sources relatives à l’équipage, à la technique, à la réglementation, aux situations d’urgence et aux événements de sécurité pour cette période, et pourquoi ? » La décision d’autoriser, de suspendre ou d’arrêter le départ appartient au **Capitaine** (13.03 §2). Le système n’autorise jamais un départ ; une validation enregistrée n’est pas une autorisation de départ.

## Qui l’utilise
- **Capitaine :** pour examiner l’évaluation et la liste « Pourquoi », puis enregistrer Autoriser, Suspendre ou Refuser avec un motif (13.03 §2).
- **Shore Operations / Surintendant technique :** pour suivre en lecture seule l’évaluation et les décisions enregistrées du capitaine. Le personnel à terre ne peut pas enregistrer une décision à la place du capitaine.

## Avant de commencer
- Dans la barre de contexte supérieure, vérifiez que la **bonne société** est sélectionnée. La page ne liste que les navires de votre périmètre (13.03 §1).
- Vous avez besoin d’un accès en lecture à la préparation au départ pour voir la page.
- L’autorisation de décider ne suffit pas à elle seule pour enregistrer une décision : il vous faut aussi une **désignation de capitaine en cours** pour ce navire. Sans elle, la page indique « You hold no current Master appointment for this ship. Decisions are read-only. »

## Étape par étape
1. Choisissez le navire dans la liste « Choisir un navire ».
2. Dans **Période de départ prévue**, renseignez « Date de départ » et « Fin du voyage » et appuyez sur **Évaluer la préparation**.
3. Sur la ligne **Évaluation**, lisez le badge d’état, le mode « Actuel » ou « Prévision », « Évalué le » et « Valable jusqu’au ».
4. Dans le tableau des catégories, vérifiez les résultats Équipage, Technique, Réglementaire, Urgence et Événements de sécurité.
5. Lisez la liste **Pourquoi**. Chaque ligne présente une observation non satisfaite, son code de motif et sa source. Examinez une ligne inattendue sur la propre page de la source.
6. Si l’évaluation n’est pas actuelle et Prêt, lisez **Pourquoi Autoriser est indisponible**. Cette section indique quand la période est une prévision, liste les catégories qui ne sont pas encore satisfaites avec leurs résultats, et explique quel enregistrement comblerait les lacunes de source Urgence (aucune source d’exercices 61.02 n’est encore connectée) et Réglementaire (aucun ensemble de certificats requis n’est défini). Elle explique uniquement les catégories du serveur ; elle ne modifie pas le contrôle.
7. Sous **Décisions du capitaine**, consultez les décisions enregistrées précédemment et leur validité.
8. Si vous êtes le capitaine, dans **Enregistrer la décision du capitaine**, choisissez Autoriser, Suspendre ou Refuser, sélectionnez votre désignation dans « Désignation », rédigez un « Motif » (10 à 2000 caractères) et appuyez sur **Enregistrer**.
9. Utilisez **Actualiser** pour relire l’évaluation.

## Signification des états
- **Prêt :** aucune source enregistrée ne comporte d’observation non satisfaite. Il s’agit uniquement d’une information ; la décision de départ reste celle du capitaine.
- **Conditionnel :** au moins une source signale une condition. Autoriser n’est pas proposé pour cette évaluation.
- **Pas prêt :** au moins une source renvoie « Échec ». Le fait que d’autres catégories soient satisfaites ne compense pas cela.
- **Revue requise :** au moins une entrée est « Inconnu ». Une personne autorisée doit l’examiner.
- **Conforme / Condition / Non conforme / Inconnu :** le résultat de la catégorie dans le tableau des catégories. **Inconnu** signifie que la source n’a pas pu être lue ou ne contient pas de données ; cela ne signifie ni « Conforme » ni « Prêt ». Aucune source de préparation aux situations d’urgence n’est connectée pour la catégorie Urgence dans cette version ; cette ligne affiche donc Inconnu. Réglementaire peut rester Inconnu même lorsque chaque certificat enregistré est valide, car aucune source ne définit l’ensemble des certificats requis pour le navire. Réglementaire inclut également les signaux de confiance des données (DATA_TRUST_*) ; sans accès en lecture à la confiance des données, Réglementaire affiche Inconnu.
- **Prévision :** la période n’inclut pas aujourd’hui. Il s’agit uniquement d’une prévision de planification, qui ne peut pas être autorisée.
- **Current / Stale — inputs changed :** la validité d’une décision enregistrée. Lorsque les entrées changent, la décision antérieure devient obsolète et n’est pas supprimée.

## Ce que vous ne pouvez pas faire / prudence
- Le système n’autorise jamais un départ et n’affiche aucun état comme une autorisation de départ. Une décision enregistrée est l’enregistrement humain propre du capitaine désigné.
- Autoriser ne peut être enregistré que pour une évaluation READY actuelle ; chaque catégorie doit être satisfaite. Comme la catégorie Urgence reste Inconnu dans cette version, l’évaluation ne peut pas atteindre Prêt et Autoriser n’est pas proposé ; **Pourquoi Autoriser est indisponible** en indique la raison. Même à l’état Prêt, la décision d’autoriser appartient au capitaine ; le système n’approuve rien. « Suspendre et Refuser peuvent toujours être enregistrés avec un motif. »
- Un motif est requis pour chaque décision. Les décisions enregistrées ne sont pas modifiées ; une nouvelle décision constitue un nouvel enregistrement.
- Vous ne pouvez pas enregistrer de décision tant que l’évaluation est obsolète ou que vous êtes hors ligne ; actualisez d’abord.

## En cas de problème
- **Vous êtes hors ligne :** l’évaluation peut être obsolète et la validation est désactivée. Actualisez lorsque vous êtes de nouveau en ligne (13.03 §10).
- **Vous n’avez pas accès à la préparation au départ pour ce navire :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire.
- **Préparation inattendue :** ouvrez la liste « Pourquoi » ; si une source semble erronée, signalez-le au responsable du contrôle ou à l’expert métier.
- **Le résultat est incertain :** « Réessayer » envoie la même commande. N’écrivez pas de nouvelle décision.
- **L’historique des décisions a changé / L’évaluation a changé :** utilisez « Recharger », examinez l’évaluation actuelle, puis décidez.
- **La commande n’a pas été acceptée :** signalez-le au support avec une capture d’écran, l’heure et les étapes effectuées.

## Pages associées
