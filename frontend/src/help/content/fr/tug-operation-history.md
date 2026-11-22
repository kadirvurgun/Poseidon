---
key: tug-operation-history
title: Historique des remorqueurs
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## À quoi sert cette page
L’historique des opérations affiche l’**historique en lecture seule** des missions de remorqueur, des exécutions, des journaux quotidiens et des preuves. Il répond à la question : « Quelles missions se sont terminées avec quel résultat, les exécutions ont-elles été vérifiées, existe-t-il un enregistrement de preuve et quels journaux quotidiens appartiennent à ces missions ? » Les enregistrements annulés et interrompus restent visibles ; rien n’est supprimé ni réétiqueté.

## Qui l’utilise
- **Coordinateur des opérations de remorquage / Opérations à terre :** pour suivre l’exhaustivité des preuves, les événements et l’historique des missions achevées (13.03 §7).
- **Capitaine du remorqueur :** pour voir les missions passées et les chronologies d’exécution de son propre remorqueur (13.03 §3).

## Avant de commencer
- Ouvrez le remorqueur depuis **Opérations de remorqueur** ; sans remorqueur sélectionné, la page affiche « Sélectionnez un remorqueur pour continuer. »
- L’accès en lecture remorqueur suffit ; la page ne modifie aucune donnée.
- L’historique est lu depuis le serveur ; il ne peut pas être actualisé hors ligne.

## Étape par étape
1. Dans **Filtres**, définissez éventuellement **ID du remorqueur**, **Du (date UTC)**, **Au (date UTC)** et **Résultat**, puis appuyez sur **Appliquer les filtres**. La plage de dates est d’au plus 400 jours.
2. Sur chaque ligne du tableau, lisez **Mission** (numéro de mission, navire assisté, type de mission, état du plan et du briefing), **Heure d'activité**, **Résultat** et **Preuves**.
3. Dans **Exécutions**, consultez pour chaque exécution l’état, l’état de vérification, la durée, l’enregistrement du capitaine et le nombre d’entrées de preuve ; utilisez **Ouvrir la chronologie d'exécution** pour aller à la console d’exécution.
4. Utilisez les liens de **Journaux quotidiens** pour ouvrir le journal correspondant. La mention « non lié par le journal » indique que le journal ne lie pas cette exécution.
5. Utilisez **Charger plus** pour les enregistrements plus anciens. **Lu le** indique quand les données ont été lues.

## Signification des états
- **Non démarrée / En cours / Achevée (enregistrée) / Interrompue / Annulée / Mixte :** le résultat global de la mission. Achevée (enregistrée) ne signifie pas que la mission a été vérifiée.
- **Vérifiée, preuve enregistrée / Vérifiée, aucune entrée de preuve / Vérification rejetée / Preuve non vérifiée / Inconnu :** l’état des preuves et de la vérification.
- **vérification en attente / vérifiée / vérification rejetée :** l’état de vérification d’une exécution individuelle.
- **Aucun :** aucun enregistrement lié n’existe pour cette mission. Cela ne signifie pas qu’aucun enregistrement n’était nécessaire.

## Ce que vous ne pouvez pas faire / prudence
- Aucun enregistrement n’est modifié, supprimé ni vérifié sur cette page.
- Aucune suppression silencieuse : les missions annulées et les exécutions interrompues ou rejetées restent dans l’historique.
- Achevé ≠ Vérifié : la vérification est l’enregistrement d’une autre personne.
- Les données affichées représentent l’état au moment de **Lu le** ; elles ne sont pas actualisées hors ligne.

## En cas de problème
- **Message « Le filtre n’est pas valide » :** vérifiez que l’ID du remorqueur est un UUID et que la plage de dates ne dépasse pas 400 jours.
- **Message « L’historique n’a pas pu être chargé. » :** vérifiez votre connexion et appuyez sur **Réessayer**.
- **« Vous n’avez pas accès à ce périmètre de remorqueur. » :** vérifiez votre rôle et votre contexte de remorqueur ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **L’enregistrement ne peut pas être vérifié :** contrôlez la référence de preuve et sa source dans la chronologie d’exécution ; contactez le vérificateur (13.03 §10).

## Pages associées
