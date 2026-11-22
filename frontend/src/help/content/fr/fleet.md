---
key: fleet
title: Flotte
roles: [Master, Tug Master, Chief Engineer, Shore Operations, Tenant Admin]
permissions: [directory.read, directory.write]
related: [today, companies, certificate-command-center, data-trust-center, departure-signoff]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## À quoi sert cette page
« Flotte » est la **liste des navires et des remorqueurs de votre périmètre**. Elle répond à la question : « À quels navires ai-je accès, et où se trouvent la fiche d’un navire, son profil réglementaire, son passeport de conformité et ses pièces jointes ? » La page affiche la fiche du navire et les informations enregistrées ; l’aptitude opérationnelle est évaluée séparément.

## Qui l’utilise
- **Capitaine / Capitaine du remorqueur / Chef mécanicien :** pour sélectionner leur propre navire et accéder à ses espaces de travail (équipements et maintenance, rôle d’équipage) (13.03 §2, §3, §5).
- **Shore Operations :** pour trouver les navires de la flotte et examiner leurs fiches.
- **Tenant Admin :** pour créer et modifier les fiches de navires (13.03 §9).

## Avant de commencer
- Dans la barre de contexte supérieure, vérifiez que la **bonne société** est sélectionnée (13.03 §1). La liste n’affiche que les navires de votre périmètre.
- Vous avez besoin d’un accès en lecture pour voir la liste. « Nouvel enregistrement » et « Modifier » n’apparaissent que pour les navires auxquels vous avez un accès en écriture.

## Étape par étape
1. Dans les cartes de synthèse en haut, consultez le nombre de navires listés, de navires, de remorqueurs et de fiches modifiables sur cette page. **Les décomptes concernent uniquement cette page**.
2. Saisissez votre texte dans **Rechercher par code ou nom**, choisissez si nécessaire « Navire » ou « Remorqueur » dans **Type de navire**, puis appuyez sur **Rechercher**.
3. Appuyez sur le nom d’un navire dans le tableau. **Détails** affiche son code, son nom et son type ; le navire sélectionné apparaît comme « Navire actuel ».
4. Ouvrez le profil du navire avec **Profil réglementaire**, ou ses données d’intégration avec **Intégration du navire pilote**.
5. Utilisez **Passeport numérique de conformité** pour examiner les exigences enregistrées, les sources, l’examen d’applicabilité et les points bloquants non résolus. Utilisez **Retour au navire** pour revenir.
6. Accédez aux espaces de travail du navire avec **Import contrôlé**, **Équipements et maintenance** ou **Rôle d’équipage**.
7. Dans la section inférieure, basculez entre **Vessel attachments** et **Brouillons documentaires**.
8. Si vous disposez de l’accès, ajoutez un navire avec **Nouvel enregistrement** ou modifiez-le avec **Modifier** et enregistrez. Utilisez **Changer de navire** pour un autre navire.

## Signification des états
- **Read-only / Editable records :** votre accès actuel sur cette page. « Modifier » n’est pas affiché sur une fiche en lecture seule.
- **Cette page uniquement. L’aptitude opérationnelle est évaluée séparément. :** le graphique de composition des navires ne donne aucune information de disponibilité.
- **Examen requis · fraîcheur non vérifiée :** les informations du passeport sont lues à partir des sources enregistrées et ne sont pas vérifiées. Le passeport n’est pas une approbation de conformité.
- **No current requirement rows returned :** cela n’établit pas un ensemble d’obligations vide.
- **Unknown / Unavailable :** la valeur est absente de la source ou n’a pas pu être lue. Cela ne signifie ni « Conforme » ni « Prêt ».

## Ce que vous ne pouvez pas faire / prudence
- Cette page n’indique jamais qu’un navire est prêt pour l’exploitation ou le départ. La décision de départ appartient au capitaine et la décision de remorquage au capitaine du remorqueur.
- Un administrateur générique ne peut pas remplacer l’admission qualifiée d’une source ni une décision humaine autorisée.
- Si vous quittez la page avec des modifications non enregistrées, une confirmation vous est demandée ; les modifications sont conservées jusqu’à ce que vous enregistriez ou quittiez explicitement la page.
- Les données affichées hors ligne ne sont pas vérifiées ; reconnectez-vous et réessayez.

## En cas de problème
- **Vous n’avez pas accès à cet espace de travail :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Aucun enregistrement trouvé :** modifiez votre recherche ou le filtre de type de navire.
- **Connectez-vous pour continuer :** votre session a pris fin ; utilisez de nouveau « Se connecter ».
- **Le service est indisponible :** utilisez « Réessayer » ; si le problème persiste, signalez-le au support avec une capture d’écran, l’heure et les étapes effectuées.

## Pages associées
