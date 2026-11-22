---
key: tug-daily-log
title: Journal quotidien du remorqueur
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le journal quotidien du remorqueur enregistre, pour une date et un quart, les **missions, compteurs horaires des moteurs, carburant et défauts**, et les compare aux remorquages exécutés dans la journée pour le **rapprochement**. Il répond à la question : « Combien de missions ont été achevées pendant ce jour/quart, les heures des compteurs correspondent-elles aux durées d’exécution, combien de carburant a été consommé et quels défauts ont été signalés ? » Le rapprochement est consultatif ; la revue du capitaine est un enregistrement humain distinct.

## Qui l’utilise
- **Équipage / officier de quart :** pour tenir le registre du quart en temps réel et dans l’ordre (13.03 §6).
- **Capitaine de remorqueur / Capitaine :** pour examiner un journal soumis et le vérifier ou le renvoyer (13.03 §3).

## Avant de commencer
- Ouvrez le remorqueur depuis **Opérations de remorqueur**.
- La création et la modification d’un journal exigent un accès en écriture remorqueur.
- La revue exige une **Désignation** (Capitaine de remorqueur ou Capitaine) enregistrée sur le serveur pour ce remorqueur. La personne qui a rédigé ou soumis le journal ne peut pas l’examiner.

## Étape par étape
1. Ouvrez un journal existant dans la liste, ou choisissez une **Date du journal** et un **Quart** et appuyez sur **Créer un journal quotidien**.
2. Saisissez le **Fuseau horaire (IANA)**.
3. Dans **Compteurs horaires des moteurs**, saisissez pour chaque **Compteur** le **Relevé d'ouverture**, le **Relevé de clôture** et, si nécessaire, la **Preuve de remise à zéro ou de dépassement de capacité**.
4. Dans **Carburant consommé**, saisissez la valeur, l’unité et la **Référence de la source de carburant**.
5. Cochez les exécutions qui appartiennent à ce journal dans **Remorquages exécutés dans la journée** ; remplissez **Identifiants des défauts liés** et **Résumé quotidien** (résumé d’au moins 20 caractères).
6. Appuyez sur **Enregistrer**. Sur la ligne **Rapprochement**, contrôlez les missions achevées, les heures d’exécution, les heures des compteurs et les défauts signalés.
7. Lorsque la liste **Points bloquants** est vide, appuyez sur **Soumettre à la revue du capitaine**.
8. En tant que capitaine, choisissez votre **Désignation**, rédigez un motif et appuyez sur **Capitaine : vérifier le journal** ou **Capitaine : renvoyer le journal**.

## Signification des états
- **Brouillon / Soumis / Renvoyé / Vérifié :** l’état du journal.
- **Revue du capitaine — En attente / Attention / Examiné :** En attente = revue attendue ; Attention = renvoyé, ou soumis alors que le rapprochement n’est pas Rapproché ; Examiné = vérifié par le capitaine. Un journal soumis n’est pas un journal vérifié.
- **Rapproché / Examen requis / Incomplet :** le résultat consultatif de la comparaison. Les codes de motif sont listés en dessous.
- **intervalle ouvert :** une exécution est encore en cours ; les heures d’exécution ne sont pas encore définitives.
- **Événements de sécurité — Inconnu :** cette page ne compte pas les événements de sécurité ; aucune conclusion « aucun événement » n’en est tirée.
- **Synchronisé / En attente de synchronisation / Conflit / Échec :** l’état de synchronisation. En attente de synchronisation indique que la modification n’a pas encore atteint le serveur.

## Ce que vous ne pouvez pas faire / prudence
- La personne qui a rédigé le journal ou l’a soumis à la revue du capitaine ne peut pas le vérifier.
- Le résultat du rapprochement n’est pas une approbation ; c’est le capitaine qui effectue la revue.
- Achever un enregistrement et le vérifier ne sont pas la même action (13.03 §1).
- Hors ligne, une modification non enregistrée est conservée dans la mémoire de cette page et envoyée avec sa clé d’origine au retour de la connexion ; gardez la page ouverte. La soumission et la revue ont lieu en ligne.
- Si un conflit apparaît, ne supposez pas que votre propre enregistrement l’emporte automatiquement (13.03 §6).

## En cas de problème
- **Message « Le serveur a listé des éléments manquants ou bloquants. » :** levez les codes dans **Points bloquants**.
- **Message « L’enregistrement a changé ou la commande est en conflit. » :** utilisez **Recharger la dernière version** pour examiner le journal actuel ; la modification locale conservée est abandonnée.
- **Message « Le résultat est inconnu. » :** **Réessayer** renvoie la même commande à l’identique.
- **Conflit :** une modification conservée sur cette page n’est jamais fusionnée ; utilisez **Recharger la dernière version**. Les entrées quotidiennes saisies dans **Opérations hors ligne du remorqueur** qui entrent en conflit y sont résolues ; elles sont aussi listées dans le **Centre des conflits importants** (13.03 §10).

## Pages associées
