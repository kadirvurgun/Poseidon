---
key: certificate-command-center
title: Centre des certificats
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le Centre des certificats liste les **déclarations** de certificats du navire sélectionné avec leurs états calendaires déclarés. Il répond à la question : « Quels certificats de ce navire approchent de leur expiration ou d’une fenêtre de visite, et quels enregistrements attendent encore un examen humain ? » Les dates affichées sont des déclarations. La page ne décide jamais qu’un certificat est valide ou vérifié (13.03 §1, §8).

## Qui l’utilise
- **Capitaine, Chef mécanicien / Surintendant technique :** suivent le calendrier des certificats du navire, saisissent de nouveaux brouillons de certificat et lancent la préparation du renouvellement.
- **Utilisateur Réglementaire / Assurance :** vérifie que les déclarations reposent sur une source officielle et versionnée et enregistre la déclaration d’un Auditeur désigné (13.03 §8).
- Les utilisateurs disposant uniquement d’un accès en lecture voient la liste mais ne peuvent pas la modifier.

## Avant de commencer
- La consultation requiert la lecture des certificats. La saisie de brouillons requiert l’écriture des certificats et un périmètre de modification sur ce navire ; l’extraction de suggestions depuis un document requiert la lecture des fichiers et l’extraction ; l’enregistrement d’une déclaration humaine requiert l’écriture dans l’annuaire, l’autorisation d’enregistrement de vérification et une désignation en cours d’**Auditor** pour ce navire et ce certificat. Les déclarations d’équipage protégées requièrent en outre la lecture de l’équipage, la lecture des certificats d’équipage, la lecture de l’identité de l’équipage et la lecture des fichiers.
- Choisissez d’abord le bon navire : appuyez dessus dans la liste **Sélectionner un navire** (13.03 §1).
- La page requiert une lecture en ligne. Hors ligne, vous voyez « Current certificate sources unavailable offline or hidden. » et aucun droit hors ligne n’est déduit.

## Étape par étape
1. Ouvrez le navire depuis la liste **Sélectionner un navire**.
2. Utilisez **Périmètre des certificats** pour choisir **Certificats du navire** ou, si vous disposez de l’autorisation, **Déclarations d’équipage protégées**. Affinez la liste avec **Filtre du calendrier déclaré**.
3. Ouvrez un enregistrement en appuyant sur sa ligne. Les lignes portent le libellé **Déclaration de certificat non vérifiée**.
4. Pour un nouvel enregistrement, appuyez sur **Nouveau brouillon de certificat**. Renseignez **Nom du document**, **Base réglementaire déclarée**, **Pavillon déclaré**, la fenêtre de visite et **Disponibilité déclarée** à partir du document source, puis appuyez sur **Enregistrer le brouillon non vérifié**.
5. Pour obtenir des suggestions à partir du document lié, appuyez sur **Extraire depuis le document lié** dans **Examiner les faits du document**. Comparez chaque suggestion avec la source, cochez **J’ai vérifié ce fait dans la source** et **J’ai vérifié que ce document appartient à ce navire et à ce certificat**, puis appuyez sur **Confirmer les faits sélectionnés et enregistrer le brouillon**.
6. En tant qu’Auditeur désigné, dans **Enregistrement d’une déclaration humaine**, renseignez **Sélectionner l’affectation réelle** et **Sélectionner l’affirmation enregistrée**, saisissez le motif, puis appuyez sur **Enregistrer une déclaration non évaluée**.
7. Pour lancer un renouvellement, utilisez **Préparer le renouvellement** sous **Travaux liés au certificat** dans l’enregistrement. Vous accédez alors au parcours [Renouvellement de certificat](/app/renewal-workflow).

## Signification des états
- **Declared expiry <90 days / <60 days / <30 days / Declared expiry passed :** une observation calendaire tirée de la date d’expiration déclarée. Ce n’est pas une décision sur la validité.
- **Declared survey window open / Beyond due window :** la position par rapport à la fenêtre de visite déclarée.
- **Examen du calendrier requis :** une date est manquante, ambiguë ou contradictoire. L’enregistrement n’est pas prêt tant qu’une personne ne l’a pas comparé au document source.
- **Inconnu :** la valeur n’a pas été déclarée. Une valeur inconnue ne signifie jamais « non manquant » ou « conforme » ; elle doit être examinée.
- **Low confidence · review / Ambiguous · review / Revue requise :** une suggestion issue du document qui n’a pas encore été confirmée par une personne. Un score de confiance n’est pas une vérification.
- « Cette page uniquement. Les observations calendaires sont distinctes de la qualification ; il ne s’agit pas de totaux de disponibilité à l’échelle de la flotte. »

## Ce que vous ne pouvez pas faire / prudence
- Enregistrer un brouillon ne vérifie pas le certificat. L’écriture de l’enregistrement, la confirmation des suggestions du document et la déclaration de l’Auditeur sont des étapes distinctes ; Terminé ≠ Vérifié.
- L’extraction automatique ne fait que suggérer. Les valeurs manquantes restent inconnues et seuls les faits que vous sélectionnez et corrigez mettent à jour le brouillon.
- Un rôle d’administrateur seul ne permet pas d’enregistrer une déclaration humaine ; une désignation en cours et le fichier source sont requis.
- Un certificat enregistré n’est jamais modifié silencieusement. Chaque enregistrement crée une nouvelle révision et les révisions antérieures restent dans **Historique des révisions enregistrées** (13.03 §1).
- La page n’accorde aucune approbation de la classe, du pavillon ou d’une autorité et ne décide pas de l’aptitude au voyage. Cette décision appartient aux personnes autorisées.

## En cas de problème
- **Source de certificat ou accès indisponible. Aucun inventaire valide, vide ou vérifié n’est déduit.** Appuyez sur **Réessayer la lecture de la source**. Si le problème persiste, vérifiez votre rôle, votre navire et votre périmètre d’objets ; contactez le Tenant Admin si nécessaire (13.03 §10).
- **Outcome unknown.** Conservez la commande d’origine et renvoyez la même requête avec **Réessayer la commande de certificat exacte**.
- **Current source changed.** Vos champs locaux sont conservés ; utilisez **Retour aux enregistrements**, relisez l’enregistrement actuel et refaites votre modification.
- **Automatic reading is not available for this document :** saisissez les valeurs manuellement à partir du document source.
- Si un enregistrement ne peut pas être vérifié, contrôlez sa preuve et la lignée de sa source et transmettez-le au vérificateur (13.03 §10).

## Pages associées
