---
key: critical-spares
title: Pièces de rechange critiques et réquisitions
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page des pièces de rechange critiques et des réquisitions affiche, pour le navire sélectionné, le **stock déclaré, le minimum et le maximum, les alertes de stock bas et les propositions de réquisition technique**. Elle répond à la question : « Quelle pièce de rechange critique a atteint son minimum, combien d’ordres de travail ouverts y font référence, et une réquisition technique a-t-elle été proposée et décidée ? » Les quantités sont des déclarations humaines et ne sont pas vérifiées par rapport à un magasin.

## Qui l’utilise
- **Chef mécanicien / Surintendant technique :** pour suivre les seuils minimaux des pièces de rechange critiques et lancer une réquisition technique (13.03 §5). La personne qui propose une réquisition et celle qui l’approuve sont des personnes différentes.

## Avant de commencer
- La consultation requiert `spare.read`. La définition de pièces et l’enregistrement d’inventaires requièrent `spare.write`, la proposition d’une réquisition `requisition.propose` et la décision `requisition.approve`.
- Les propositions et les décisions requièrent un utilisateur humain connecté.
- Choisissez le bon navire dans la liste **Navire**.

## Étape par étape
1. Choisissez le navire. La vue **Pièces de rechange critiques** s’ouvre. Lisez les nombres **Stock bas** et **Revue requise** en haut.
2. Sur chaque ligne du tableau, lisez **Criticité de la pièce**, **Criticité de l’équipement**, **État du stock**, **Projeté**, **Min / max** et **Ordres de travail ouverts**.
3. Appuyez sur **Nouvelle pièce de rechange critique** pour une nouvelle pièce ou sur **Modifier** pour une pièce existante. Saisissez **Référence de la pièce**, **Nom**, **Unité**, **Stock minimum** et **Stock maximum**. Laissez les seuils vides tant qu’ils ne sont pas décidés.
4. Pour saisir un inventaire, cochez **Enregistrer un nouvel inventaire**, renseignez **Quantité comptée** et appuyez sur **Enregistrer**.
5. Sur une ligne **Stock bas** sans réquisition ouverte, appuyez sur **Proposer une réquisition**. Le serveur calcule la quantité proposée (maximum moins stock projeté). Si aucun maximum n’est défini, aucune quantité n’est proposée et l’approbateur doit saisir **Quantité approuvée**.
6. Dans la vue **Réquisitions techniques**, une personne autorisée qui n’a pas fait la proposition vérifie **Quantité approuvée** et appuie sur **Approuver pour l'export ERP**, ou choisit un **Motif du rejet** et appuie sur **Rejeter**.
7. Ouvrez **Référence d'export ERP** sur une réquisition approuvée pour voir la référence.

## Signification des états
- **Stock bas :** le stock projeté (dernier inventaire déclaré moins les pièces enregistrées sur les ordres de travail soumis depuis cet inventaire) est inférieur ou égal au minimum. La proposition de réquisition est offerte sur cette ligne.
- **Au-dessus du minimum (déclaré) :** le stock projeté est supérieur au minimum ; l’inventaire n’est pas vérifié.
- **Minimum non configuré :** aucun seuil n’a été saisi. Un seuil vide est signalé comme revue requise, jamais comme sain.
- **Stock unknown / Consumption unresolved :** le stock n’a pas pu être calculé. Inconnu ne signifie pas « stock suffisant ».
- **Proposed / Approved for ERP export / Rejected :** l’état de la réquisition technique.
- **Critical (declared) / Non-critical (declared) / Inconnu :** la criticité de la pièce est une déclaration.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas décider d’une réquisition que vous avez proposée. La page indique « Vous l'avez proposée ; une autre personne doit décider. »
- La proposition et l’approbation sont des actes humains distincts. L’approbation crée uniquement une référence d’export ERP ; aucun bon de commande, fournisseur ou prix n’est géré ici.
- La page ne comporte pas de champ distinct de seuil de réapprovisionnement ; le stock bas est évalué par rapport au stock minimum.
- Hors ligne, les données ne peuvent pas être actualisées et aucune action ne peut être effectuée.

## En cas de problème
- **L’enregistrement a changé :** utilisez **Réessayer** pour actualiser la page et réessayez.
- **Outcome unknown :** **Réessayer la même commande** envoie la même commande.
- **La requête a été rejetée par une règle :** vérifiez l’état du stock et les informations de réquisition ouverte.
- **Vous n’avez pas l’autorisation :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire (13.03 §10).

## Pages associées
