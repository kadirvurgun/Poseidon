---
key: towage-plan
title: Plan de remorquage
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le plan de remorquage enregistre, pour une mission de remorqueur soumise, les **rôles et la configuration des remorqueurs, l’échange d’informations, les limites opérationnelles, la communication, le largage d’urgence, le plan d’urgence et les mesures pour la zone de rupture (snap-back)**. Il répond à la question : « Comment cette mission sera-t-elle exécutée, dans quelles limites et avec quelles dispositions d’urgence ? » L’approbation du plan est un enregistrement humain du Commandant ou du Capitaine du remorqueur désigné ; ce n’est ni une décision GO ni un permis. Les limites sont des données d’entrée du plan indiquées avec leur source ; le système ne fournit aucune valeur par défaut.

## Qui l’utilise
- **Coordinateur des opérations de remorquage / planificateur :** pour préparer le plan et le soumettre pour approbation (13.03 §7).
- **Capitaine du remorqueur / Commandant :** pour confirmer la configuration, la remorque, la BP/capacité, la communication, les limites, le plan d’urgence et les conditions d’arrêt des travaux, puis approuver ou renvoyer le plan (13.03 §3).

## Avant de commencer
- Ouvrez le remorqueur depuis **Opérations de remorqueur** et choisissez la mission dans **Sélectionner une mission de remorqueur soumise**.
- La modification du plan exige un accès en écriture remorqueur et en écriture plan. Les remorqueurs du plan sont choisis parmi les remorqueurs ayant une proposition d’affectation en cours pour cette mission.
- L’approbation ou le renvoi exige une **Désignation** (Capitaine du remorqueur ou Commandant) enregistrée sur le serveur pour le remorqueur coordinateur.

## Étape par étape
1. Lisez **État** et **Révision**, puis la liste **Éléments manquants avant soumission**.
2. Dans **Rôles et configuration des remorqueurs**, utilisez **Ajouter** pour chaque remorqueur ; saisissez **Position**, **Méthode de remorquage**, **Remorqueur coordinateur (principal)**, **Origine de la remorque**, **Point de connexion**, **Identifiant de l'équipement de remorque** et **Identifiant de l'équipement de largage d'urgence**.
3. Rédigez le texte **Échange d'informations Commandant / pilote / Capitaine du remorqueur**.
4. Dans **Limites opérationnelles (données d'entrée du plan)**, saisissez une **Valeur** et la **Source de la limite** pour chaque **Paramètre**. Une nouvelle limite commence vide.
5. Remplissez **Plan de communication** (canal principal et de secours, langue de travail, procédure en cas de perte de communication), **Largage d'urgence**, **Plan d'urgence** et **Mesures pour la zone de rupture (snap-back)**.
6. Enregistrez avec **Créer un brouillon** ou **Enregistrer le brouillon** ; lorsque plus rien ne manque, appuyez sur **Soumettre pour approbation**.
7. L’approbateur choisit une **Désignation**, rédige un **Motif** d’au moins 10 caractères et appuie sur **Approuver** ou **Renvoyer**.
8. Si un plan approuvé doit changer, rédigez un motif et utilisez **Modifier**. Les listes **Enregistrements d'approbation** et **Révisions** montrent l’historique.

## Signification des états
- **Brouillon :** le plan peut être modifié.
- **En revue :** le plan a été soumis pour approbation ; une décision est attendue.
- **Renvoyé :** l’approbateur a renvoyé le plan avec un motif ; corrigez-le et soumettez-le à nouveau.
- **Approuvé :** le Commandant ou le Capitaine du remorqueur a approuvé cette révision du plan. Ce n’est pas une décision GO ; le contrôle de préparation et le briefing restent nécessaires.
- **Inconnu :** aucune valeur n’a été choisie pour le champ. « Inconnu » pour les zones de rupture ne remplace jamais « Non ».
- **La demande de remorqueur a changé après l'enregistrement de ce plan. :** le plan repose sur une révision plus ancienne de la demande et doit être réexaminé.

## Ce que vous ne pouvez pas faire / prudence
- La personne qui a rédigé ou soumis le plan ne peut pas l’approuver.
- Un plan dont des sections obligatoires manquent ne peut pas être soumis ; un brouillon incomplet peut être enregistré.
- Le système ne propose aucune valeur de limite, de canal ou de seuil ; chaque valeur vient du planificateur avec sa source.
- Un plan approuvé n’est pas une autorisation de démarrer ; le démarrage exige des accusés de réception du briefing à jour et l’enregistrement GO du Capitaine du remorqueur (13.03 §3).
- Les commandes sont désactivées hors ligne.

## En cas de problème
- **Message « Des sections obligatoires manquent. » :** complétez les codes listés dans **Éléments manquants avant soumission**.
- **Message « L’enregistrement a changé » :** utilisez **Recharger** pour examiner le plan actuel.
- **Message « Le résultat est inconnu » :** **Réessayer** renvoie la même requête.
- **Aucune autorisation :** vérifiez votre rôle, votre remorqueur et votre contexte de désignation ; contactez votre administrateur de locataire si nécessaire (13.03 §10).

## Pages associées
