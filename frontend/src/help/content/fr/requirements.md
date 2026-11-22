---
key: requirements
title: Brouillons d’exigences
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Brouillons d’exigences conserve les **exigences** dérivées d’une révision de source sous forme de brouillons versionnés et rattache chaque exigence à une révision de source exacte. Elle répond à la question : « Sur quelle révision de quelle source cette exigence repose-t-elle, et quel est son cycle de vie enregistré ? » Une exigence est le premier maillon de la chaîne Exigence → Contrôle → Preuve → Vérification. Aucun enregistrement de cette chaîne ne doit rester non lié (orphelin) (13.03 §8).

## Qui l’utilise
- **Utilisateur Réglementation / Assurance :** rattache l’exigence à sa source, évalue l’applicabilité avec les données de contexte et maintient la chaîne complète (13.03 §8).
- **DPA / HSEQ :** lit le fondement d’une exigence lors des travaux de constat et d’audit (13.03 §4).

## Avant de commencer
- La consultation nécessite la lecture à l’échelle du locataire et la lecture des brouillons réglementaires ; la modification nécessite en plus l’écriture des brouillons réglementaires.
- Les sous-vues nécessitent leurs propres permissions, toujours avec une portée de lecture à l’échelle du locataire. **Cycle de vie et applicabilité** nécessite la lecture de l’applicabilité ; modifier les règles nécessite l’écriture des règles, demander une revue nécessite la revue des règles et évaluer nécessite l’évaluation de l’applicabilité ; la qualification et l’attestation des faits nécessitent la qualification réglementaire ou l’attestation des faits plus une désignation enregistrée sur le serveur. **Contrôles et preuves** nécessite la lecture des contrôles, des preuves, des fichiers et de l’applicabilité ; créer des liens nécessite l’écriture des contrôles ou des preuves avec des droits de modification sur le navire, et l’approbation nécessite l’approbation des contrôles.
- Enregistrez d’abord la source sur la page **Brouillons de sources**. Une exigence ne peut être rattachée qu’à une révision de source existante.
- L’enregistrement nécessite une connexion active au serveur ; hors ligne, les modifications sont conservées dans l’onglet.

## Étape par étape
1. Consultez les brouillons d’exigences dans la liste ; cherchez avec **Search drafts** et **Rechercher**.
2. Pour une nouvelle exigence, appuyez sur **Nouveau brouillon**.
3. Dans **Draft fields**, choisissez un **État** pour chaque champ (**Manquant**, **Inconnu**, **Known (declared)**) et saisissez les valeurs connues : **Instrument**, **Déclaration de version / amendement**, **Date d’effet déclarée**, **Déclaration / provenance**, **Source observée le (avec décalage UTC)**.
4. Dans **Immutable source citation**, tapez dans **Find a source** et appuyez sur **Search sources**. Choisissez la source dans **Source draft**, puis la révision exacte dans **Exact revision**. Vérifiez le contenu de la révision choisie avec **Selected immutable source content**.
5. Appuyez sur **Enregistrer le brouillon**. Lorsque l’enregistrement est ouvert, les sections **Cycle de vie enregistré** et **Historique immuable** apparaissent.
6. Utilisez **Cycle de vie et applicabilité** pour l’applicabilité et **Contrôles et preuves** pour les liens de contrôle et de preuve. Le **Rapport des éléments critiques orphelins** dans **Contrôles et preuves** liste les déclarations critiques sans lien d’exigence ; liez ces enregistrements à une exigence.

## Signification des états
- **Cycle de vie enregistré :** l’état que le serveur déduit des règles rattachées : **Brouillon**, **Adopté**, **Effet futur**, **En vigueur**, **Remplacé**. S’il ne peut pas être lu, **Indisponible** est affiché ; le système ne suppose ni brouillon ni en vigueur.
- « Les règles rattachées enregistrent des états de cycle de vie différents. La valeur affichée est une priorité logicielle ; un examen par un expert est requis. » La valeur affichée n’est alors pas une décision ; un examen par un expert est nécessaire.
- **Inconnu :** l’information n’est pas connue. Lorsque des données critiques sont inconnues, le résultat d’applicabilité reste **Revue requise** (REVIEW_REQUIRED) ; il n’est jamais compté comme « non applicable » ni comme « non » (13.03 §8).
- **Applicability review required / Requirement link missing / Verification not performed :** éléments ouverts dans la chaîne contrôle-preuve.
- **Achèvement inconnu · Non vérifié · Efficacité inconnue :** l’achèvement, la vérification et l’efficacité sont des états distincts. Un enregistrement terminé n’est pas vérifié, et un enregistrement vérifié n’est pas efficace.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas rattacher une exigence à « la plus récente » source ; une **Exact revision** est toujours choisie. Lorsque la source reçoit une nouvelle révision, l’ancien rattachement est conservé dans l’historique.
- Un aperçu d’applicabilité n’établit ni conformité, ni état de préparation, ni permis, ni approbation juridique : « APERÇU / BROUILLON. Les prédictions n’établissent ni conformité, ni état de préparation, ni permis, ni approbation juridique. »
- Un changement de cycle de vie passe par **Demander un examen humain** ; l’admission de la source et un examinateur indépendant qualifié décident. Le système ne le clôture jamais de lui-même.
- Ne supposez pas que les sources de classe, de pavillon, de l’entreprise et de règles locales se substituent les unes aux autres (13.03 §8).

## En cas de problème
- **Source selection unavailable.** Appuyez sur **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Ouvrez la révision actuelle avec **Recharger le brouillon enregistré**.
- **Check field states, bounds, dates and source revision.** Si vous avez choisi une source, choisissez aussi sa révision exacte.
- **History unavailable. Editor retained.** Appuyez sur **Retry history**.
- Si le cycle de vie est inattendu, examinez les règles rattachées et les révisions de source ; contactez si nécessaire le responsable du contrôle ou l’expert métier (SME) (13.03 §10).
- Sans permission, vérifiez votre rôle et le périmètre du locataire et adressez-vous à l’administrateur du locataire (13.03 §10).

## Pages associées
