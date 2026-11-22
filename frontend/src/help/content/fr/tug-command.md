---
key: tug-command
title: Commandement des remorqueurs
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## À quoi sert cette page
Affichée dans le menu sous le nom **Commandement des remorqueurs**, cette page présente le **contexte source** du remorqueur sélectionné. Elle répond à la question : « Quelles déclarations de rôle, de type, de traction au point fixe (BP) et d’équipement sont enregistrées pour ce remorqueur, d’où viennent-elles et sont-elles à jour ? » La page n’affiche que les déclarations et leurs sources ; elle ne donne aucune aptitude opérationnelle, éligibilité, permis ni approbation de sécurité. Le badge en haut de page le rappelle : « Déclarations sources · aptitude opérationnelle inconnue ».

## Qui l’utilise
- **Capitaine du remorqueur :** pour confirmer le contexte de rôle, de BP et d’équipement de son propre remorqueur avant le début des travaux (13.03 §3).
- **Coordinateur des opérations de remorquage / Opérations à terre :** pour s’assurer d’être dans le bon contexte de remorqueur avant de passer au tableau des missions et à la réception des missions (13.03 §7).
- **Équipage :** pour voir sa vue de rôle et les écrans du mode navire qui lui sont ouverts (13.03 §6).

## Avant de commencer
- Vous avez besoin du mode remorqueur et d’un accès en lecture remorqueur. Sans cela, la page affiche « L’accès est indisponible. ».
- Si aucun contexte n’est sélectionné, la page liste les remorqueurs autorisés sous **Sélectionner un remorqueur** ; ouvrez-en un.
- La liste des équipements exige aussi un accès en lecture aux équipements ; sans lui, la source des équipements apparaît comme « indisponible ou non accordée ».

## Étape par étape
1. Ouvrez le remorqueur depuis la liste **Sélectionner un remorqueur**. La ligne **Contexte du remorqueur** sous le titre affiche le code, le nom et le mode du remorqueur.
2. Dans **Rôle (attribution serveur)** et **Vue de menu mode remorqueur**, lisez le rôle et la vue de menu que le serveur vous a attribués.
3. Dans **Visibilité des modes**, voyez si les écrans du mode navire sont ouverts. La section **Écrans du mode navire** ne renvoie qu’aux écrans qui vous sont accordés.
4. Vérifiez dans **Type de remorqueur**, **Source / révision du type** et **Observation / politique de la source du type** de quelle source et de quelle révision provient le type.
5. Lisez **BP du profil (non vérifiée)**, **Source / version / provenance BP** et **Fraîcheur / observation BP**. Il s’agit d’une valeur déclarée, non mesurée.
6. Dans **Contexte des équipements**, consultez les déclarations d’équipement, leurs niveaux, révisions et provenance.
7. Utilisez les onglets de la page pour passer au **Tableau des missions** ou (avec accès en écriture) à la **Réception des missions**, ou (avec accès en écriture) appuyez sur **Nouvelle demande**.

## Signification des états
- **Inconnu — aucune attribution de rôle :** le serveur n’a aucune attribution de rôle pour vous sur ce remorqueur. Ce n’est pas « aucun rôle, aucun problème » ; une attribution par un administrateur est nécessaire.
- **Libellés de rôle indisponibles / Vue de menu indisponible :** la source ne peut pas être lue pour le moment. Une information manquante n’est pas considérée comme vide.
- **Aucune vue configurée pour le mode remorqueur :** aucune vue de menu n’est définie pour vous dans ce mode.
- **Masqués en mode remorqueur. Un rôle inter-modes est requis. :** les écrans du mode navire exigent un rôle inter-modes explicitement autorisé.
- **BP du profil (non vérifiée) :** la traction au point fixe déclarée. Ce n’est pas un résultat d’essai vérifié.
- **Aucune déclaration d’équipement renvoyée. L’aptitude opérationnelle vérifiée reste inconnue. :** une liste vide ne signifie jamais que l’équipement est apte.
- **Inconnu / Indisponible :** la valeur source manque ou n’a pas pu être lue. Inconnu ne remplace jamais « Prêt » ou « Non ».

## Ce que vous ne pouvez pas faire / prudence
- Aucun champ n’est modifié sur cette page et aucun résultat de préparation n’est produit. La décision opérationnelle et l’autorité de sécurité restent au Capitaine du remorqueur à bord (13.03 §3).
- Une proposition d’affectation à terre ne prime jamais sur la décision de sécurité du Capitaine du remorqueur à bord (13.03 §7).
- Même avec une autorisation inter-modes déclarée, chaque écran du mode navire exige toujours sa propre autorisation serveur.
- Hors ligne, les sources de profil actuelles ne sont pas affichées ; reconnectez-vous pour revérifier l’accès et les versions des sources.

## En cas de problème
- **L’accès est indisponible :** vérifiez votre rôle, votre remorqueur et votre contexte d’objet ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Alerte « L’identité actuelle et l’accès au navire doivent être vérifiés » :** appuyez sur **Revérifier l’accès**.
- **Données indisponibles :** vérifiez votre connexion et appuyez sur **Réessayer**.
- **Valeurs de BP ou d’équipement inattendues :** notez les champs de source et de version et examinez-les avec le responsable de la source ; si un enregistrement ne peut pas être vérifié, contrôlez ses preuves et sa provenance (13.03 §10).

## Pages associées
