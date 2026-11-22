---
key: tug-assignment
title: Affectation des remorqueurs
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Cette page affiche les **propositions d’affectation de remorqueurs** pour une mission de remorqueur soumise et leurs **conflits** avec d’autres missions. Elle répond à la question : « Quel remorqueur a été proposé pour cette mission, avec quel rôle et quelle plage horaire, entre-t-il en conflit avec d’autres travaux, et le Capitaine de remorqueur a-t-il accepté la proposition ? » La terre propose, le Capitaine de remorqueur accuse réception ; la page ne prend aucune décision d’envoi ou de libération. L’avis en haut de page le rappelle : « Le système n’émet jamais de GO. »

## Qui l’utilise
- **Coordinateur des opérations de remorquage / Opérations à terre :** pour faire une proposition d’affectation après avoir examiné les conflits et la préparation sur le tableau des missions (13.03 §7).
- **Capitaine de remorqueur :** pour confirmer dans son contexte une proposition concernant son propre remorqueur et l’accepter ou la refuser (13.03 §3).

## Avant de commencer
- Ouvrez le remorqueur depuis **Opérations remorqueur** ; si aucun remorqueur n’est sélectionné, la page vous y renvoie.
- Proposer exige un accès en écriture remorqueur et en écriture plan ; la mission doit être **Soumise**.
- Répondre exige une **Désignation** (Capitaine de remorqueur ou Capitaine) enregistrée sur le serveur pour ce remorqueur. La personne qui a fait une proposition ne peut pas y répondre.

## Étape par étape
1. Ouvrez le numéro de mission depuis **Sélectionnez un travail de remorquage soumis**. Le lien **Affectations et préparation** dans le détail de la mission ouvre aussi cette page.
2. Pour les propositions existantes, lisez **État**, **Accusé du capitaine de remorqueur**, **Rôle**, **Début prévu** et **Fin prévue**.
3. Dans **Conflits**, consultez les conflits d’horaire et de ressources avec d’autres missions, leur badge de résultat et leur code de motif.
4. Pour une nouvelle proposition, choisissez **Remorqueur**, **Début prévu**, **Fin prévue** et **Rôle** (Principal, Soutien, Escorte) dans le formulaire **Proposer une affectation** et appuyez sur **Proposer une affectation**.
5. En tant que Capitaine de remorqueur, choisissez votre **Désignation**, ajoutez un **Motif** si nécessaire et appuyez sur **Accepter** ou **Rejeter**. Un motif est obligatoire pour rejeter.
6. Pour retirer une proposition, saisissez un motif et appuyez sur **Retirer la proposition**.
7. Utilisez les liens **Contrôle de préparation opérationnelle** et **Équipage et compétence de remorquage** sous chaque proposition pour passer aux écrans de préparation.

## Signification des états
- **Proposé / Retiré :** le cycle de vie de la proposition. Proposé ne signifie pas que le remorqueur a été envoyé.
- **En attente / Accepté / Refusé :** la réponse du Capitaine de remorqueur à la proposition. Accepté confirme le périmètre de la proposition ; ce n’est pas une décision GO.
- **Conforme / Non conforme / Inconnu :** le résultat d’un contrôle de conflit. Inconnu indique qu’il n’y avait pas assez de données sources pour le contrôle ; cela ne signifie jamais « aucun conflit ».
- **Aucun conflit observé ; les données peuvent être incomplètes. :** aucun conflit n’a été observé, mais ce n’est pas un résultat d’éligibilité complet.
- **La demande a changé après cette proposition ; une nouvelle proposition est requise. :** la demande de mission a changé après la révision sur laquelle reposait la proposition.

## Ce que vous ne pouvez pas faire / prudence
- Une proposition d’affectation à terre ne prime jamais sur la décision de sécurité du Capitaine de remorqueur à bord (13.03 §3, §7).
- Le proposant ne peut pas accepter sa propre proposition ; la réponse appartient uniquement à la personne désignée pour ce remorqueur.
- Aucune proposition ne peut être faite pour une mission en brouillon ou annulée.
- Les commandes sont désactivées hors ligne.

## En cas de problème
- **Message « Le résultat est incertain » :** **Réessayer** renvoie la même commande ; ne créez pas de nouvelle proposition.
- **Message « L’enregistrement a changé » :** utilisez **Recharger** pour examiner l’enregistrement actuel, puis agissez à nouveau.
- **« Vous n’êtes pas autorisé pour cette action. » / « Vous n’avez pas accès à cet enregistrement. » :** vérifiez votre rôle, votre remorqueur et votre contexte de désignation ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Conflit inattendu :** notez le code de motif et examinez-le avec le responsable de l’autre mission.

## Pages associées
