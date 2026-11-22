---
key: system-health
title: État du système
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## À quoi sert cette page
« État du système » présente aux administrateurs les **observations de service** sur un seul écran : API, service d’écriture d’audit, synchronisation, tâches de notification, service de fichiers et fraîcheur des données. Il répond à la question : « Quels signaux de service ont été observés pour le navire sélectionné et ce locataire, et lesquels ne l’ont pas été ? » La configuration et la télémétrie manquante n’établissent ni un service sain ni la disponibilité d’un navire.

## Qui l’utilise
- **Administrateur système / du locataire :** pour repérer les problèmes de service et de synchronisation et les traiter de manière contrôlée (13.03 §9).
- **Support / Ingénierie :** pour examiner les observations lorsqu’une erreur de l’application est signalée (13.03 §10).

## Avant de commencer
- La page n’est visible que pour les utilisateurs disposant de la lecture à l’échelle du locataire, de la lecture de l’historique des actions et de la lecture de l’état du système.
- Dans la barre de contexte supérieure, vérifiez que la **bonne société** est sélectionnée.
- Les observations sont demandées par navire ; vous devez d’abord sélectionner un navire.

## Étape par étape
1. Choisissez le navire dans **Périmètre d’observation du navire**. Pour une longue liste, utilisez **Navires précédents** / **Navires suivants**.
2. Vérifiez l’heure « Lu le ».
3. Dans **Disponibilité du service**, lisez l’heure de démarrage du processus et sa durée de fonctionnement.
4. Dans les cartes **API** et **Service d’écriture d’audit**, lisez les chiffres de requêtes, d’erreurs, d’éléments persistés et de latence. Ils couvrent ce locataire depuis le démarrage du processus.
5. Dans **Synchronisation**, consultez les résultats validés, les conflits non résolus et la dernière synchronisation validée.
6. Dans **Tâches de notification**, consultez les notifications ouvertes, les escalades échues et l’état de la tâche d’évaluation.
7. Lisez les cartes **Service de fichiers** et **Fraîcheur des données**.
8. Sous **Indicateurs du pilote**, examinez les observations des clients pilotes inscrits.
9. Appuyez sur **Actualiser les observations** pour obtenir des données actuelles.

## Signification des états
- **Not observed / Unavailable :** il n’existe aucune observation pour ce signal. Cela ne signifie ni « sain » ni « aucun problème ».
- **Configuré · non sondé :** le service de fichiers semble configuré, mais aucune sonde active du stockage ou de l’analyseur n’a été effectuée.
- **Within schedule / Running / Overdue / Scheduler not enabled / Run abandoned (lease expired) :** l’état de la tâche d’évaluation des notifications.
- **Conflits non résolus :** conflits hors ligne en attente d’une résolution humaine. Ils ne sont jamais fusionnés automatiquement.
- **Current / Stale / Unknown / Unavailable :** nombres d’évaluations de fraîcheur des données. Unknown ne signifie pas « Current ».
- **This snapshot is older than two minutes :** actualisez avant de vous y fier.

## Ce que vous ne pouvez pas faire / prudence
- Cette page est en lecture seule ; aucune action n’est effectuée sur les services, navires ou enregistrements.
- Les enregistrements de notification sont uniquement internes à l’application ; ils ne prouvent ni une remise ni une quelconque décision de sécurité.
- La disponibilité du service montre seulement que ce processus API a répondu ; elle ne prouve pas que les autres services fonctionnent.
- Les indicateurs du pilote sont déclarés et non vérifiés ; ils ne constituent pas une déclaration d’acceptation.
- Ne corrigez pas les données directement en production ; publiez les changements de configuration de manière contrôlée (13.03 §9).

## En cas de problème
- **L’accès à l’état du système n’est pas disponible dans ce contexte :** vérifiez votre rôle et le périmètre du locataire.
- **Observations de service indisponibles :** aucun état sain n’est déduit ; utilisez « Actualiser les observations » après un moment.
- **Il existe un conflit non résolu :** orientez l’utilisateur concerné vers le Centre des conflits (13.03 §10).
- **Erreur de l’application :** signalez-la au support/à l’ingénierie avec l’identifiant de corrélation (Correlation ID), l’écran, l’heure et vos étapes.

## Pages associées
