---
key: regulatory-review
title: Revue réglementaire SME
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La revue réglementaire SME enregistre des campagnes au cours desquelles un expert humain examine les éléments de source et d’applicabilité d’un navire ou d’un remorqueur. Elle répond à la question : « Quels éléments de source et d’exigence de ce navire ont été confirmés par un examinateur autorisé, et lesquels restent ouverts et suivis ? » C’est uniquement un enregistrement de revue humaine : le système n’approuve aucune exigence, n’active aucune règle ni aucun permis et ne revendique aucune approbation de classe ou de type (13.03 §8).

## Qui l’utilise
- **Utilisateur Réglementation / Assurance :** ouvre des campagnes d’examen, enregistre des verdicts sur les éléments et clôture la campagne (13.03 §8).
- **Examinateur désigné (SME) :** confirme ou suit des éléments dans le cadre de sa propre autorité de source.
- **DPA / HSEQ :** surveille les éléments ouverts et non suivis (13.03 §4).

## Avant de commencer
- La consultation nécessite la lecture de la revue réglementaire. Ouvrir et clôturer des campagnes nécessite la gestion ; enregistrer des verdicts nécessite la permission de verdict et une désignation d’examinateur en cours pour ce périmètre.
- Choisissez **Navire** ou **Remorqueur** dans les onglets **Périmètre d’examen**, puis ouvrez le bon navire dans **Choisir un navire** (13.03 §1).
- Les sources doivent être enregistrées comme officielles et versionnées sur la page [Brouillons de sources](/app/sources) (13.03 §8).
- Hors ligne, les données peuvent être périmées et les commandes sont désactivées.

## Étape par étape
1. Choisissez le périmètre et le navire. S’il n’y a pas de campagne, appuyez sur **Ouvrir une campagne d’examen**.
2. Dans **Matrice de responsabilité des sources**, examinez pour chaque autorité de source la catégorie, les nombres d’éléments ouverts et confirmés et les rôles responsables.
3. Sélectionnez un élément dans **Éléments à examiner**.
4. Dans le formulaire **Enregistrer le verdict**, remplissez **Désignation d’examinateur**, **Verdict**, **Autorité de la source** et **Rôle responsable**. Si l’élément n’a pas de source enregistrée, saisissez la **Référence de la source faisant foi** et la **Version / édition de la source**. Pour un élément qui reste ouvert, saisissez un **Identifiant du ticket suivi**.
5. Rédigez un motif de 10 à 2000 caractères dans **Note de l’examinateur** et appuyez sur **Enregistrer**.
6. Lorsque tous les éléments sont réglés, choisissez un **Résultat de clôture** dans **Clôturer la campagne**, rédigez le **Motif de clôture** et appuyez sur **Clôturer la campagne**.

## Signification des états
- Campagne : **Ouvert**, **En revue**, **Clôturée avec éléments ouverts**, **Clôturé**.
- Verdict : **Confirmé**, **Non applicable (confirmé)**, **Revue requise**, **Ambigu**. Lorsque des données critiques sont inconnues, l’élément reste **Revue requise** ; un élément inconnu n’est jamais compté comme « Non applicable ».
- **Candidat (non vérifié) :** la source n’est pas encore rattachée à une source officielle vérifiée.
- **Périmé — référence modifiée** et **Profil du navire modifié :** la source ou le profil du navire a changé après le verdict ; l’élément doit être examiné à nouveau.
- **Éléments ouverts non suivis :** éléments ouverts sans ticket suivi. Tant que ce nombre est supérieur à zéro, la campagne ne peut pas être clôturée.
- Les catégories **Droit réglementaire**, **Règle de classe**, **SMS de l’entreprise** et **Guide — pas du droit** ne se substituent jamais les unes aux autres (13.03 §8).

## Ce que vous ne pouvez pas faire / prudence
- La personne qui a ouvert une campagne ne peut pas y enregistrer de verdicts ; sans désignation d’examinateur, les verdicts sont en lecture seule.
- Un examinateur dont la qualification n’est pas vérifiée peut suivre des éléments ouverts mais ne peut pas confirmer. Un élément périmé ne peut pas être confirmé.
- Confirmer nécessite une source enregistrée ou une référence de source faisant foi avec son édition. Ne laissez aucun enregistrement orphelin ou sans source dans la chaîne Exigence → Contrôle → Preuve → Vérification (13.03 §8).
- **Clôturé** exige que chaque élément soit confirmé ; sinon, clôturez avec des éléments ouverts, chacun étant suivi.
- N’indiquez pas d’approbation de classe ou de type dans la note. Dans le périmètre remorqueur, aucune limite de remorquage sans source contrôlée n’est une valeur par défaut légale.
- Les verdicts ne sont enregistrés que par des utilisateurs humains ; les comptes d’IA et d’automatisation ne peuvent pas les enregistrer.

## En cas de problème
- **L’enregistrement a changé. Rechargez et vérifiez avant de continuer.** Appuyez sur **Recharger**.
- **Le résultat est incertain. Réessayer envoie la même commande.** Utilisez **Réessayer**.
- **Les règles d’examen ont refusé cette commande.** Vérifiez si l’élément est périmé, la référence de la source et le ticket suivi.
- **Vous n’avez pas accès à l’examen réglementaire de ce navire.** ou **Vous n’êtes pas autorisé à effectuer cette action.** Vérifiez votre rôle et votre périmètre ; contactez si nécessaire l’administrateur du locataire (13.03 §10).
- Si l’applicabilité semble inattendue, vérifiez la source et les données de contexte et parlez-en au responsable du contrôle ou à l’expert métier (13.03 §10).

## Pages associées
