---
key: data-trust-center
title: Centre de confiance des données
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le centre de confiance des données affiche les **signaux de qualité des données** pour les contrôles, les preuves et les champs du profil du navire sélectionné. Il répond à la question : « Quels enregistrements de ce navire sont manquants, obsolètes, contradictoires, non vérifiés ou orphelins, et quelles informations de source et de fraîcheur l’indiquent ? » Un signal de confiance n’est pas une vérification de l’enregistrement sous-jacent. La page ne marque jamais rien comme prêt.

## Qui l’utilise
- **Utilisateur Réglementaire / Assurance :** pour ne pas laisser d’enregistrements orphelins ou incomplets dans la chaîne Exigence → Contrôle → Preuve → Vérification (13.03 §8).
- **DPA :** pour suivre les lacunes de preuves et de vérification (13.03 §4).
- **Surintendant technique / Capitaine :** pour voir les signaux qui maintiennent une évaluation de disponibilité au stade revue requise (13.03 §2, §5).

## Avant de commencer
- Dans la barre de contexte supérieure, vérifiez que la **bonne société** est sélectionnée (13.03 §1).
- La page requiert, tous ensemble, l’accès en lecture aux contrôles, aux preuves, aux fichiers, à l’applicabilité et aux brouillons réglementaires du locataire, avec un périmètre de lecture couvrant tout le locataire.
- Si aucun navire n’est sélectionné, la page affiche d’abord la liste « Choisir un navire ».

## Étape par étape
1. Choisissez le navire dans la liste « Choisir un navire ».
2. Dans **Résumé**, lisez l’état global (« Revue requise » ou « Aucun signal significatif ») et les nombres **Objets par signal**.
3. Dans le tableau **Contrôles et preuves**, vérifiez pour chaque objet les colonnes « Source / provenance », « Fraîcheur », « Vérification » et « Signaux ».
4. Notez le code de motif à côté d’un badge de signal et examinez l’enregistrement sur sa propre page.
5. Consultez les signaux des champs de profil dans le tableau **Champs du profil navire**.
6. Utilisez **Précédent** / **Suivant** pour les autres objets.
7. Si « Ces signaux peuvent être obsolètes. » apparaît, appuyez sur **Recharger**.

## Signification des états
- **Missing / Stale / Conflicting / Unverified / Orphan :** le type de signal sur un objet. Un signal est un marqueur de qualité ; il ne prouve pas que l’enregistrement est juste ou faux.
- **Aucun signal :** aucun signal n’a été trouvé sur cet objet. Cela ne signifie pas que l’enregistrement est vérifié.
- **Revue requise :** un signal significatif existe. La disponibilité est maintenue au stade revue requise tant que des signaux significatifs existent.
- **Aucun signal significatif :** ce n’est pas une déclaration de disponibilité.
- **Vérifié par une personne :** une personne autorisée a vérifié la preuve. Cela provient de l’enregistrement propre de la preuve ; le centre ne vérifie rien.
- **Not verified / Verification expired / Verification rejected :** l’état de vérification de la preuve.
- **Sans objet pour les contrôles :** affiché sur les lignes de contrôle ; l’état de vérification concerne les lignes de preuve.
- **Observation time unknown / No freshness policy / Future-dated :** la fraîcheur n’a pas pu être établie ou la date se situe dans le futur. Aucun de ces états ne signifie « en vigueur ».

## Ce que vous ne pouvez pas faire / prudence
- Les enregistrements ne sont ni corrigés, ni vérifiés, ni clôturés depuis cette page ; cela se fait sur la propre page de l’enregistrement, par une personne autorisée.
- Si seuls les premiers objets ont été évalués, le résumé reste en revue requise.
- Les objets que vous ne pouvez pas lire sont comptés mais pas affichés, et le résumé reste alors Revue requise. Un enregistrement que vous ne pouvez pas voir ne signifie pas qu’il n’y a aucun problème.
- Hors ligne, les signaux de confiance des données ne peuvent pas être actualisés.

## En cas de problème
- **Vous n’êtes pas autorisé à lire les signaux de confiance des données :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **L’enregistrement ne peut pas être vérifié :** contrôlez la preuve et sa provenance ; signalez-le au vérificateur autorisé.
- **Signal inattendu :** signalez le code de motif au responsable du contrôle ou à l’expert métier.
- **Les signaux de confiance des données sont indisponibles :** utilisez « Réessayer » ; si le problème persiste, signalez-le au support avec une capture d’écran, l’heure et les étapes effectuées.

## Pages associées
