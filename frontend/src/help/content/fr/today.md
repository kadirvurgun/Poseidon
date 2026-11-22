---
key: today
title: Aujourd’hui
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## À quoi sert cette page
« Aujourd’hui » rassemble en une seule liste, pour le navire ou le remorqueur sélectionné, les **alertes qui demandent votre attention maintenant**. La page répond à la question : « Quel enregistrement de ce navire attend aujourd’hui une décision ou une vérification, et quelle source l’indique ? » La page ne décide rien ; elle affiche l’alerte, sa source et l’étape suivante.

## Qui l’utilise
- **Capitaine** et **Capitaine du remorqueur :** pour voir les exceptions du jour, leur responsable et l’étape suivante (13.03 §2, §3).
- **Chef mécanicien :** pour suivre les alertes du plan de maintenance (13.03 §5).
- **Crew / OOW :** pour voir les travaux qui leur incombent (13.03 §6).

## Avant de commencer
- Dans la barre de contexte en haut, vérifiez que la **bonne société et le bon navire** sont sélectionnés (13.03 §1). Une alerte affichée appartient au navire sélectionné.
- Vous avez besoin d’un accès en lecture à l’espace de travail pour ouvrir cette page.
- Si aucun navire n’est sélectionné, la page affiche d’abord la liste **Sélectionner un navire**.

## Étape par étape
1. Sélectionnez le navire ou le remorqueur sur lequel vous travaillez. Le navire sélectionné apparaît comme **Navire actuel** ; utilisez **Changer de navire** pour le modifier.
2. Regardez la section **Couverture des sources** en haut. Elle indique quelles sources (réglementaire, maintenance planifiée, opérations de remorquage) ont pu être lues.
3. Lisez les cartes d’alerte de haut en bas. Chaque carte provient d’une source et propose une étape suivante (par exemple **Examiner le brouillon**, **Examiner le plan de maintenance**, **Voir la mission de remorquage**).
4. Appuyez sur **Pourquoi ?** pour voir la raison d’une carte. La section affiche la règle, l’enregistrement source, sa révision, l’heure de lecture et toutes les entrées inconnues.
5. Suivez le lien de la carte vers la page propre de l’enregistrement et agissez-y, dans la limite de vos autorisations.
6. Utilisez **Actualiser** pour relire les données.

## Signification des états
- **Disponible (non vérifié) :** la source a été lue. Cela ne signifie pas que son contenu a été vérifié.
- **Manquant / Indisponible dans ce contexte :** la source n’a pas pu être lue pour ce navire. Une alerte que vous ne voyez pas ne signifie pas que tout va bien.
- **Vue partielle :** certaines sources n’ont pas pu être lues. Un compte de zéro ne signifie pas « aucun problème ».
- **Toutes les sources listées ont été lues et n’ont renvoyé aucun élément :** aucune alerte n’est revenue. Ce n’est pas une autorisation de sécurité.
- **Inconnu :** la valeur manque dans la source ou n’a pas pu être déterminée. Inconnu ne signifie ni « Non », ni « Prêt », ni « Sans objet ».
- **Examen requis / Attention de planification / Avis :** la priorité de la carte. Une carte « examen requis » attend qu’une personne autorisée l’examine.
- **Terminé ≠ Vérifié :** terminer un enregistrement ne signifie pas qu’un vérificateur autorisé l’a confirmé (13.03 §1).

## Ce que vous ne pouvez pas faire / prudence
- Cette page ne prend aucune décision de départ, d’opération ou de sécurité. Cette décision appartient au Capitaine ou au Capitaine du remorqueur autorisé.
- Aucune commande ne s’exécute depuis cette page ; les actions ont lieu sur la page propre de l’enregistrement.
- Les données affichées hors ligne sont anciennes et non vérifiées. Les détails de **Pourquoi ?** ne s’ouvrent pas hors ligne.
- Une heure de lecture ou d’enregistrement ne prouve pas que la source est correcte.

## En cas de problème
- **Hors ligne :** poursuivez votre travail critique et surveillez l’état de synchronisation. Si un conflit apparaît, allez au centre des conflits (13.03 §10).
- **Accès refusé / Navire indisponible dans votre périmètre :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire.
- **Alerte inattendue :** ouvrez **Pourquoi ?** ; si la règle ou la source semble erronée, transmettez-la au responsable du contrôle ou à un expert du domaine.
- **Source indisponible / Trop de requêtes :** attendez un instant et utilisez **Réessayer**.
- **La réponse du serveur a échoué à la validation :** rien de cette réponse n’est affiché ; signalez-le au support avec une capture d’écran, l’heure et vos étapes.

## Pages associées
