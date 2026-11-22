---
key: risk-assessment
title: Évaluation des risques
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Évaluation des risques enregistre les dangers, les conséquences potentielles, les mesures de maîtrise, le risque initial et résiduel et les conditions d’arrêt de travail pour un travail ou une opération sur un navire. Elle répond à la question : « Les dangers et les mesures de maîtrise de ce travail ont-ils été pris en compte, revus et acquittés par les participants ? » La page est **uniquement une cible de développement** : une approbation ici est un enregistrement de revue humaine, pas un permis, une acceptation du risque par un expert ni le passage d’un jalon HSE (13.03 §2, §4).

## Qui l’utilise
- **Capitaine et Capitaine du remorqueur :** font préparer l’évaluation pour les travaux sur le navire ou le remorqueur et prennent la décision d’arrêt de travail. L’autorité de décision leur reste (13.03 §2, §3).
- **DPA / HSEQ :** supervisent la gouvernance des risques et enregistrent la décision de revue (13.03 §4).
- **Équipage / OOW / Mécanicien :** en tant que participants, acquittent ou contestent pour eux-mêmes l’évaluation approuvée (13.03 §6).

## Avant de commencer
- Choisissez un navire pour ouvrir ses évaluations des risques. La lecture nécessite la permission de lecture des risques ; rédiger un brouillon nécessite la permission d’écriture des risques et une portée de modification sur ce navire. La décision de revue nécessite la permission d’approbation des risques ; l’accusé de réception nécessite la permission d’accusé de réception des risques.
- Les cotations nécessitent une méthode de risque publiée (matrice) dans le locataire. Sans elle, les cotations restent **Non évalué**. Un responsable HSEQ du locataire configure la matrice et une autre personne la publie (page Bibliothèque de modèles de risque).
- Les commandes de risque nécessitent une connexion en ligne actuelle ; rien n’est mis en file d’attente ni approuvé hors ligne.

## Étape par étape
1. Choisissez le navire et ouvrez une évaluation dans la liste, ou appuyez sur **Nouvelle évaluation**.
2. Saisissez un **Numéro d’évaluation**. Facultativement, saisissez un **Type de travail** et utilisez **Partir d’un modèle publié (copie uniquement)** pour choisir un modèle correspondant ; le modèle est seulement copié dans le nouveau brouillon.
3. Remplissez **Titre**, **Contexte d’opération / de travail**, **Type de travail**, **Périmètre du travail** et **Méthode de risque**. Pour **Arrêt de travail requis**, choisissez **Oui**, **Non** ou **Inconnu** et, si nécessaire, rédigez les **Stop-work conditions**.
4. Avec **Ajouter un danger**, saisissez pour chaque danger l’**Étape du travail**, les **Potential consequences**, le **Risque initial** et le **Risque résiduel** (vraisemblance, gravité, justification). Avec **Ajouter une mesure**, ajoutez les **Mesures de maîtrise**, le **Responsable de la mesure** et les **Barrier references**.
5. Dans **Participants**, écrivez un identifiant d’acteur et un rôle par ligne. Liez si nécessaire des enregistrements associés avec **Ajouter un lien**.
6. Appuyez sur **Enregistrer le brouillon**, puis sur **Soumettre à la revue**. **Points bloquants** liste ce qui manque.
7. Un réviseur rédige un **Motif** d’au moins 10 caractères et appuie sur **Approuver (enregistrement de revue humaine)** ou **Renvoyer**.
8. Après l’approbation, chaque participant appuie pour lui-même sur **Accuser réception** ou, avec un motif, sur **Contester**. Si une modification est nécessaire, utilisez **Amender (ouvre un nouveau brouillon)**.

## Signification des états
- **Draft / In review / Returned / Approved (human record) :** le cycle de vie de l’évaluation. Approuvé est un enregistrement de revue humaine, pas un permis.
- **Non évalué / Method not configured :** il n’y a pas de cotation. Le système n’utilise aucune matrice par défaut, aucune multiplication ni aucun seuil faible/moyen/élevé.
- **Arrêt de travail requis — Inconnu :** aucune décision n’a encore été prise ; ce n’est jamais lu comme « Non ».
- **Acknowledgement not applicable before approval / Acknowledgements pending / All participants acknowledged / Disputed – unresolved :** l’état des accusés de réception des participants. Une contestation n’est jamais close par l’accusé de réception de quelqu’un d’autre.
- **Changed since pinned / Indisponible :** un enregistrement lié a changé après la révision épinglée ou ne peut plus être lu ; examinez-le à nouveau.

## Ce que vous ne pouvez pas faire / prudence
- Un auteur ou la personne qui a soumis le cycle de revue ne peut pas approuver la même évaluation (séparation des tâches).
- Chacun accuse réception uniquement pour lui-même et uniquement de la révision approuvée exacte.
- Une approbation ici n’est ni un permis, ni une acceptation du risque par un expert, ni un jalon HSE. La décision de commencer ou d’arrêter le travail reste au Capitaine ou au Capitaine du remorqueur.
- Un brouillon créé à partir d’un modèle ne comporte ni cotation, ni signature, ni confirmation des participants, ni approbation.
- Hors ligne, une évaluation ne peut être ni soumise, ni approuvée, ni acquittée.

## En cas de problème
- **Bloqué jusqu’à ce que les éléments listés soient complétés.** Complétez les points bloquants et soumettez à nouveau.
- **This record changed elsewhere.** Appuyez sur **Recharger** pour voir la révision actuelle, puis répétez l’action.
- **Vous n’êtes pas autorisé à effectuer cette action (séparation des tâches ou périmètre).** Demandez à une autre personne autorisée d’effectuer la revue, ou vérifiez votre rôle et le contexte du navire ; contactez si nécessaire l’administrateur du locataire (13.03 §10).
- **L’espace risques n’est pas disponible avec vos accès actuels.** Vérifiez vos permissions.
- Si une cotation semble inattendue, vérifiez la méthode de risque et sa révision ; adressez-vous au responsable du contrôle ou au responsable HSEQ (13.03 §10).

## Pages associées
