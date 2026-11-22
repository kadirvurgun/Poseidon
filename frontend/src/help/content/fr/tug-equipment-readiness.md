---
key: tug-equipment-readiness
title: Préparation de l'équipement de remorquage
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Cette page affiche les **déclarations d’équipement de remorquage** du remorqueur sélectionné (treuil de remorquage, croc de remorquage, remorque, largage d’urgence, lutte contre l’incendie FiFi) et leurs **vérifications par le chef mécanicien**. Elle répond à la question : « Quels sont la CMU, les dates d’essai, la condition et la criticité de chaque élément, qui l’a vérifié et que dit le contrôle de préparation ? » L’évaluation n’est qu’un avis et alimente le contrôle de préparation ; ce n’est pas une décision GO.

## Qui l’utilise
- **Capitaine du remorqueur :** pour contrôler le treuil, la remorque, le croc de remorquage / stoppeur de chaîne, le largage d’urgence, le FiFi et les preuves de défauts critiques (13.03 §3).
- **Chef mécanicien :** pour vérifier ou ne pas vérifier les déclarations et enregistrer l’effet d’un défaut critique sur la préparation (13.03 §5).

## Avant de commencer
- Ouvrez le remorqueur depuis **Opérations de remorqueur** ; cet écran ne nécessite aucune affectation.
- L’ajout ou la modification d’une déclaration exige un accès en écriture remorqueur et en écriture équipement.
- L’enregistrement d’une vérification exige une désignation de **Chef mécanicien** enregistrée sur le serveur pour ce remorqueur.

## Étape par étape
1. Lisez le badge global sur la ligne **Évaluation**.
2. Sur chaque carte d’équipement, contrôlez le badge de résultat, le code de motif, **CMU (t)**, **Prochain essai dû**, **Condition** et **Critique**.
3. Pour une nouvelle déclaration, appuyez sur **Ajouter un équipement** ; remplissez **Type d’équipement**, **Libellé**, **CMU (t)**, **Dernier essai**, **Prochain essai dû**, **Condition**, **Critique**, **ID d'actif PMS**, **ID du certificat** et **Notes**, puis appuyez sur **Enregistrer la déclaration**.
4. Pour changer une déclaration existante, utilisez **Modifier** sur sa carte.
5. En tant que chef mécanicien, choisissez votre **Désignation**, rédigez un motif et utilisez **Vérifié** ou **Non vérifié** pour **Enregistrer la vérification**. Un motif est obligatoire pour « Non vérifié ».
6. Consultez l’historique des vérifications avec révision et heure sous **Vérifications du chef mécanicien**, et le tableau **Contrôles des sources** en bas de page.

## Signification des états
- **Prêt / Conditionnel / Non prêt / Examen requis :** l’évaluation consultative. Prêt n’est pas une décision GO.
- **Conforme / Non conforme / Inconnu :** le résultat du contrôle d’un élément.
- **En état de service / Dégradé / Défaillant / Inconnu :** la condition déclarée. Si aucune condition n’est choisie, elle reste « Inconnu » ; elle n’est pas comptée comme en état de service.
- **Non évalué :** la criticité n’a pas été évaluée. Cela ne signifie pas « Non ».
- **Vérifié / Non vérifié :** l’enregistrement humain du chef mécanicien.
- **remplacé par une révision ultérieure :** la vérification portait sur une révision plus ancienne de la déclaration ; la révision actuelle exige une nouvelle vérification.

## Ce que vous ne pouvez pas faire / prudence
- L’enregistrement crée une déclaration non vérifiée. Toute modification exige une nouvelle vérification du chef mécanicien.
- Achever un enregistrement et le vérifier ne sont pas la même action ; la vérification attend la personne autorisée (13.03 §1).
- Le type d’équipement ne peut pas être changé après l’enregistrement ; ajoutez une nouvelle déclaration pour un autre type.
- La page ne déclare jamais l’équipement de remorquage sûr ; la décision reste au Capitaine du remorqueur (13.03 §3).
- Les commandes sont désactivées hors ligne.

## En cas de problème
- **Message « L’enregistrement a changé » :** utilisez **Recharger** pour examiner la déclaration actuelle.
- **Message « Le résultat est incertain » :** **Réessayer** renvoie la même commande.
- **L’enregistrement ne peut pas être vérifié :** contrôlez les preuves et la source PMS/certificat ; contactez le vérificateur (13.03 §10).
- **Aucune autorisation :** vérifiez votre rôle, votre remorqueur et votre contexte de désignation ; contactez votre administrateur de locataire si nécessaire (13.03 §10).

## Pages associées
