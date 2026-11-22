---
key: survey-planner
title: Planificateur de visites
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le planificateur de visites affiche les fenêtres de visite déclarées sur un calendrier de flotte, avec l’adéquation au voyage et au port. Il répond à la question : « Dans la période choisie, quels certificats de quels navires ont une fenêtre de visite qui s’ouvre ou est dépassée, et existe-t-il une liste de contrôle de préparation ? » C’est uniquement une aide à la planification : ni un calendrier de l’autorité, ni une réservation, ni un résultat de visite (13.03 §8).

## Qui l’utilise
- **Capitaine, Chef mécanicien / Surintendant technique :** voient les visites à venir et démarrent à temps la liste de contrôle de préparation (13.03 §5).
- **Utilisateur Réglementation / Assurance :** surveille les fenêtres déclarées et les déclarations manquantes dans toute la flotte (13.03 §8).

## Avant de commencer
- La consultation nécessite la lecture des certificats. Seuls les navires auxquels vous avez accès sont listés.
- Les fenêtres proviennent des déclarations de l’enregistrement du certificat. Si aucune fenêtre n’est déclarée, la ligne apparaît sous **Fenêtre non déclarée** ; saisissez les dates issues du document source dans le [Centre de commande des certificats](/app/certificate-command-center).
- La page nécessite une lecture en direct. Hors ligne, vous voyez « Hors ligne : une lecture en ligne à jour est nécessaire. »

## Étape par étape
1. Dans **Période du calendrier**, choisissez **Du** et **Au** et appuyez sur **Afficher**.
2. Lisez les nombres de fenêtres ouvertes, à venir, en retard et inconnues dans la ligne de synthèse.
3. Sur chaque ligne, examinez **Certificat**, **Émetteur**, **Pavillon déclaré**, **Base réglementaire**, **Expiration déclarée**, **Fenêtre de visite** et **Adéquation voyage/port**.
4. Appuyez sur **Ouvrir le certificat** pour voir le certificat.
5. Utilisez **Ouvrir la liste de contrôle** si une liste de préparation existe, ou **Démarrer une liste de contrôle** sinon, pour aller à la [Liste de contrôle avant visite](/app/pre-survey-checklist).

## Signification des états
- **Upcoming / Window open :** la position par rapport à la fenêtre déclarée.
- **Fenêtre passée, réalisation non enregistrée :** la fenêtre est terminée et aucun enregistrement ne montre que la visite a été effectuée. Cela ne prouve pas non plus que la visite a été manquée ; vérifiez les enregistrements.
- **Fenêtre inconnue :** la date manque ou est illisible. Une fenêtre inconnue ne signifie jamais « aucun problème » ; elle nécessite une revue.
- Adéquation voyage/port : **Conditionnel**, **Non adapté**, **Inconnu**. La page ne donne jamais d’approbation « adapté ».
- État de préparation de la liste de contrôle : **Bloqué**, **Incomplet**, **Préparé, confirmation humaine en attente**.

## Ce que vous ne pouvez pas faire / prudence
- Le planificateur ne réserve rien et ne dépose rien auprès d’une autorité. La coordination avec la classe, le pavillon et l’autorité est externe et n’est pas simulée ici.
- Ce que montre le calendrier n’est ni un résultat de visite ni la validité d’un certificat. Les décisions de voyage et de visite appartiennent aux personnes autorisées.
- La page est en lecture seule ; les modifications se font sur les pages des certificats et des listes de contrôle.

## En cas de problème
- **Seule une partie de la flotte est affichée ; réduisez la période.** Choisissez une période de calendrier plus courte.
- **Aucun certificat avec fenêtre de visite sur cette période.** Élargissez la période ou vérifiez les déclarations des certificats.
- **Chargement impossible. Réessayez.** Appuyez sur **Réessayer**. Si cela persiste, signalez-le au support avec l’écran, l’heure et vos étapes (13.03 §10).
- **Vous n’avez pas accès à cette vue.** Vérifiez votre rôle et le périmètre des navires ; contactez si nécessaire l’administrateur du locataire (13.03 §10).

## Pages associées
