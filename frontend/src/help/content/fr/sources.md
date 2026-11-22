---
key: sources
title: Brouillons de sources
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Brouillons de sources conserve les sources réglementaires et de règles sur lesquelles s’appuie votre organisation (convention, pavillon, classe, règle de l’entreprise ou locale) sous forme d’**enregistrements de brouillon versionnés**. Elle répond à la question : « Sur quelle révision de quelle source officielle cette exigence repose-t-elle ? » L’avis en haut de page s’applique : « BROUILLON local au locataire · Déclaré et non vérifié. Les dates et empreintes ne confèrent aucune autorité juridique. Les références de source sont stockées sous forme de texte. » (13.03 §8).

## Qui l’utilise
- **Utilisateur Réglementation / Assurance :** enregistre la source officielle et versionnée et indique sa date d’effet et son cycle de vie (13.03 §8).
- **DPA / HSEQ :** la consulte pour voir à quelle révision de source un audit ou un constat fait référence (13.03 §4).

## Avant de commencer
- La consultation nécessite la lecture à l’échelle du locataire et la lecture des brouillons réglementaires. Créer et enregistrer des brouillons nécessite en plus l’écriture des brouillons réglementaires ; sans elle, le bouton **Nouveau brouillon** n’est pas affiché.
- Les sous-vues nécessitent leurs propres permissions, toujours avec une portée de lecture à l’échelle du locataire. **Cycle de vie et applicabilité** nécessite la lecture de l’applicabilité ; modifier les règles nécessite l’écriture des règles, demander une revue nécessite la revue des règles et évaluer nécessite l’évaluation de l’applicabilité ; la qualification et l’attestation des faits nécessitent la qualification réglementaire ou l’attestation des faits plus une désignation enregistrée sur le serveur. **Contrôles et preuves** nécessite la lecture des contrôles, des preuves, des fichiers et de l’applicabilité ; créer des liens nécessite l’écriture des contrôles ou des preuves avec des droits de modification sur le navire, et l’approbation nécessite l’approbation des contrôles.
- Ayez sous la main le texte officiel de la source et les détails de sa version/de ses amendements. POSEIDON ne télécharge ni ne vérifie le texte de la source ; c’est vous qui le déclarez.
- L’enregistrement nécessite une connexion active au serveur. Hors ligne, vos modifications sont conservées dans l’onglet mais ne sont pas enregistrées.

## Étape par étape
1. Consultez les brouillons de sources enregistrés dans la liste. Pour chercher, tapez dans **Search drafts** et appuyez sur **Rechercher**. Naviguez entre les pages avec **Précédent** et **Suivant**.
2. Pour une nouvelle source, appuyez sur **Nouveau brouillon**.
3. Dans **Draft fields**, choisissez d’abord un **État** pour chaque champ : **Manquant**, **Inconnu** ou **Known (declared)**. Vous ne pouvez saisir une valeur qu’après avoir choisi **Known (declared)**. Les champs sont **Titre**, **Autorité déclarée**, **Référence source (texte uniquement)**, **Déclaration de version / amendement**, **Date d’effet déclarée**, **Déclaration / provenance** et **Source observée le (avec décalage UTC)**.
4. Appuyez sur **Enregistrer le brouillon**. En cas de succès, vous voyez « Brouillon enregistré. Non vérifié. » ; ouvrez l’enregistrement avec **Open saved draft and history**.
5. Ouvrez un brouillon existant depuis la liste, modifiez-le et enregistrez à nouveau. Chaque enregistrement crée une nouvelle **Révision** ; les révisions antérieures restent dans l’**Historique immuable** avec leur contenu et leur **Empreinte d’intégrité (sans valeur d’habilitation)**.
6. Pour passer aux règles d’applicabilité ou à la chaîne contrôle-preuve, utilisez **Cycle de vie et applicabilité** ou **Contrôles et preuves** au-dessus de la liste. Appuyez sur **Back to register** pour revenir.

## Signification des états
- **BROUILLON :** l’état stocké affiché dans la liste et dans l’en-tête de l’enregistrement. Chaque enregistrement de source sur cette page est un brouillon ; ce n’est ni une adoption officielle ni une décision d’entrée en vigueur.
- **Manquant :** aucune information n’a encore été saisie pour le champ. Les étapes ultérieures (par exemple une évaluation d’applicabilité) sont bloquées tant que des données requises manquent.
- **Inconnu :** l’information a été recherchée mais n’est pas connue. Le système ne lit jamais Inconnu comme « non » ni comme « non applicable » ; lorsque des données critiques sont inconnues, le résultat reste en revue (REVIEW_REQUIRED).
- **Known (declared) :** vous avez déclaré la valeur. Une déclaration n’est pas une information vérifiée.
- **Empreinte d’intégrité (sans valeur d’habilitation) :** montre que le contenu d’une révision n’a pas changé ; ce n’est pas une preuve de validité juridique.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas modifier une révision enregistrée. Lorsque la source change, enregistrez une nouvelle révision ; les exigences restent rattachées à l’ancienne révision et l’historique est conservé (13.03 §1).
- Ne supposez pas que les sources de classe, de pavillon, de l’entreprise et de règles locales se substituent les unes aux autres ; enregistrez chacune comme sa propre source (13.03 §8).
- Cette page n’admet pas officiellement une source et ne donne à personne un résultat de conformité, de permis ou d’état de préparation. L’admission de la source et la revue indépendante relèvent d’une autorité humaine.
- Les dates d’effet et les empreintes ne confèrent aucune autorité juridique.

## En cas de problème
- **Permission denied.** Vérifiez votre rôle et le périmètre du locataire ; adressez-vous si nécessaire à l’administrateur du locataire (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Quelqu’un a modifié l’enregistrement avant vous. Ouvrez la révision actuelle avec **Recharger le brouillon enregistré** et refaites votre modification.
- **Check field states, bounds, dates and source revision.** Une valeur de champ ou un format de date n’est pas valide.
- **Service unavailable. Your draft is retained.** Lorsque la connexion revient, renvoyez la même commande avec **Retry frozen command**.
- **Sign in to continue; retain your draft.** Votre session a pris fin ; utilisez le lien **Se connecter**.
- Si une erreur de l’application persiste, signalez au support l’identifiant de corrélation, l’écran, l’heure et vos étapes (13.03 §10).

## Pages associées
