---
key: pilot-program
title: Programme pilote : semaines et rapport de valeur
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Programme pilote regroupe en un seul endroit les semaines pilotes d’un navire (et éventuellement d’un remorqueur), les points de contrôle de chaque semaine, les problèmes du pilote, les retours quotidiens et le rapport de valeur. Elle répond à la question : « Les points de chaque semaine pilote ont-ils été satisfaits par des enregistrements réels, et comment les valeurs mesurées ont-elles évolué par rapport à la référence ? » L’avis en haut de page s’applique : « Le logiciel ne peut pas terminer un pilote. Un point de contrôle n’est satisfait que par un lien que vous créez vers un enregistrement réel vérifié par le serveur ; une référence manquante reste Inconnu ; seul le Product Owner désigné clôture une semaine ou enregistre la décision du programme. »

## Qui l’utilise
- **Product Owner :** clôture les semaines et enregistre la décision du programme (Continuer, Corriger, Arrêter, Étendre). Cela nécessite une désignation Product Owner en cours.
- **Équipe pilote :** lie des enregistrements aux points de contrôle, signale des problèmes, enregistre retours et mesures.
- **Responsable du tri :** trie et résout les problèmes.
- 13.03 ne définit aucun rôle distinct pour cette page ; les principes d’administration du locataire s’appliquent (13.03 §9).

## Avant de commencer
- La consultation nécessite la lecture à l’échelle du locataire et la lecture du programme pilote. La création de programmes, l’enregistrement, le tri et la décision nécessitent chacun leur propre permission ; les clôtures de semaine et les décisions nécessitent en plus une désignation Product Owner.
- Les enregistrements que vous liez (par exemple un certificat, un enregistrement d’équipage ou un ordre de travail) doivent déjà exister sur leurs propres pages.
- Dans les retours, ne saisissez jamais de noms, adresses e-mail, numéros de téléphone ou d’identité ; utilisez uniquement un pseudonyme et un rôle.
- L’enregistrement nécessite une connexion active.

## Étape par étape
1. Ouvrez un programme depuis **Programmes pilotes**. Pour un nouveau programme, remplissez **Nom du programme**, **Navire**, **Remorqueur (facultatif)**, **Date de début de la semaine 1** et **Fuseau horaire du calendrier (IANA)** dans **Créer un programme pilote** et appuyez sur **Enregistrer**.
2. Dans chaque section **Semaine**, examinez les colonnes **Point de contrôle**, **Critère (source)**, **Domaine**, **État** et **Enregistrements liés**.
3. Pour satisfaire un point, saisissez le **Point de contrôle**, le **Type d’enregistrement** et l’**Identifiant de l’enregistrement** dans **Lier un enregistrement** et appuyez sur **Lier un enregistrement**. Supprimez un lien erroné avec **Retirer le lien** après avoir rédigé un **Motif du retrait**.
4. Dans **Problèmes du pilote**, utilisez **Signaler un problème** pour saisir le domaine, la **Catégorie**, la gravité et le **Résumé du problème**. Le responsable du tri utilise **Tri** avec une **Note de tri**, puis résout le problème.
5. Dans **Retour quotidien**, utilisez **Enregistrer un retour** pour saisir un **Résumé** et un **Ressenti**.
6. Dans **Rapport de valeur**, utilisez **Enregistrer une mesure** pour saisir l’**Indicateur**, la **Phase** (Référence ou Période pilote), la **Valeur**, la **Taille de l’échantillon**, **Période du**, **Période au**, la **Référence de preuve** et **Méthode et échantillon**.
7. Si vous êtes le Product Owner, enregistrez le résultat et la justification dans **Clôturer la semaine**. À la fin du programme, remplissez la décision, la **Désignation** et la **Justification** dans **Enregistrer la décision du programme** et appuyez sur **Enregistrer**.

## Signification des états
- **Lié et vérifié :** un enregistrement réel vérifié par le serveur est lié au point. Les points **Lien plus valide** et **Manquant** ne sont pas satisfaits.
- **Completion required :** l’enregistrement lié doit être dans son état terminé. « La trace d’audit prouve qu’une personne a écrit l’enregistrement ; elle ne prouve pas que l’enregistrement ou le flux est terminé. »
- **Ready for human close / Not ready :** une recommandation pour la semaine ; seul le Product Owner la clôture comme **Acceptée** ou **Non acceptée**.
- **Triaged same day / Triaged late / Triage due today / Overdue :** le délai de tri des problèmes P0 par rapport au jour calendaire du programme. Pour les problèmes avec **Pas d'objectif « jour même » dans la source**, le système n’invente aucune échéance.
- **Measured / Inconnu :** lorsqu’une valeur de référence ou de pilote manque, le rapport reste **Inconnu** ; il n’est jamais compté comme zéro ni comme une amélioration. Les valeurs **Non comparable** ne sont pas comparées.
- **Télémétrie (déclarée, non vérifiée) :** une valeur de télémétrie déclarée.
- **Expand eligible; human decision pending / Fix recommended :** une recommandation du programme, pas une décision.

## Ce que vous ne pouvez pas faire / prudence
- « Accepté n’est possible que lorsque chaque point est vérifié et qu’aucun tri P0 n’est ouvert, en attente ou en retard. »
- « Étendre n’est possible que lorsque chaque semaine est acceptée et que les valeurs, l’UAT, les défauts et les P0 sont dégagés. »
- Si vous êtes l’auteur de preuves sur lesquelles repose une semaine ou une décision (liens, retours, mesures), vous ne pouvez pas clôturer cette semaine ni enregistrer cette décision.
- Aucune clôture de semaine ni décision ne peut être enregistrée pour un programme fictif de démonstration.
- Le système ne termine jamais un pilote, ne clôture jamais une semaine et ne fabrique jamais de décisions ni de validations.

## En cas de problème
- **Vous n'avez pas de désignation de Product Owner en cours ; clôtures et décisions sont en lecture seule.** Contactez l’administrateur du locataire pour la désignation (13.03 §10).
- **La saisie des retours est indisponible tant qu'un opérateur n'a pas configuré la politique de conservation.** Demandez à l’administrateur système de configurer la politique de conservation.
- **L’enregistrement a changé ou a déjà été utilisé. Rechargez.** Ouvrez l’état actuel avec **Recharger**.
- **Le résultat est incertain. Réessayer envoie la même commande.** Utilisez **Réessayer** pour renvoyer la même commande en toute sécurité.
- **Refusé par les règles.** Par exemple, l’enregistrement lié peut ne pas exister, être hors périmètre ou ne pas être terminé.
- **Non autorisé.** Vous êtes l’auteur de liens, retours ou mesures sur lesquels repose cette clôture de semaine ou cette décision ; une autre personne doit l’enregistrer.
- Une semaine qui n’a pas commencé ne peut pas être clôturée (affiché comme « L’enregistrement a changé ou a déjà été utilisé. Rechargez. »).
- **Vous n’avez pas accès à ces enregistrements.** Vérifiez votre rôle et le périmètre du locataire (13.03 §10).

## Pages associées
