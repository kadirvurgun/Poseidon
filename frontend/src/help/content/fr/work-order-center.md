---
key: work-order-center
title: Ordres de travail
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le centre des ordres de travail contient les **brouillons d’ordres de travail et les enregistrements d’exécution déclarés** du navire sélectionné. Il répond à la question : « Quels ordres de travail existent sur ce navire, et lesquels ont été libérés, exécutés, ont fait l’objet d’une soumission de preuves et ont été vérifiés par une personne ? » Chaque révision enregistrée est conservée dans un historique immuable. La vérification et la clôture ne sont ni une remise en service, ni un permis, ni une approbation de risque.

## Qui l’utilise
- **Chef mécanicien / Surintendant technique :** pour ouvrir, libérer, vérifier et clôturer des ordres de travail planifiés et non planifiés (13.03 §5).
- **Mécanicien / équipage :** pour démarrer le travail affecté et enregistrer et soumettre relevés, pièces de rechange, preuves photo et documents (13.03 §6).

## Avant de commencer
- La lecture exige `workorder.read`, `equipment.read` et `maintenance.read`. Les brouillons exigent `workorder.write`, la libération `workorder.release`, l’exécution `workorder.execute`, la vérification `workorder.verify` et la clôture `workorder.close`.
- La libération, la vérification et la clôture exigent un utilisateur humain connecté.
- Choisissez le bon navire dans **Sélectionner un navire ou un remorqueur**.

## Étape par étape
1. Choisissez le navire. Dans **Ordres de travail enregistrés**, filtrez cette page avec **Rechercher les numéros d’ordres de travail renvoyés** et ouvrez un enregistrement.
2. Pour un nouveau travail, appuyez sur **Nouveau brouillon d’ordre de travail**, remplissez les champs et appuyez sur **Enregistrer le brouillon non évalué**. Les brouillons incomplets peuvent être enregistrés.
3. Dans **Exécution et preuves**, si vous avez l’autorisation de libération, choisissez **Déclarer les preuves requises** sous **Politique des preuves requises**, cochez les types de preuves et appuyez sur **Libérer l'ordre de travail**. Si vous libérez sans déclaration, la vérification reste bloquée jusqu’à ce qu’une personne habilitée à libérer utilise **Modifier les preuves requises** → **Enregistrer les preuves requises**.
4. L’exécutant appuie sur **Démarrer le travail**, remplit **Relevés**, **Pièces de rechange consommées**, **Preuves photo et documents**, **Travail effectué** et **Constats**, appuie sur **Enregistrer les preuves** puis sur **Soumettre les preuves**.
5. Un vérificateur autorisé qui n’a pas exécuté le travail examine les preuves. Il appuie sur **Enregistrer la vérification**, ou choisit un **Motif du renvoi** et appuie sur **Renvoyer à l'exécutant**.
6. Pour un ordre de travail vérifié, une personne autorisée appuie sur **Enregistrer la clôture**.
7. Consultez les révisions antérieures dans **Historique immuable des ordres de travail**. Si un enregistrement d’indisponibilité est nécessaire, utilisez **Ouvrir le brouillon d’indisponibilité lié**.

## Signification des états
- **Non libéré pour exécution. / Libéré / En cours :** le travail n’est pas encore libéré, est libéré ou est en cours d’exécution.
- **Preuves soumises :** l’exécutant a déclaré le travail et les preuves. Cela ne signifie pas vérifié.
- **Vérifié (enregistrement humain) :** enregistré par un vérificateur autorisé qui n’a pas exécuté le travail. **Clôturé (enregistrement humain)** est une étape distincte.
- **Preuves requises manquantes / Présentes, en attente de vérification humaine / Vérifiées par une personne :** l’état du contrôle des preuves requises.
- **Revue requise : politique non configurée :** aucune preuve requise n’a été déclarée. Non configuré ne signifie jamais que rien n’est requis ; la vérification reste bloquée.
- **Renvoyé par le vérificateur :** le travail est retourné à l’exécutant avec le motif affiché.

## Ce que vous ne pouvez pas faire / prudence
- Si vous avez exécuté le travail, vous ne pouvez ni le vérifier ni le clôturer. La page indique « Vous avez exécuté ce travail, vous ne pouvez donc ni le vérifier ni le clôturer. »
- Preuves soumises, Vérifié et Clôturé sont des enregistrements humains distincts. La page ne déclare jamais un équipement prêt pour le service.
- Le résumé « Source d’ordre de travail enregistrée » décrit les champs du brouillon enregistré et indique : « La libération, l’enregistrement des preuves, la vérification indépendante et la clôture sont enregistrés dans le panneau d’exécution. Les preuves soumises ne sont acceptées que par une vérification humaine indépendante, jamais par la personne qui a effectué le travail ; il n’existe pas d’étape distincte d’acceptation des preuves, et la vérification n’est pas une décision de remise en service. » Ces actions sont effectuées dans **Exécution et preuves**.
- Les relevés sont conservés tels que déclarés ; aucune conversion d’unité ni aucun seuil n’est appliqué.
- La file d’attente de preuves hors ligne ne fonctionne qu’en présence d’une politique hors ligne approuvée et pendant que le travail est en cours. Les conflits ne sont jamais fusionnés automatiquement ; utilisez **Ouvrir le centre des conflits** et une personne décide.

## En cas de problème
- **L’enregistrement a changé ou cette action n’est pas autorisée dans son état actuel :** utilisez **Recharger** pour lire l’enregistrement actuel et réessayez.
- **Résultat inconnu :** **Réessayer la même commande** renvoie la même commande ; cela ne crée pas de nouvel enregistrement.
- **Autorisation ou autorité humaine manquante :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Le contrôle des preuves requises a rejeté cette action :** complétez les types de preuves manquants. Si un enregistrement ne peut pas être vérifié, contrôlez les preuves et leur provenance avec le vérificateur (13.03 §10).

## Pages associées
