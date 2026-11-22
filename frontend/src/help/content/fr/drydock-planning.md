---
key: drydock-planning
title: Planification de la mise en cale sèche
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La planification de la mise en cale sèche contient les **dossiers de cale sèche** du navire sélectionné (projet, chantier naval, dates prévues et éléments du périmètre). Elle répond à la question : « À quel ordre de travail, défaut, certificat ou liste de contrôle de visite chaque élément du périmètre de cale sèche est-il lié, et ces liens sont-ils à jour ? » Le périmètre est lié à ces enregistrements (13.03 §5). **Complétude des liens** concerne uniquement l’exhaustivité des liens ; ce n’est ni un résultat de visite de classe ou de pavillon, ni une déclaration de navigabilité.

## Qui l’utilise
- **Surintendant technique :** pour préparer un dossier de cale sèche, lier le périmètre aux enregistrements, le planifier et l’achever (13.03 §5).
- **Chef mécanicien :** pour suivre la façon dont le périmètre est lié aux enregistrements de défauts et d’ordres de travail et, avec l’autorisation requise, vérifier un dossier achevé par quelqu’un d’autre (13.03 §5).

## Avant de commencer
- Sélectionnez le bon navire. Si aucun navire n’est sélectionné, la page indique **Sélectionnez un navire dans l'espace de travail pour continuer.**
- Vous avez besoin d’un accès en lecture aux cales sèches pour voir les dossiers. La création, la modification et le changement de statut requièrent un accès en écriture ; la vérification requiert un accès de vérification.
- Gardez à portée de main les identifiants et les révisions des enregistrements d’ordres de travail, de défauts, de certificats et de listes de contrôle de visite que vous allez lier.

## Étape par étape
1. Ouvrez un dossier depuis la liste, ou créez un brouillon vide avec **Créer un dossier de cale sèche**.
2. Renseignez **Projet de cale sèche**, **Chantier naval**, **Début prévu** et **Fin prévue**.
3. Ajoutez des éléments avec **Ajouter un élément du périmètre** ; saisissez le type d’**Élément du périmètre**, **Titre**, **Lot de travaux**, **Exigence de la société de classification**, **Exigence réglementaire** et **Priorité**.
4. Sur chaque élément, utilisez **Ajouter un lien** et saisissez **Type d’enregistrement**, **Identifiant de l'enregistrement** et **Révision épinglée**.
5. Pour l’achèvement, ajoutez une référence de preuve sous **Preuve d'achèvement** avec **Ajouter**. Appuyez ensuite sur **Enregistrer**.
6. Vérifiez la liste **Points bloquants pour la planification** ou **Points bloquants pour l'achèvement**. Sous **Changer le statut**, choisissez une **Action** (**Planifier**, **Revenir au brouillon**, **Démarrer la cale sèche**, **Achever la cale sèche**, **Annuler le dossier**) et confirmez-la. L’annulation requiert une note. Seules les actions valides pour l’état actuel sont proposées ; un dossier ne peut être annulé qu’à l’état Brouillon ou Planifié.
7. Sur un dossier achevé que vous n’avez pas achevé vous-même, rédigez un **Motif** d’au moins 10 caractères et utilisez **Vérifier l'enregistrement** ou **Rejeter la vérification**.
8. Suivez les dates prévues et réelles ainsi que chaque révision dans la **Chronologie**.

## Signification des états
- **Draft / Planned / En cours / Completed / Verified / Cancelled :** le cycle de vie du dossier. Achevé et Vérifié ne sont pas la même chose.
- **Links current / Attention / Revue requise / Incomplete :** la complétude des liens. Même avec des liens à jour, ce n’est pas un résultat de visite.
- **Linked / Unlinked / Overdue / Revue requise :** l’état de liaison d’un élément du périmètre (Revue requise lorsqu’un lien est Obsolète ou Indisponible).
- **Current / Stale / Unavailable :** un enregistrement lié comparé à sa révision épinglée. Examinez un lien obsolète et utilisez **Épingler à nouveau sur** pour l’épingler à la nouvelle révision.
- **Inconnu / non renseigné :** aucune priorité n’a été saisie. Inconnu ne signifie ni « Normal » ni « Faible ».

## Ce que vous ne pouvez pas faire / prudence
- La page ne décide pas qu’une mise en cale sèche est sûre ni que le navire est en état de navigabilité.
- Vous ne pouvez pas vérifier un dossier que vous avez achevé. Le rejet de la vérification remet le dossier à l’état **En cours**.
- Le coût, la devise, le fournisseur et le budget ne sont que des références externes ; rien n’est totalisé, approuvé ou commandé ici.
- Un dossier achevé, vérifié ou annulé ne peut pas être modifié.
- Les commandes ne sont pas envoyées hors ligne.

## En cas de problème
- **Le serveur a listé des éléments manquants ou bloquants :** résolvez la liste des points bloquants et réessayez.
- **L’enregistrement a changé ou la commande est en conflit :** utilisez **Recharger la dernière version** et examinez.
- **Le résultat est inconnu :** **Réessayer** renvoie la commande identique.
- **Vous n’avez pas accès à cet enregistrement :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Vous ne pouvez pas lire ces types d'enregistrements liés :** vérifiez l’autorisation auprès du responsable de l’enregistrement ou de votre administrateur de locataire.

## Pages associées
