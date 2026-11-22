---
key: tug-job-intake
title: Réception des missions
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La réception des missions structure une nouvelle **demande de mission de remorqueur sous forme de brouillon** dans le contexte de remorqueur sélectionné. Elle répond à la question : « Pour cette mission, quels sont le navire assisté, le type de mission, la fenêtre horaire, l’emplacement et les ressources requises (nombre de remorqueurs, traction au point fixe) ? » La demande enregistrée est une déclaration en brouillon ; ce n’est ni une affectation opérationnelle, ni une éligibilité, ni un permis, ni une approbation de sécurité.

## Qui l’utilise
- **Coordinateur des opérations de remorquage / Opérations à terre :** pour structurer la demande et saisir le navire assisté, le type de mission, la fenêtre horaire et la capacité requise (13.03 §7).

## Avant de commencer
- Dans la barre de contexte en haut, sélectionnez la **bonne société et le bon remorqueur**.
- Vous avez besoin d’un accès en écriture remorqueur. Sans lui, le formulaire ne s’ouvre pas et « L’accès est indisponible. » est affiché.
- Les brouillons incomplets peuvent être enregistrés. Les données opérationnelles obligatoires et l’examen autorisé doivent être résolus avant la soumission.

## Étape par étape
1. Dans **Demande et navire assisté**, remplissez **Source de la demande**, **Référence du client**, **Navire assisté**, **IMO du navire assisté**, **Type de mission**, **Priorité** et **Responsable de la demande**. Les listes sans choix restent « Inconnu ».
2. Dans **Horaire et emplacement demandés**, saisissez **Début demandé (UTC)**, **Fin demandée (UTC, conditionnelle)**, **Zone d’opération**, **Point de prise en charge** et **Point de destination**. Les heures sont en UTC.
3. Dans **Besoins en ressources et notes**, saisissez **BP requise (tonnes, décimale exacte)**, **Nombre de remorqueurs requis**, **Exigences particulières** et **Notes**.
4. Appuyez sur **Enregistrer le brouillon**. En cas de succès, le détail de la mission s’ouvre avec « Brouillon enregistré. L’approbation opérationnelle reste indisponible. »
5. Pour abandonner vos modifications, utilisez **Abandonner les modifications**.
6. Dans le détail de la mission, utilisez **Soumettre la demande** pour enregistrer la demande pour la planification. Si des données obligatoires manquent, la soumission est bloquée et les champs manquants sont listés.

## Signification des états
- **Brouillon :** la demande est enregistrée et modifiable ; elle n’est pas encore soumise.
- **Soumise :** la demande est enregistrée pour la planification. Ce n’est ni une approbation, ni une affectation, ni une décision de préparation.
- **Annulée :** la demande est close ; elle ne peut pas être relancée.
- **Inconnu :** aucune valeur n’a été choisie pour le champ. Inconnu n’est pas une valeur par défaut et ne remplace jamais « Non » ou « Sans objet ».
- **Vérifiez cette valeur. :** le serveur a rejeté ce champ ; corrigez-le et enregistrez à nouveau.

## Ce que vous ne pouvez pas faire / prudence
- Cette page n’affecte aucun remorqueur et ne déclare jamais la mission prête. La proposition d’affectation est une étape distincte et ne prime jamais sur la décision de sécurité du Capitaine du remorqueur à bord (13.03 §7).
- Une demande ne peut pas être soumise tant que des données obligatoires manquent ; un brouillon incomplet peut toujours être enregistré.
- Les champs d’une demande soumise ne peuvent pas être modifiés depuis ce formulaire ; une demande annulée ne peut pas être relancée, créez-en une nouvelle à la place.
- Hors ligne, le brouillon n’est conservé que sur cette page et ne peut pas être enregistré.

## En cas de problème
- **Message « Résultat de l’enregistrement incertain » :** la tentative exacte est conservée ; après reconnexion, utilisez **Réessayer** pour envoyer la même commande. Abandonner les saisies locales n’annule pas une commande déjà envoyée.
- **Message « Conflit » :** votre brouillon est conservé ; abandonnez et rechargez avant une nouvelle tentative.
- **Vérifier les champs :** corrigez les champs signalés.
- **L’accès est indisponible :** vérifiez votre rôle et votre contexte de remorqueur ; contactez votre administrateur de locataire si nécessaire (13.03 §10).

## Pages associées
