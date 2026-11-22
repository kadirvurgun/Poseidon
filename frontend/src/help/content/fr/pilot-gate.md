---
key: pilot-gate
title: Revue go/no-go du pilote
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Revue go/no-go du pilote présente en un seul endroit l’état des jalons requis pour un pilote, le nombre de P0 ouverts et la préparation du support, et enregistre la décision go/no-go du Product Owner désigné. Elle répond à la question : « Chaque jalon du pilote a-t-il été atteint avec des preuves, et une décision humaine a-t-elle été enregistrée ? » L’avis en haut de page s’applique : « Revue consultative uniquement. Des jalons inconnus ou reportés et un P0 inconnu ou ouvert recommandent NO-GO. Seul le Product Owner désigné enregistre une décision. »

## Qui l’utilise
- **Product Owner :** lit le résultat de la revue et enregistre la décision go/no-go avec une justification. Décider nécessite une désignation Product Owner en cours.
- **Support / Administrateur du locataire :** marque les éléments de préparation du support et de l’astreinte avec une référence de preuve.
- 13.03 ne définit aucun rôle distinct pour cette page ; les principes d’administration du locataire (moindre privilège, publication contrôlée) s’appliquent (13.03 §9).

## Avant de commencer
- La consultation nécessite la lecture à l’échelle du locataire et la lecture des jalons du pilote. Marquer un élément de support nécessite la permission d’enregistrement du support ; décider nécessite la permission de décision plus une désignation Product Owner en cours provisionnée sur le serveur. La permission seule ne suffit pas.
- Seul un compte humain peut marquer le support ou décider ; la désignation doit correspondre à votre rôle actif et être dans sa période de validité.
- Les entrées des jalons proviennent d’autres pages ; par exemple, les jalons Ship UAT et Tug UAT sont calculés à partir des exécutions attestées sur la page **Enregistrements UAT par rôle**.
- L’enregistrement nécessite une connexion active ; il est désactivé hors ligne.

## Étape par étape
1. Dans **Revue du jalon pilote**, lisez la recommandation (**Recommandation consultative**), le nombre de **P0 ouverts** et le tableau des jalons : **Jalon**, **État**, **Source**, **Motifs**.
2. Dans **Préparation du support et de l’astreinte**, choisissez un **État** pour chaque élément, saisissez une **Référence de preuve** pour **Prêt**, rédigez une **Note** et appuyez sur **Marquer**.
3. Si vous êtes le Product Owner, choisissez la décision dans **Enregistrer la décision go/no-go** (**NO-GO**, **GO conditionnel**, **GO**), choisissez votre désignation Product Owner dans **Désignation** et rédigez la **Justification**. Pour **GO conditionnel**, remplissez **Conditions (une par ligne)**.
4. Appuyez sur **Enregistrer**. La décision apparaît dans **Décisions du Product Owner** avec son numéro de séquence.
5. Appuyez sur **Actualiser** pour recharger les données.

## Signification des états
- **Evidence threshold met (PASS) :** la source du jalon atteint le seuil de preuve. Ce n’est pas une approbation ; un humain décide.
- **Fail (FAIL)**, **Unknown (UNKNOWN)**, **Deferred (DEFERRED) :** le jalon n’est pas atteint. Un jalon inconnu ou reporté ne compte jamais comme réussi et recommande NO-GO.
- **NO-GO recommandé :** au moins un jalon n’est pas PASS, ou le nombre de P0 ouverts est inconnu ou supérieur à zéro.
- **Éligible au GO ; décision humaine en attente :** chaque jalon est PASS et le nombre de P0 ouverts est zéro. Le système ne décide jamais GO ; seul le Product Owner l’enregistre.
- **Not marked / Prêt / Not ready :** le dernier état marqué d’un élément de support.
- **Current / Stale: review changed :** une décision est liée à la revue au moment où elle a été prise. Si la revue a changé ensuite, la décision apparaît comme périmée et doit être reconsidérée.

## Ce que vous ne pouvez pas faire / prudence
- « GO n’est possible que lorsque chaque jalon est PASS et que le nombre de P0 ouverts est zéro. » Sinon, l’option GO est désactivée.
- Le GO conditionnel exige le même état que le GO (chaque jalon PASS, nombre de P0 ouverts connu et égal à zéro) et au moins une condition. Il ne peut pas être enregistré tant qu’un jalon est en échec, inconnu ou reporté, ni tant qu’un P0 est ouvert ou inconnu. NO-GO peut toujours être enregistré, sans conditions.
- Le système, l’IA ou l’automatisation ne prennent jamais la décision du pilote et ne fabriquent jamais de jalons ni de validations. Les jalons et décisions du pilote sont des enregistrements humains.
- Vous ne pouvez pas modifier une décision enregistrée ; une nouvelle décision est ajoutée avec un nouveau numéro de séquence.

## En cas de problème
- **Vous n’avez pas de désignation Product Owner en cours. Les décisions sont en lecture seule.** Les désignations sont provisionnées sur le serveur ; contactez l’administrateur du locataire (13.03 §10).
- **Vous n’avez pas accès à ces enregistrements.** Vérifiez votre rôle et le périmètre du locataire.
- **L’enregistrement a changé ou a déjà été utilisé. Rechargez.** La revue a changé pendant que vous décidiez ; ouvrez la revue actuelle avec **Recharger**. Ce message apparaît aussi lorsque les états des jalons ont changé et que la décision n’est plus autorisée.
- **Le résultat est incertain. Réessayer envoie la même commande.** Utilisez **Réessayer** pour renvoyer la même commande en toute sécurité.
- **Refusé par les règles.** Un jalon est reporté.
- **Non autorisé.** Votre désignation n’est pas en cours ou votre compte n’est pas un compte humain.
- **Le service est indisponible. Réessayez.** Si cela persiste, signalez au support l’identifiant de corrélation, l’heure et vos étapes (13.03 §10).

## Pages associées
