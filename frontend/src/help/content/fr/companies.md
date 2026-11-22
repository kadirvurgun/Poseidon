---
key: companies
title: Sociétés
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## À quoi sert cette page
La page Sociétés liste les **fiches de société** de votre espace de travail que vous êtes autorisé à voir. Elle répond à la question : « Quelles sociétés sont définies dans cet espace de travail, et quels sont leur code et leur nom ? » Les fiches de navires et de remorqueurs appartiennent à une société ; choisir la bonne société est donc la première étape du choix de votre contexte.

## Qui l’utilise
- **Tenant Admin :** pour créer et modifier les fiches de société selon le principe du moindre privilège (13.03 §9).
- **Opérations à terre :** pour trouver le bon contexte de société.

## Avant de commencer
- Vous avez besoin de l’autorisation de lecture des sociétés pour voir cette page. Sans elle, le message « Vous n’avez pas accès à cet espace de travail. » s’affiche.
- La création d’une fiche requiert l’autorisation de création. Votre possibilité de modifier une fiche dépend de l’autorisation que le serveur accorde pour cette fiche.

## Étape par étape
1. Consultez le code et le nom de chaque société dans la liste. Utilisez **Précédent** / **Suivant** pour les autres pages.
2. Pour rechercher, saisissez votre texte dans **Rechercher par code ou nom** et appuyez sur **Rechercher**.
3. Sélectionnez le nom d’une société pour ouvrir la section **Détails**.
4. Pour une nouvelle société, appuyez sur **Nouvel enregistrement**, saisissez le code et le nom, puis appuyez sur **Enregistrer**. Tous les champs sont obligatoires.
5. Pour modifier une fiche existante, utilisez **Modifier** dans les détails.
6. Ouvrez **Afficher la fiche de l’entité juridique** pour voir les informations de l’entité juridique ; cette section est en lecture seule.

## Signification des états
- **No records found :** aucune fiche n’a été renvoyée pour votre recherche ou votre périmètre d’autorisation.
- **Legal entity: Draft – incomplete, not active / Active / Inactive :** l’état de la fiche de l’entité juridique. Brouillon signifie que des valeurs manquent encore avant l’activation.
- **Inconnu :** la valeur ne figure pas dans la fiche. Inconnu ne signifie ni « Non » ni « valide ».
- **Saved :** la fiche a été écrite sur le serveur. Cette fiche ne crée aucune approbation opérationnelle.

## Ce que vous ne pouvez pas faire / prudence
- Créer ou modifier une fiche de société n’est pas une approbation opérationnelle, de conformité ou de sécurité.
- Les valeurs de l’entité juridique ne sont pas modifiées depuis cette page ; elles ne changent que par des commandes RH auditées. Les identifiants de registre sont affichés masqués.
- Un même code ne peut pas être utilisé par deux sociétés.
- Si vous quittez la page avec un brouillon non enregistré, il vous est demandé s’il faut l’abandonner ou poursuivre la modification.

## En cas de problème
- **This record changed in another operation :** votre brouillon est conservé. Abandonnez le brouillon pour charger la fiche actuelle, puis refaites votre modification.
- **This code is already in use :** saisissez un autre code.
- **The outcome could not be confirmed :** les champs restent verrouillés tant que le résultat n’est pas connu ; relancez sans risque la même commande.
- **Your permission may have changed / No access :** vérifiez votre rôle et votre contexte d’espace de travail ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **The service is unavailable :** utilisez « Réessayer » ; si le problème persiste, signalez-le avec la référence de support.

## Pages associées
