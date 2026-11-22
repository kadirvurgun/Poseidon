---
key: document-compare
title: Comparaison de révisions de document
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Cette page affiche **deux révisions d’un document maîtrisé côte à côte, en lecture seule**, pour le navire sélectionné. Elle répond à la question : « Qu’est-ce qui diffère entre ces deux révisions au niveau du fichier et des métadonnées, et quel est l’historique de publication, de vérification et de conservation de chacune ? » La page montre les différences ; elle n’approuve, ne publie ni ne juge les modifications. Elle ne met pas une révision en vigueur et ne lui fait pas remplacer une révision antérieure. Les originaux des preuves ne sont jamais modifiés silencieusement ; lorsqu’une nouvelle révision remplace une révision antérieure, l’état de publication de la révision antérieure devient Remplacée et l’événement apparaît dans son historique de conservation (13.03 §1).

## Qui l’utilise
- **Capitaine / Chef mécanicien / Équipage :** pour comparer la révision en vigueur avec la précédente et enregistrer un accusé de lecture (13.03 §2, §5, §6).
- **DPA / Utilisateur Réglementaire et assurance :** pour examiner l’historique des révisions et les enregistrements de vérification (13.03 §4, §8).

## Avant de commencer
- Si aucun navire n’est sélectionné, la page indique **Choisissez un navire de votre périmètre.** et liste les navires. Sélectionnez le bon navire.
- Vous avez besoin d’un accès en lecture aux fichiers pour voir la page. Le contenu restreint et les documents restreints ISPS requièrent en outre leur propre autorisation.
- Le chargement du contenu et l’enregistrement d’un accusé de lecture ne fonctionnent qu’en ligne.

## Étape par étape
1. Ouvrez un document depuis la liste **Documents maîtrisés**. La classification et les libellés **Restreint ISPS** sont affichés sur la ligne.
2. Sous **Choisir deux révisions**, sélectionnez les révisions **Gauche (base)** et **Droite (examinée)**. La page propose d’abord la dernière révision à droite et celle qui la précède à gauche.
3. Appuyez sur **Comparer**. Utilisez **Inverser** pour les permuter.
4. Lisez la déclaration sur les octets et le tableau **Comparaison des métadonnées** : **Type de fichier**, **Taille (octets)**, **SHA-256 du contenu**, **Réception**, **Publication**, **Vérification** et les champs horaires.
5. Examinez l’**Historique d’approbation et de conservation** de chaque côté.
6. Pour voir le contenu, appuyez sur **Charger le contenu (accès enregistré)**. Le contenu est affiché avec un filigrane d’attribution.
7. Après **Charger le contenu (accès enregistré)**, si vous avez lu la révision en vigueur, cochez **J’ai lu cette révision et j’ai pu accéder à son contenu.** et appuyez sur **Enregistrer l’accusé de lecture**.

## Signification des états
- **Unpublished / Effective / Superseded :** l’état de publication de la révision. Une révision qui n’est pas en vigueur affiche **Non en vigueur opérationnelle**.
- **Unverified / Verified valid / Verified invalid / Verification inconclusive :** l’enregistrement de vérification de la révision. Non vérifié ne signifie pas « valide ».
- **Identique / Changed / Not comparable :** le résultat d’une ligne de métadonnées. Non comparable ne signifie pas « Identique ».
- **Byte-identical files :** uniquement des octets identiques ; ce n’est pas un examen du contenu.
- **Files differ at byte level :** quelle règle a changé, et sa portée, doivent être examinées par une personne.
- **Contenu restreint pour votre rôle ; métadonnées seulement.** le contenu n’est pas ouvert pour ce rôle.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas publier, approuver, mettre en vigueur ou remplacer un document depuis cette page.
- La page n’interprète pas automatiquement les différences de contenu ; une personne autorisée décide de leur portée.
- Un accusé de lecture n’est enregistré que pour la révision en vigueur, une fois par personne et par révision. Ce n’est ni une approbation ni une vérification.
- Une copie imprimée n’est pas maîtrisée. Le contenu n’est pas mis en cache et n’est pas conservé hors ligne.
- Hors ligne, la comparaison est obsolète et aucun nouveau contenu ne peut être chargé.

## En cas de problème
- **Ce document ne vous est pas accessible :** vérifiez votre contexte de navire et votre autorisation ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Vous n’avez pas l’autorisation pour cette action :** vérifiez votre rôle et votre contexte de navire (13.03 §10).
- **L’enregistrement a changé entre-temps :** utilisez **Recharger** pour ouvrir la dernière version.
- **Une révision ne peut pas être vérifiée :** contrôlez la preuve et l’historique de provenance ; adressez-vous à un vérificateur autorisé (13.03 §10).
- **La requête a échoué :** envoyez l’écran, l’heure et les étapes effectuées au support (13.03 §10).

## Pages associées
