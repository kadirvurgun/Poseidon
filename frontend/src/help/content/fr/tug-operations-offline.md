---
key: tug-operations-offline
title: Opérations hors ligne du remorqueur
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Cette page conserve sur l’appareil un **instantané préchargé** d’une mission de remorqueur et place trois types de saisies faites sans connexion dans une **file d’attente durable et chiffrée** : accusés de réception de briefing, observations d’exécution et entrées du journal quotidien. Elle répond à la question : « Que puis-je saisir en sécurité sans connexion, qu’est-ce qui attend dans la file et comment le serveur l’a-t-il pris en compte ? » La libération par le capitaine, le GO de préparation, le démarrage/les transitions de l’exécution, la vérification et les revues restent des enregistrements humains en ligne.

## Qui l’utilise
- **Capitaine du remorqueur :** pour poursuivre le travail critique là où la connexion est faible et suivre l’état de synchronisation (13.03 §10).
- **Équipage :** pour faire hors ligne les accusés de réception de briefing et les entrées de quart, et pour résoudre les conflits sans supposer que son propre enregistrement l’emporte automatiquement (13.03 §6).

## Avant de commencer
- Ouvrez la page depuis une mission de remorqueur ; sans mission, la page affiche « Ouvrez cet écran depuis une mission de remorqueur pour précharger son instantané. »
- La ligne **Politique hors ligne** doit afficher une politique de stockage configurée (`CONFIGURED_UNVERIFIED`) ; sans politique, le préchargement de l’instantané et la saisie hors ligne sont indisponibles.
- Préchargez l’instantané **pendant que vous êtes en ligne**. Avant de saisir, vérifiez que le bail de l’instantané n’a pas expiré.

## Étape par étape
1. En ligne, appuyez sur **Précharger l'instantané de mission**. Le message « Instantané scellé sur cet appareil. » apparaît.
2. Dans **Instantané de mission**, vérifiez le numéro de mission, son état et **Bail jusqu'au**.
3. Si vous êtes participant d’un briefing émis, choisissez **Accuser réception** ou **Contester** (avec un motif d’au moins 10 caractères) dans **Accuser réception du briefing** et appuyez sur **Sceller et mettre en file**.
4. Pour une exécution en cours, choisissez l’exécution et le type d’observation (Note, Remorque connectée, Remorque larguée) dans **Enregistrer une observation d'exécution**, rédigez la note et appuyez sur **Sceller et mettre en file**.
5. Pour un journal quotidien en brouillon ou renvoyé, choisissez le journal dans **Entrée du journal quotidien**, rédigez le **Résumé de la journée** et appuyez sur **Sceller et mettre en file**.
6. Suivez l’état de chaque saisie dans **Saisies en file**. Les saisies sont envoyées dans l’ordre au retour de la connexion ; pour un envoi manuel, utilisez **Envoyer les saisies en file**.
7. Si une saisie affiche **Conflit important**, lisez **Modifié sur le serveur**, choisissez explicitement **Conserver l'enregistrement du serveur** ou **Appliquer ma valeur saisie sur l'enregistrement actuel** et appuyez sur **Résoudre le conflit**.

## Signification des états
- **En file sur l'appareil (non envoyé) :** la saisie est scellée sur cet appareil ; en file ne signifie pas envoyé.
- **Accepté par le serveur :** le serveur a reçu la saisie. Ce n’est ni une approbation ni une vérification.
- **Conflit important :** l’enregistrement du serveur a changé après la saisie ; il n’est jamais fusionné automatiquement et attend la décision d’une personne.
- **Valeur du serveur conservée / Refusé par le serveur :** le résultat donné par le serveur à la saisie.
- **UNKNOWN (hors ligne) :** l’état de la politique n’a pas pu être lu hors ligne ; aucune politique n’est présumée.

## Ce que vous ne pouvez pas faire / prudence
- Uniquement en ligne : libération par le capitaine, GO de préparation, démarrage et transitions de l’exécution, émission du briefing, vérification, soumission et revue du journal quotidien.
- Les conflits importants ne sont jamais fusionnés automatiquement ; la résolution est toujours un choix humain explicite (13.03 §1).
- Aucune nouvelle saisie ne peut être faite avec un bail d’instantané expiré ; préchargez un nouvel instantané.
- Lors de la relecture, chaque saisie est traitée une seule fois avec son identifiant d’origine ; ne recréez pas la même saisie.

## En cas de problème
- **Message « La clé d’appareil de cette file est indisponible. » :** les enregistrements saisis sont conservés mais ne peuvent pas être lus ; ne les supprimez pas et contactez le support.
- **Message « Résultat inconnu » :** la même saisie sera réessayée avec son identifiant d’origine ; n’en créez pas de nouvelle.
- **Message « La politique hors ligne n’autorise pas d’instantané. » :** contactez votre administrateur de locataire au sujet de la configuration de la politique (13.03 §10).
- **Conflit important :** utilisez le formulaire **Résoudre le conflit** sur cette page ; les conflits sont aussi listés dans le **Centre des conflits importants** (13.03 §10).

## Pages associées
