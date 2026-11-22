---
key: renewal-workflow
title: Renouvellement de certificat
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Renouvellement de certificat enregistre étape par étape la préparation du renouvellement d’un certificat et son processus interne : préparation, demande, attente de l’autorité, remplacement délivré, vérification et achèvement. Elle répond à la question : « Où en est le renouvellement de ce certificat, et qui effectue la prochaine étape ? » C’est uniquement un enregistrement de processus interne ; il n’établit ni validité juridique ni approbation de l’autorité (13.03 §1, §8).

## Qui l’utilise
- **Capitaine, Chef mécanicien / Surintendant technique :** préparent le brouillon de renouvellement et enregistrent les exigences et la demande.
- **Responsable de renouvellement affecté :** fait avancer le processus. C’est un rôle tenu par les membres disposant de la permission de responsable de renouvellement ; le processus ne peut pas s’ouvrir ni être validé sans lui.
- **Vérificateur et personne qui achève :** comparent le remplacement délivré avec sa source et le vérifient, puis, en tant que personne différente, achèvent le renouvellement (13.03 §1, §8).

## Avant de commencer
- La consultation nécessite la lecture des certificats. Préparer un brouillon nécessite l’écriture des certificats et l’écriture des renouvellements sur ce navire ; vérifier nécessite la vérification des renouvellements ; achever nécessite l’achèvement des renouvellements.
- Choisissez d’abord le bon navire dans **Sélectionner un navire** (13.03 §1).
- Le certificat à renouveler doit avoir une révision enregistrée actuelle. Un certificat remplacé ne peut pas être renouvelé.
- Les étapes du processus nécessitent une connexion active. Hors ligne, vous voyez « Vous êtes hors ligne. Les étapes du processus sont indisponibles jusqu’au retour de la connexion. »

## Étape par étape
1. Ouvrez un **Enregistrement de renouvellement** depuis **Brouillons de renouvellement enregistrés**. Pour un nouveau renouvellement, utilisez **Choisir le certificat à renouveler** pour aller au [Centre de commande des certificats](/app/certificate-command-center) et appuyez sur **Préparer le renouvellement** sur l’enregistrement.
2. Remplissez le **Formulaire de renouvellement de certificat** : **Date d’échéance déclarée du renouvellement**, la fenêtre de visite, **Lieu prévu**, **Rôle affecté au renouvellement** ainsi que **Déclaration d’inspection requise**, **Déclaration de documents requis** et **Déclaration d’actions requises**. Laissez une valeur que vous ne connaissez pas à **Inconnu**.
3. Enregistrez le brouillon avec **Enregistrer le brouillon non vérifié**.
4. Dans **Processus de renouvellement**, choisissez l’étape ouverte à votre rôle dans **Étapes disponibles**, par exemple **Confirmer les exigences remplies**, **Enregistrer la demande** ou **Attendre la décision de l’autorité**. Remplissez les champs requis et appuyez sur **Enregistrer l’étape**.
5. Lorsque l’autorité délivre le nouveau certificat, utilisez **Enregistrer le remplacement délivré** et sélectionnez-le. Son propre document sert de preuve.
6. Le vérificateur utilise **Vérifier le remplacement** et renseigne **L’émetteur concorde**, **Le numéro de certificat concorde**, **L’objet concorde**, **Les dates concordent** et **Le périmètre concorde** en comparant avec la source.
7. Une personne différente achève le renouvellement avec **Clôturer et remplacer**. Si l’autorité a refusé, utilisez **Enregistrer le rejet de l’autorité** ; si la préparation est abandonnée, utilisez **Annuler la préparation**.

## Signification des états
- **Draft / Preparing :** le renouvellement est en préparation ; le formulaire peut être modifié.
- **Ready to apply / Application recorded / Awaiting authority :** l’étape de demande. La décision de l’autorité a lieu en dehors du système.
- **Délivré, non vérifié :** le remplacement est enregistré mais n’a pas encore été comparé avec sa source par une personne.
- **Vérifié par un réviseur :** le vérificateur a enregistré la comparaison ; le renouvellement n’est pas encore achevé.
- **Completed :** le processus interne est clôturé et le remplacement remplace l’original. Ce n’est pas une décision sur la validité juridique du certificat.
- **Cancelled / Rejected :** le flux s’est arrêté.
- Une exigence peut être **Complete / Non requis / Inconnu**. **Inconnu** ne signifie pas « Non requis » ; le serveur ne fait pas avancer le flux sur une exigence inconnue.

## Ce que vous ne pouvez pas faire / prudence
- La personne qui a enregistré le remplacement délivré, ou qui a saisi ce certificat de remplacement, ne peut pas le vérifier. La personne qui achève doit être différente du vérificateur, de la personne qui a enregistré le remplacement délivré et de l’auteur du certificat de remplacement.
- La vérification et l’achèvement sont uniquement des décisions humaines ; Terminé ≠ Vérifié.
- Les déclarations de planification n’établissent ni visites réalisées, ni demandes approuvées, ni aptitude au voyage, ni vérification.
- Les liens de remplacement provisoires ne remplacent pas l’original. L’ancien certificat n’est clôturé que par l’étape d’achèvement avec un lien de remplacement traçable (13.03 §1).
- Chaque étape est enregistrée comme une révision immuable ; l’historique reste dans **Historique des révisions enregistrées**.
- Sans responsable explicitement affecté, le système ne se rabat jamais sur un administrateur.

## En cas de problème
- **Aucune étape n’est disponible pour votre rôle dans cet état.** La prochaine étape appartient à un autre rôle ; vérifiez votre permission et votre désignation et contactez si nécessaire l’administrateur du locataire (13.03 §10).
- **Le renouvellement a changé après son chargement.** ou **Ce n’est pas la révision actuelle.** Appuyez sur **Recharger** et examinez l’état actuel.
- **Le résultat est inconnu. Réessayer renvoie la même requête.** Utilisez **Réessayer la même requête**.
- **Refusé pour séparation des tâches : vous avez enregistré une étape antérieure de ce renouvellement, une autre personne doit donc effectuer cette étape.** L’enregistrement n’a pas changé ; recharger n’aide pas. Laissez l’étape à une autre personne autorisée.
- **L’étape n’a pas été enregistrée. Vérifiez les informations et réessayez.** Vérifiez les champs requis et les sélections de concordance.
- Si le remplacement ne peut pas être vérifié, contrôlez le document et la traçabilité de la source et transmettez-le au vérificateur (13.03 §10).

## Pages associées
