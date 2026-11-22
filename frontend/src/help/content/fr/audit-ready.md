---
key: audit-ready
title: Traçabilité prête pour l’audit
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Traçabilité prête pour l’audit affiche, étape par étape, les chaînes de traçabilité qui vous sont attribuées pour un navire : Exigence → contrôle → preuve → vérification → décision, avec les états, les horodatages et les références de source déclarées. Elle répond à la question : « Sur quelle exigence repose cette preuve, qui l’a vérifiée et quelle décision a été enregistrée ? » Une chaîne enregistrée est un enregistrement humain, et non une approbation ou une déclaration de disponibilité (13.03 §4, §8).

## Qui l’utilise
- **Utilisateur Réglementaire / Assurance :** vérifie qu’aucun enregistrement orphelin ne subsiste dans la chaîne Exigence→Contrôle→Preuve→Vérification (13.03 §8).
- **DPA / HSEQ :** présente la piste Constat→Action→Vérification avec ses preuves lors de la préparation d’un audit, d’un contrôle PSC ou d’une inspection (13.03 §4).

## Avant de commencer
- La page requiert la lecture d’audit et la lecture de traçabilité humaine, ainsi qu’une désignation en cours de lecteur de traçabilité pour ce navire. L’autorisation seule ne suffit pas ; sans la désignation, aucune chaîne n’est listée.
- Choisissez le bon navire. La liste n’affiche que les chaînes qui vous sont attribuées.
- La page se charge via une connexion en ligne. Hors ligne, les données affichées peuvent être obsolètes.

## Étape par étape
1. Choisissez un navire dans **Navire** (**Sélectionner un navire**).
2. Ouvrez une chaîne depuis **Chaînes attribuées**.
3. Dans **Étapes de traçabilité**, développez chaque étape (Exigence, Contrôle, Preuve, Vérification, Décision) et examinez **État**, **Enregistré le**, **Révision**, **Empreinte du contenu** et **Référence de source (déclarée, non vérifiée)**.
4. Si des **Points ouverts** sont listés, transmettez les codes au responsable de l’enregistrement concerné.
5. Lorsque des chaînes attribuées lisibles existent, compilez un dossier dans **Dossier d’audit** avec **Compiler un nouveau dossier**, ouvrez-le depuis **Dossiers compilés** et utilisez **Exporter en JSON** si nécessaire.
6. Appuyez sur **Réessayer** dans l’en-tête pour actualiser les données.

## Signification des états
- **Chaîne enregistrée — enregistrement humain uniquement, pas une approbation :** chaque étape est enregistrée. Cela ne déclare pas le navire ou l’opération prêt ou conforme.
- **Revue requise :** au moins une étape est manquante, inconnue ou indisponible. Une entrée inconnue ou illisible n’est jamais affichée comme complète.
- **Non enregistré :** aucun enregistrement n’existe pour cette étape ; par exemple, aucune vérification ni décision n’a encore été enregistrée.
- **Indisponible / UNKNOWN :** l’enregistrement ne peut pas être lu pour l’instant ; son contenu n’est pas présumé.
- **Obsolète : une entrée a été remplacée ou a expiré ; revue requise.** Une preuve ou une source a changé ultérieurement ; examinez de nouveau la chaîne.
- **Référence non fournie :** aucune référence de source n’a été saisie ; une référence est une déclaration, pas un fait vérifié.
- Une preuve vérifiée ne signifie pas que l’action a été efficace : Vérifié ≠ Efficace (13.03 §4).
- **Dossier d’audit :** « Compilation système ; pas un enregistrement officiel ». Les éléments hors de vos désignations sont seulement comptés, pas affichés ; les éléments **Remplacé (conservé pour la traçabilité)** ne sont jamais traités comme actuels.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas modifier des enregistrements, vérifier ou décider sur cette page ; elle est en lecture seule. Les vérifications et les décisions sont enregistrées par la personne désignée sur leurs propres pages.
- Vous ne pouvez pas voir les chaînes qui ne vous sont pas attribuées ; les chaînes masquées ne sont jamais nommées, et les chaînes attribuées indisponibles ne sont affichées que sous forme de nombre.
- Les empreintes d’intégrité et de contexte ne constituent ni une autorité ni une approbation.

## En cas de problème
- **Vous n’êtes pas autorisé pour cette vue.** Vérifiez votre rôle, votre navire et votre contexte de désignation ; contactez le Tenant Admin (13.03 §10).
- **Aucune chaîne ne vous est attribuée pour ce navire.** Vérifiez que vous avez sélectionné le bon navire ; demandez à la personne responsable si une désignation est nécessaire.
- **La traçabilité n’a pas pu être chargée.** Appuyez sur **Réessayer**.
- **Chaîne introuvable ou plus visible pour vous.** Votre désignation a peut-être pris fin ou l’enregistrement a quitté votre périmètre.
- Si un enregistrement ne peut pas être vérifié, contrôlez sa preuve et sa provenance et contactez le vérificateur si nécessaire (13.03 §10).

## Pages associées
