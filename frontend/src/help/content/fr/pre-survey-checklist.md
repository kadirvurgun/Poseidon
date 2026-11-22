---
key: pre-survey-checklist
title: Liste de contrôle avant visite
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La liste de contrôle avant visite suit les tâches de préparation de la visite d’un certificat avec responsable, échéance, preuves et vérification humaine indépendante. Elle répond à la question : « Quels éléments de préparation de cette visite sont ouverts, lesquels ont des preuves, et lesquels ont été vérifiés par une autre personne ? » L’état de préparation est consultatif et n’est jamais une approbation de la visite (13.03 §1, §8).

## Qui l’utilise
- **Capitaine, Chef mécanicien / Surintendant technique :** démarrent une liste de contrôle, renseignent les éléments et joignent des preuves.
- **Vérificateur :** compare les éléments renseignés par quelqu’un d’autre avec leurs preuves et les vérifie ou les rejette (13.03 §1).
- Les utilisateurs disposant uniquement d’un accès en lecture voient « Lecture seule pour votre rôle. »

## Avant de commencer
- La consultation nécessite la lecture des certificats. Démarrer et renseigner une liste de contrôle nécessite la planification des visites sur ce navire ; la vérification nécessite la revue des visites.
- Choisissez le bon navire avec **Choisir un navire** (13.03 §1).
- La fenêtre de visite du certificat doit être déclarée dans le [Centre de commande des certificats](/app/certificate-command-center).
- La page nécessite une lecture en direct. Hors ligne, vous voyez « Hors ligne : une lecture en ligne à jour est nécessaire. »

## Étape par étape
1. Consultez les listes du navire sous **Listes de contrôle**, ou, dans le [Planificateur de visites](/app/survey-planner), appuyez sur **Ouvrir la liste de contrôle** ou **Démarrer une liste de contrôle** sur une ligne.
2. Pour une nouvelle liste, utilisez **Choisir un certificat**, choisissez un **Modèle** et appuyez sur **Créer la liste de contrôle**. Le modèle est un modèle de départ, pas un catalogue approuvé.
3. Dans **Données du plan de visite**, remplissez **Début prévu**, **Fin prévue**, **Lieu prévu (texte)** et, si disponible, **Escale**.
4. Dans **Éléments**, saisissez pour chaque élément le **Rôle responsable**, l’**Échéance**, la **Réponse** et, si nécessaire, le **Constat** et un **Commentaire**. Liez des preuves avec **Ajouter une preuve**, puis appuyez sur **Enregistrer**. Chaque enregistrement crée une nouvelle révision.
5. En tant que vérificateur, sur les éléments renseignés par quelqu’un d’autre, choisissez **Vérifier** ou **Rejeter** sous **Décision**, saisissez le motif et appuyez sur **Enregistrer la vérification**.
6. Surveillez **État de préparation** et **Blocages**.

## Signification des états
- Réponse d’un élément : **Ouvert**, **Prêt**, **Attention requise**, **Bloqué**, **Sans objet**. Un élément dont la réponse est inconnue reste **Ouvert** ; il ne compte jamais comme « Sans objet » ni comme « Prêt ».
- Vérification : **En attente**, **Vérifié (humain)**, **Rejeté**. Un élément renseigné reste **En attente** jusqu’à sa vérification.
- État de préparation : **Bloqué**, **Incomplet**, **Préparé, confirmation humaine en attente**. Même le meilleur état attend une confirmation humaine ; le système ne dit jamais « visite approuvée ».
- **Critical / Required / Optional :** l’importance de l’élément. Les éléments critiques et requis non résolus pèsent contre l’état de préparation et apparaissent sous **Blocages**.
- **Voyage : inconnu (pas de module voyage) :** l’adéquation au voyage ne peut pas encore être évaluée et reste inconnue.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas vérifier un élément que vous avez renseigné : « Vous avez renseigné cet élément ; une autre personne doit le vérifier. »
- Renseigner un élément comme **Prêt** ne le vérifie pas ; Terminé ≠ Vérifié. Chaque décision de vérification est liée à la réponse et aux preuves exactes que vous voyez.
- L’état de préparation n’est ni une approbation de la visite, ni une réservation, ni la validité du certificat. La coordination avec la classe, le pavillon et l’autorité est externe et n’est pas simulée ici.
- Les éléments du modèle ne sont pas un catalogue approuvé ; une personne autorisée juge s’ils conviennent au navire.

## En cas de problème
- **Cette liste a changé depuis son ouverture. Rechargez avant de modifier.** Appuyez sur **Recharger la version actuelle**.
- **Le résultat n’est pas encore connu. Réessayer renvoie la même requête.** Utilisez **Renvoyer la même requête**.
- **Éléments non enregistrés :** corrigez les éléments listés et enregistrez à nouveau.
- **Vous n’avez pas accès à cette vue.** Vérifiez votre rôle, le navire et le périmètre de l’objet ; contactez si nécessaire l’administrateur du locataire (13.03 §10).
- Si un élément ne peut pas être vérifié, contrôlez ses preuves et sa source et transmettez-le au vérificateur (13.03 §10).

## Pages associées
