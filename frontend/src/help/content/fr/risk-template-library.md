---
key: risk-template-library
title: Bibliothèque de modèles de risque
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La bibliothèque de modèles de risque gère, avec leurs révisions, les modèles d’évaluation des risques utilisés dans tout le locataire (type de travail, dangers, mesures de maîtrise par défaut, barrières requises, sources SMS et d’exigences) ainsi que les méthodes de risque (matrice). Elle répond à la question : « Quel modèle publié s’applique à ce type de travail, et quelle révision est en vigueur ? » La page est **uniquement une cible de développement** : une publication ici est un enregistrement humain HSEQ, pas un permis, une acceptation du risque par un expert ni le passage d’un jalon HSE (13.03 §4).

## Qui l’utilise
- **DPA / HSEQ :** dans le cadre de la gouvernance des risques, préparent, revoient et publient les révisions de modèles, les barrières et la matrice de risque (13.03 §4).
- **Capitaine, Capitaine du remorqueur et équipage :** lisent les modèles publiés et envoient des **Suggestions de l’équipage** pour un modèle.

## Avant de commencer
- La lecture nécessite la permission de lecture des modèles. Rédiger et publier des modèles nécessite une portée à l’échelle du locataire et la permission d’écriture ou de publication des modèles. Envoyer une suggestion ne nécessite que la permission de suggestion de modèle.
- Créer et publier une méthode de risque sont des permissions distinctes et nécessitent une portée à l’échelle du locataire.
- Les commandes de risque nécessitent une connexion en ligne actuelle ; rien n’est mis en file d’attente ni approuvé hors ligne.

## Étape par étape
1. Consultez les modèles dans la liste. Chaque ligne affiche l’état, la révision **Actuel** et le nombre de **Suggestions**. Ouvrez un modèle.
2. Pour un nouveau modèle, appuyez sur **Nouveau modèle**. Remplissez **Code du modèle**, **Titre**, **Type de travail**, **Périmètre navire / remorqueur**, **Responsable de l’approbation**, **Motif du changement**, **Required barriers**, **SMS / requirement sources** et **Dangers**, puis appuyez sur **Enregistrer le brouillon**.
3. Sur une révision en brouillon, appuyez sur **Soumettre à la revue**. Les éléments manquants sont listés comme points bloquants.
4. Un éditeur saisit **En vigueur à partir de** et un **Motif** d’au moins 10 caractères et appuie sur **Publier (enregistrement humain HSEQ)** ou **Renvoyer**.
5. Pour modifier un modèle publié, rédigez un motif et appuyez sur **Démarrer une nouvelle révision** ; l’ancienne révision ne change pas.
6. L’équipage rédige une suggestion dans **Envoyer une suggestion** et l’envoie. Une personne autorisée l’examine avec **Accepter** ou **Rejeter** et un motif.
7. Dans **Méthodes de risque (matrice)**, utilisez **Nouvelle méthode** pour saisir **Code de méthode**, **Likelihood codes**, **Severity codes** et **Matrix cells** ; une autre personne la publie avec **Publier la méthode**.

## Signification des états
- **Draft / In review / Returned / Published / Superseded :** le cycle de vie de la révision du modèle.
- **Actuel / Planifié / Not effective :** la vue d’application. La dernière révision publiée dont la date d’effet est atteinte est **Actuel** ; une révision avec une date future est **Planifié**. Une révision future ne remplace jamais l’ancienne prématurément.
- **Inconnu :** le champ n’est pas renseigné ; une valeur vide n’est jamais remplie avec une valeur par défaut.
- **No published risk method: ratings stay not assessed.** Le système ne propose aucune matrice ni aucun seuil par défaut.

## Ce que vous ne pouvez pas faire / prudence
- Un auteur d’une révision ou la personne qui l’a soumise ne peut pas publier la même révision ; la publication est un enregistrement humain distinct (séparation des tâches).
- La publication n’est ni un permis, ni une acceptation du risque par un expert, ni un catalogue de production approuvé.
- Un modèle n’est appliqué à une évaluation des risques que sous forme de **copie** ; il ne comporte ni cotation, ni signature, ni confirmation des participants, ni approbation, ni accusé de réception. Lorsqu’un modèle change, les évaluations existantes ne changent pas d’elles-mêmes.
- Une révision publiée n’est jamais modifiée ; démarrez une nouvelle révision pour la changer.

## En cas de problème
- **Bloqué jusqu’à ce que les éléments listés soient complétés.** Complétez les points bloquants et soumettez à nouveau.
- **This record changed elsewhere.** Appuyez sur **Recharger** pour voir la révision actuelle.
- **Vous n’êtes pas autorisé à effectuer cette action (séparation des tâches ou périmètre).** La publication doit être effectuée par une autre personne autorisée qui n’est pas l’auteur de la révision ; si une permission manque, contactez l’administrateur du locataire (13.03 §10).
- **Cette action ne s’applique plus à l’état actuel. Rechargez.** L’état de la révision a changé.

## Pages associées
