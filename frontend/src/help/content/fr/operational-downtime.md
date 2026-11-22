---
key: operational-downtime
title: Indisponibilité opérationnelle
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Cette page enregistre les **interruptions opérationnelles déclarées** (indisponibilités) d’un navire avec la traçabilité de leur source. Elle répond à la question : « Quelle interruption a été déclarée sur ce navire, quand, pour quelle cause et liée à quel défaut ou ordre de travail, et où en sont sa revue et sa clôture ? » La page ne décide pas du droit à une indemnité de loss of hire, de l’aptitude au service ni d’aucune levée de sécurité.

## Qui l’utilise
- **Chef mécanicien / Surintendant technique :** pour enregistrer une indisponibilité causée par une panne technique ou une maintenance et la lier à l’historique du défaut ou de l’ordre de travail (13.03 §5).
- **Capitaine :** pour suivre l’impact opérationnel de l’indisponibilité et la revoir lorsqu’il est désigné (13.03 §2).
- **Réviseur désigné (Capitaine, Second capitaine, Chef mécanicien, Responsable de flotte, DPA) :** pour vérifier, renvoyer pour correction ou clôturer un enregistrement d’indisponibilité.

## Avant de commencer
- Dans la barre de contexte supérieure, vérifiez que la **bonne société** est sélectionnée ; la page ne liste que les navires de votre périmètre.
- La lecture nécessite un accès en lecture aux indisponibilités ; l’ouverture d’un brouillon nécessite un accès en écriture. Les actions de revue nécessitent en plus un accès de revue et une désignation de service en cours pour ce navire.
- Pour ouvrir une indisponibilité liée, vous pouvez utiliser **Ouvrir le brouillon d’indisponibilité lié** sur une page de défaut ou d’ordre de travail.

## Étape par étape
1. Choisissez le navire dans la liste « Sélectionner un navire ». La liste **Interruptions opérationnelles enregistrées** s’ouvre.
2. Utilisez **Rechercher les causes sur cette page** pour restreindre les enregistrements de cette page.
3. Pour un nouvel enregistrement, appuyez sur **Nouveau brouillon d’indisponibilité**.
4. Pour chaque champ, choisissez d’abord l’état de sa valeur : **Manquant**, **Inconnu** ou **Valeur déclarée**. Vous ne saisissez une valeur que lorsque « Valeur déclarée » est sélectionné.
5. Dans « Début de l’interruption » et « Fin de l’interruption », saisissez un horodatage complet avec les secondes et un décalage UTC explicite.
6. Appuyez sur **Enregistrer le brouillon non évalué**. Un brouillon incomplet peut être enregistré.
7. Dans **Revue et clôture de l’indisponibilité**, liez si nécessaire des preuves de réclamation avec **Choisir une preuve**, puis appuyez sur **Soumettre à la revue**.
8. Si vous êtes un réviseur désigné, choisissez votre « Désignation » et utilisez **Vérifier**, **Renvoyer pour correction** ou **Clôturer**.
9. Consultez les révisions antérieures sous **Historique immuable des brouillons**.

## Signification des états
- **Brouillon / Revue requise :** le contenu enregistré n’est pas évalué. Les raisons figurent sous « Pourquoi ce brouillon reste non évalué ».
- **Non soumis / Ouvert / En revue :** l’état du cycle de vie de l’enregistrement.
- **Vérifié (humain) :** une personne désignée, autre que celle qui a enregistré, l’a vérifié. Terminé n’est pas Vérifié (13.03 §1).
- **Clôturé :** l’indisponibilité a été clôturée. Une clôture vérifiée n’est pas une levée de sécurité.
- **Inconnu :** la valeur n’a pas été déclarée ou n’a pas pu être déterminée. Inconnu ne signifie ni « Non » ni « Sans objet ». C’est pourquoi le loss of hire et la clôture restent « Inconnu ».
- **Candidat Loss of Hire :** un candidat uniquement ; il n’indique aucun droit ni aucun montant.
- **Intervalle enregistré :** une observation technique non évaluée ; elle ne remplace pas les minutes déclarées.

## Ce que vous ne pouvez pas faire / prudence
- La personne qui a enregistré l’indisponibilité ne peut pas la vérifier ni la clôturer ; une autre personne désignée doit le faire.
- Soumettre, vérifier et clôturer ne sont pas possibles tant que des faits requis manquent.
- Lier une preuve de réclamation n’est pas une vérification.
- Aucune description, heure ni décision de clôture n’est copiée depuis la source propriétaire (défaut, ordre de travail) ; seul le lien exact vers la révision est conservé.
- Hors ligne, les sources actuelles ne peuvent pas être lues et il n’existe pas de stockage hors ligne ; vos saisies restent uniquement dans la page ouverte.

## En cas de problème
- **Le résultat de l’enregistrement est inconnu :** votre commande d’origine est protégée. Avec le même compte, utilisez « Réessayer la commande d’indisponibilité exacte ».
- **Le brouillon enregistré a changé :** quelqu’un d’autre a modifié l’enregistrement. Vos saisies sont conservées ; utilisez « Lire le brouillon actuel » pour recharger, ou abandonnez.
- **Les enregistrements d’indisponibilité ou l’accès actuel ne sont pas disponibles :** vérifiez votre rôle et le contexte du navire ; contactez si nécessaire l’administrateur de votre locataire (13.03 §10).
- **La source a changé :** utilisez la révision actuelle de la source propriétaire pour un nouveau brouillon.
- **Commande non acceptée :** signalez-le au support avec une capture d’écran, l’heure et vos étapes.

## Pages associées
