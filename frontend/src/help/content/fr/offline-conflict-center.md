---
key: offline-conflict-center
title: Centre des conflits importants
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le centre des conflits importants liste, pour le navire ou le remorqueur sélectionné, les **conflits importants entre vos modifications hors ligne et l’enregistrement actuel du serveur**, et permet à une personne de les résoudre par une décision motivée. Il répond à la question : « Lesquelles de mes modifications hors ligne sont entrées en conflit avec un enregistrement plus récent du serveur, et comment, par qui et avec quel motif seront-elles résolues ? » Rien n’est écrasé ni résolu automatiquement ici.

## Qui l’utilise
- **Équipage / Capitaine du remorqueur / Capitaine / Chef mécanicien :** pour voir et résoudre leurs propres enregistrements en conflit après un travail hors ligne. Lorsqu’un conflit apparaît, ne supposez pas que votre propre enregistrement l’emporte automatiquement (13.03 §6).
- Un conflit important exige une résolution humaine ; cette page est la cible du flux « Conflit important → Centre des conflits » de 13.03 §10.

## Avant de commencer
- La page ne liste que les conflits que vous avez enregistrés et nécessite un accès en lecture à au moins une source lisible (par exemple affectation de l’équipage, opérations de remorquage, registre quotidien, preuves d’ordre de travail (avec lecture des équipements et de la maintenance), brouillon de renouvellement de certificat). Une seule source lisible suffit pour ouvrir la page ; les permissions listées sont des alternatives.
- La liste et les décisions nécessitent une connexion actuelle au serveur. Hors ligne, vos modifications locales restent conservées sur l’appareil.
- Choisissez le navire ou le remorqueur dans la liste **Navire**.

## Étape par étape
1. Choisissez un navire dans **Navire**. Le tableau s’ouvre avec les colonnes **Source**, **Enregistrement**, **Versions**, **Saisi**, **Reçu par le serveur** et **État**.
2. Si une mention « Non inclus : » apparaît, les sources pour lesquelles vous n’avez pas de permission de lecture ne figurent pas dans la liste.
3. Sur une ligne **Ouvert — décision requise**, appuyez sur **Examiner**.
4. Dans **Examen du conflit**, comparez pour chaque champ les valeurs **Avant la saisie**, **Saisi** et **Serveur**. Aucun choix n’est présélectionné.
5. Pour chaque valeur, choisissez explicitement **Conserver la valeur du serveur** ou **Utiliser la valeur saisie**, remplissez **Motif de résolution (obligatoire)** et appuyez sur **Soumettre la résolution**.
6. Pour supprimer uniquement la copie locale sur cet appareil, cochez la confirmation dans **Abandonner ma modification locale** et appuyez sur **Abandonner la modification locale**.
7. Utilisez **Fermer l’examen** pour revenir à la liste.

## Signification des états
- **Ouvert — décision requise :** le conflit attend une décision humaine.
- **Remplacé par un conflit plus récent :** un conflit plus récent existe pour le même enregistrement ; examinez le nouveau.
- **Résolu par décision explicite :** quelqu’un l’a résolu par une décision motivée. Ce n’est pas une approbation opérationnelle.
- **Withheld / Not provided / Inconnu :** la valeur est masquée par l’autorisation, n’a pas été enregistrée ou n’a pas pu être lue. Une valeur inconnue n’est jamais considérée comme vide ni comme « Non ».
- **Server receipt recorded for your explicit decision :** le serveur a reçu votre décision. Cet accusé de réception n’est pas une approbation opérationnelle.

## Ce que vous ne pouvez pas faire / prudence
- Les conflits ne sont jamais fusionnés ni résolus automatiquement ; chaque valeur exige un choix humain explicite et un motif (13.03 §1).
- Le centre résout directement les conflits d’affectation de l’équipage. Pour les autres sources, il affiche « Résolvez ce conflit depuis le panneau hors ligne de l’enregistrement propriétaire. » ; par exemple, les enregistrements de remorquage se résolvent dans **Opérations hors ligne du remorqueur**.
- Abandonner une modification locale ne supprime que la copie sur l’appareil ; l’enregistrement du serveur reste inchangé et le conflit reste enregistré jusqu’à une décision motivée.
- Une modification saisie sur un autre appareil doit être résolue sur cet appareil ; rien n’est abandonné.
- La soumission revérifie votre accès et les versions de l’enregistrement ; elle n’accorde pas d’approbation opérationnelle.

## En cas de problème
- **Message « Le résultat de la soumission est inconnu » :** avant de soumettre à nouveau, utilisez **Vérifier le résultat de la soumission**.
- **Message « L’enregistrement du serveur a changé après votre examen » / « L’enregistrement du serveur a de nouveau changé » :** votre décision n’a pas été appliquée ; examinez à nouveau le conflit.
- **« Les conflits importants n’ont pas pu être chargés. Rien n’a été résolu. » :** appuyez sur **Réessayer**.
- **« Vous n’avez pas accès au centre des conflits. » / « L’accès aux conflits n’est pas disponible pour ce navire. » :** vérifiez votre rôle et le contexte du navire ; contactez si nécessaire l’administrateur de votre locataire (13.03 §10).

## Pages associées
