---
key: towage-risk-briefing
title: Risques et briefing de remorquage
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Cette page prépare et émet l’**enregistrement des risques et du briefing de remorquage** pour une mission de remorqueur soumise et recueille les **accusés de réception** des participants. Elle répond à la question : « Quelles conditions, quels dangers, quelles barrières préventives et d’atténuation, quelle autorité d’arrêt des travaux et quelles dispositions d’urgence ont été présentés lors du briefing, quel est l’état de l’évaluation des risques liée, et chaque participant a-t-il accusé réception de la révision émise ? » L’émission du briefing est un enregistrement humain du Commandant ou du Capitaine du remorqueur désigné ; ce n’est ni une décision GO ni un permis. Un briefing incomplet bloque READY.

## Qui l’utilise
- **Capitaine du remorqueur / Commandant :** pour préparer et émettre le briefing et s’assurer qu’aucune décision READY n’est prise avant que les risques et le briefing soient complets (13.03 §3).
- **Équipage et autres participants (pilote, officier, équipage de pont, mécanicien) :** pour accuser réception du briefing émis ou le contester dans le bon contexte de rôle (13.03 §6).
- **Coordinateur des opérations de remorquage :** pour suivre l’état du briefing et des accusés de réception (13.03 §7).

## Avant de commencer
- Ouvrez le remorqueur depuis **Opérations de remorqueur** et choisissez la mission dans **Sélectionner une mission de remorqueur soumise**.
- Le **Plan de remorquage** de la mission doit être approuvé et l’évaluation des risques liée approuvée ; sinon le briefing ne peut pas être émis.
- L’émission exige une **Désignation** (Capitaine du remorqueur ou Commandant) enregistrée sur le serveur pour le remorqueur coordinateur. L’émission exige aussi un accès en lecture à l’évaluation des risques et en écriture remorqueur sur le remorqueur coordinateur. L’accusé de réception est réservé aux personnes figurant dans la liste des participants.

## Étape par étape
1. Sur la ligne du haut, lisez **État**, **Révision**, **Accusé de réception**, **État du risque** et **État du plan**.
2. Dans **Donnée d'entrée de préparation (un briefing incomplet bloque READY)**, consultez les résultats et les codes de motif envoyés au contrôle de préparation.
3. Remplissez **Conditions confirmées lors du briefing** (météo, courant, visibilité), **Dangers**, **Barrières préventives**, **Barrières d'atténuation**, **Autorité d'arrêt des travaux** et **Dispositions d'urgence**.
4. Saisissez l’**Identifiant de l'évaluation des risques liée** ; dans **Participants**, utilisez **Ajouter** pour saisir l’**Identifiant de la personne** et le **Rôle** de chaque participant.
5. Enregistrez avec **Créer un brouillon** ou **Enregistrer le brouillon** ; lorsque **Éléments manquants avant soumission** est vide, choisissez une **Désignation** et appuyez sur **Émettre le briefing**.
6. En tant que participant, lisez la révision émise et appuyez sur **Accuser réception**. Si vous n’êtes pas d’accord, rédigez un **Motif** d’au moins 10 caractères et appuyez sur **Contester**.
7. Si le briefing doit changer, rédigez un motif et utilisez **Modifier**. Les listes **Accusés de réception** et **Révisions** montrent l’historique.

## Signification des états
- **Brouillon / Émis :** l’état du briefing. Émis signifie que les accusés de réception peuvent commencer ; à lui seul, ce n’est pas un résultat de préparation.
- **Non émis / En attente / Terminé / Contesté :** l’état des accusés de réception des participants. Il ne devient **Terminé** que lorsque chaque participant a accusé réception de la révision émise.
- **Approuvé et accusé / Attention / Bloqué / Examen requis :** l’état de l’évaluation des risques liée.
- **Réussi / Échec / Inconnu :** le résultat du contrôle envoyé au contrôle de préparation. Un briefing manquant ou non émis et des accusés de réception en attente constituent un **Échec** qui bloque READY.

## Ce que vous ne pouvez pas faire / prudence
- Aucune décision READY n’est prise avant que les risques et le briefing soient complets et que les accusés de réception des participants soient enregistrés (13.03 §3).
- Une contestation fait passer l’état des accusés de réception à **Contesté** et envoie un Échec au contrôle de préparation, de sorte que READY et GO sont bloqués. Le motif apparaît dans **Accusés de réception**. Un accusé de réception ne peut pas être modifié pour la même révision ; pour lever une contestation, le briefing est modifié (nouvelle révision), émis à nouveau et chaque participant en accuse réception à nouveau.
- Si le plan ou l’évaluation des risques change après le briefing, le briefing doit être réexaminé ; les accusés de réception antérieurs ne comptent pas pour une nouvelle révision.
- Personne n’accuse réception au nom d’une autre personne ; chaque participant enregistre son propre accusé de réception (13.03 §6).
- Aucune commande n’est envoyée depuis cette page hors ligne. Un accusé de réception peut être mis en file d’attente dans **Opérations hors ligne du remorqueur**.

## En cas de problème
- **Message « Des sections obligatoires manquent. » :** complétez les codes manquants ; vérifiez que le plan et l’évaluation des risques sont approuvés.
- **Message « L’enregistrement a changé » :** utilisez **Recharger** pour examiner le briefing actuel.
- **Message « Le résultat est inconnu » :** **Réessayer** renvoie la même requête.
- **Aucune autorisation :** vérifiez votre rôle, votre remorqueur et votre contexte de désignation ; contactez votre administrateur de locataire si nécessaire (13.03 §10).

## Pages associées
