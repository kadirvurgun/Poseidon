---
key: pms-command-center
title: Centre de commande PMS
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le centre de commande PMS rassemble sur un seul écran les **exceptions** de maintenance du navire sélectionné. Il répond à la question : « Ce navire a-t-il des tâches critiques en retard, des ordres de travail ouverts, des alertes de défaut ou des manques de pièces critiques, et pourquoi ? » La page est une vue consultative des exceptions ; ce n’est pas une décision d’aptitude, de disponibilité ou de sécurité. Les éléments sains ne sont pas listés, seulement comptés.

## Qui l’utilise
- **Chef mécanicien / Surintendant technique :** pour suivre les tâches en retard, les ordres de travail ouverts, les alertes de défaut et les manques de pièces critiques (13.03 §5).
- **Capitaine :** pour voir l’impact des enregistrements PMS et de défauts en termes de restriction opérationnelle (13.03 §2).

## Avant de commencer
- Vous avez besoin de la permission de lecture des équipements (`equipment.read`) pour voir la page.
- Chaque section dépend de la permission de lecture de son propre module. Par exemple, sans permission de lecture des défauts, la section **Alertes d’état** reste « Accès requis : non affiché ».
- Choisissez d’abord un navire. Sans navire, la page n’affiche que la liste **Choisissez un navire.**

## Étape par étape
1. Choisissez le navire dans la liste. Si aucun n’est dans votre périmètre, la page indique « Aucun navire dans votre périmètre. »
2. Lisez le badge **Synthèse des exceptions** et l’heure de **Dernière actualisation** en haut.
3. Examinez les sections : **Tâches critiques en retard**, **Tâches dues, en retard et à revoir**, **Ordres de travail ouverts**, **Alertes d’état** et **Manques de pièces critiques**.
4. Ouvrez **Pourquoi** sur un élément pour voir les codes de raison enregistrés et, s’il existe, l’horodatage **Observé**.
5. Utilisez **Ouvrir l’enregistrement source** pour aller à la source. Un élément de défaut ouvre [Défauts et mesures temporaires](/app/defects), un élément d’ordre de travail ouvre [Centre des ordres de travail](/app/work-order-center).
6. Appuyez sur **Actualiser** pour relire les données.

## Signification des états
- **Blocked / Review required / Attention :** l’état de la synthèse des exceptions. Les détails figurent dans les éléments des sections.
- **Aucune exception ouverte (consultatif) :** les sources lues n’ont renvoyé aucune exception ouverte. Ce n’est pas un résultat de disponibilité ou d’aptitude.
- **Critical / Attention / Review required :** la gravité d’un élément.
- **Source indisponible : inconnu, pas zéro :** la source de la section n’a pas pu être lue. Inconnu ne signifie ni « zéro », ni « aucun problème », ni « Prêt ».
- **Accès requis : non affiché :** vous n’avez pas de permission pour cette section ; ni le contenu ni le nombre ne sont affichés.
- **Aucune exception de cette source :** la section a été lue et n’a renvoyé aucune exception. Le nombre d’éléments sains est affiché séparément.
- **Stale data / Freshness unknown :** la lecture derrière l’élément n’est pas actuelle, ou sa fraîcheur est inconnue.

## Ce que vous ne pouvez pas faire / prudence
- La page ne modifie aucun enregistrement ; elle ne fait que lire. Ordres de travail, défauts et pièces de rechange sont traités sur leurs propres pages.
- La page ne déclare jamais un navire « prêt » ou « sûr ». La décision d’appareillage appartient au Capitaine (13.03 §2).
- Un défaut ne compte comme clôturé que par un enregistrement de clôture humain, et uniquement pour la révision de déclaration qui a été clôturée. Si la déclaration change ensuite, l’élément revient en revue.
- Les données affichées hors ligne peuvent être anciennes, et **Actualiser** n’est pas disponible.

## En cas de problème
- **L’enregistrement n’a pas pu être chargé :** vérifiez votre connexion et utilisez **Recharger**.
- **Vous n’avez pas la permission serveur pour cette action ou cet enregistrement :** vérifiez votre rôle et le contexte du navire ; contactez si nécessaire l’administrateur de votre locataire (13.03 §10).
- **Exception inattendue :** ouvrez **Pourquoi** et examinez l’enregistrement source avec le responsable ou l’expert métier (13.03 §10).
- **Erreur de l’application :** signalez au support l’identifiant de corrélation (Correlation ID), l’écran, l’heure et vos étapes (13.03 §10).

## Pages associées
