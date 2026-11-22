---
key: tug-job-board
title: Tableau des missions
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [canRead, canWrite]
related: [tug-command, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 7]
reviewed: false
---
## À quoi sert cette page
Le tableau des missions liste, pour la planification, les **demandes de mission de remorqueur en brouillon** du contexte de remorqueur sélectionné. Il répond à la question : « Quelles demandes existent, quelle fenêtre horaire demandent-elles, combien de remorqueurs et quelle traction au point fixe (BP) exigent-elles, et que manque-t-il encore pour la planification ? » Le tableau n’affiche que des déclarations en brouillon ; ce n’est ni une affectation opérationnelle, ni une éligibilité, ni un permis, ni une approbation de sécurité.

## Qui l’utilise
- **Coordinateur des opérations de remorquage / Opérations à terre :** pour voir les demandes, suivre les informations de conflit et de préparation et préparer une proposition d’affectation (13.03 §7).
- **Capitaine du remorqueur :** pour voir à l’avance les travaux à venir pour son remorqueur (13.03 §3).

## Avant de commencer
- Dans la barre de contexte en haut, sélectionnez la **bonne société et le bon remorqueur**. Le tableau n’affiche que les demandes de ce contexte.
- Vous avez besoin d’un accès en lecture pour voir le tableau. La création d’une demande exige un accès en écriture ; sans lui, le bouton **Nouvelle demande** n’est pas affiché.

## Étape par étape
1. Dans le résumé en haut, consultez le nombre de demandes sur cette page, les missions avec un blocage enregistré, celles en attente d’examen de préparation et celles dont la préparation est inconnue. **Les comptes ne couvrent que cette page.**
2. Si nécessaire, saisissez un numéro de mission ou le nom d’un navire assisté dans **Rechercher des demandes**, choisissez les filtres **Priorité**, **État** et **Fenêtre horaire** et appuyez sur **Appliquer les filtres**.
3. Sur chaque ligne, lisez la fenêtre demandée (UTC), les affectations en brouillon, le nombre de remorqueurs requis, la BP requise par rapport à la capacité de planification, la préparation et le conflit opérationnel.
4. Ouvrez **Motif / prochaine action** sur une ligne pour voir les références des remorqueurs affectés, le périmètre de comparaison et les motifs enregistrés.
5. Utilisez **Ouvrir la mission** pour accéder aux détails de la demande.
6. Pour une nouvelle demande, appuyez sur **Nouvelle demande** pour ouvrir la **Réception des missions** (exige un accès en écriture).
7. Utilisez **Actualiser le tableau** pour relire les données et **Précédent** / **Suivant** pour les autres pages.

## Signification des états
- **Brouillon / Soumise / Annulée :** l’état du cycle de vie de la demande. La soumission enregistre la demande pour la planification ; ce n’est ni une approbation, ni une affectation, ni une décision de préparation.
- **Bloqué :** la demande a un blocage enregistré. Le motif se trouve sous **Motif / prochaine action**.
- **Revue requise :** les informations de préparation attendent l’examen d’une personne autorisée.
- **Inconnu :** il n’y a pas assez de données sources pour la préparation ou le conflit. Inconnu ne signifie ni « Prêt » ni « aucun conflit », et le tableau n’en tire aucune conclusion de préparation.
- **Examen humain autorisé requis / Examen de contact aux limites requis :** deux demandes peuvent se chevaucher dans le temps ou se toucher bout à bout. Une personne décide.
- **BP du profil (non vérifiée) :** la traction au point fixe affichée est une valeur déclarée, ni mesurée ni vérifiée.

## Ce que vous ne pouvez pas faire / prudence
- Le tableau ne déclare jamais une demande « prête » ou « sûre ». Une proposition d’affectation à terre ne retire pas l’autorité de sécurité du Capitaine du remorqueur à bord (13.03 §7).
- Les champs d’une demande soumise ou annulée ne sont pas modifiés depuis le tableau ; une demande annulée ne peut pas être relancée, créez-en une nouvelle à la place.
- Le filtre de fenêtre horaire utilise l’heure du serveur. Les demandes sans début restent dans la liste comme « Inconnu ».
- Les données affichées hors ligne peuvent être anciennes et le tableau ne peut pas être actualisé.

## En cas de problème
- **Tableau actuel indisponible :** vérifiez votre connexion et utilisez **Actualiser le tableau**.
- **Aucune demande en brouillon :** si vous avez appliqué des filtres, essayez une autre recherche ou priorité ; les nouvelles demandes apparaissent dans ce contexte.
- **Accès indisponible :** vérifiez votre rôle et votre contexte de remorqueur ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Blocage ou examen inattendu :** ouvrez **Motif / prochaine action** et examinez les motifs enregistrés avec le responsable de la demande.

## Pages associées
