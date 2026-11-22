---
key: defects
title: Défauts et mesures de protection
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page des défauts et des mesures temporaires contient les **défauts, restrictions et mesures temporaires déclarées enregistrés** du navire sélectionné. Elle répond à la question : « Quelle est la criticité de ce défaut, quelle mesure de protection ou restriction opérationnelle est enregistrée, l’action corrective a-t-elle été vérifiée, et qu’est-ce qui bloque encore la clôture ? » Chaque étape est un enregistrement humain ; la page n’accorde aucune autorité de sécurité ou d’exploitation.

## Qui l’utilise
- **Chef mécanicien / Surintendant technique :** pour enregistrer la mesure de protection ou la restriction et l’impact d’un défaut critique sur la disponibilité, et pour suivre l’action corrective (13.03 §5).
- **Capitaine :** pour voir l’impact d’un défaut en termes de restriction opérationnelle (13.03 §2).
- **Mécanicien / équipage :** pour déclarer un défaut qu’il a constaté (13.03 §6).

## Avant de commencer
- La lecture requiert `defect.read` et `equipment.read`. Les déclarations et les enregistrements du cycle de vie requièrent `defect.write`.
- La vérification requiert `defect.verify`, l’efficacité `defect.effectiveness` et la clôture `defect.close`, chacune avec un utilisateur humain connecté.
- Choisissez le bon navire dans **Sélectionner un navire ou un remorqueur**.

## Étape par étape
1. Choisissez le navire. Si nécessaire, filtrez **Déclarations enregistrées** avec **Filtrer les observations d’expiration renvoyées** et ouvrez une déclaration.
2. Pour un nouveau défaut, appuyez sur **Nouvelle déclaration de défaut**, renseignez les champs et appuyez sur **Enregistrer la déclaration non évaluée**. Les brouillons incomplets peuvent être enregistrés.
3. Sur une déclaration enregistrée, appuyez sur **Démarrer le cycle de vie** dans la section **Cycle de vie du défaut**.
4. Utilisez **Enregistrer l’évaluation** pour choisir la criticité et rédiger la déclaration (au moins 10 caractères).
5. Utilisez **Enregistrer une mesure ou restriction** et choisissez **Mesure de protection** ou **Restriction opérationnelle** comme **Type de mesure**. Pour une mesure temporaire, saisissez l’expiration (UTC).
6. Utilisez **Enregistrer une action corrective** pour décrire le travail effectué.
7. Une personne autorisée qui n’a enregistré aucune action corrective enregistre **Enregistrer la vérification** puis **Enregistrer l’efficacité**.
8. Lorsqu’il ne reste aucun **Obstacles à la clôture**, une personne autorisée qui n’a, elle non plus, enregistré aucune action corrective enregistre **Clore et remettre en service**. Les enregistrements apparaissent sous **Enregistrements du cycle de vie**.

## Signification des états
- **Detect / Assess / Safeguard / Action / Verify / Effectiveness :** l’étape du cycle de vie.
- **Clos (décision humaine enregistrée) :** la clôture est une décision humaine ; ce n’est pas un résultat vert « réparé ».
- **Règle critique applicable :** un défaut critique ne peut pas être clos sans mesure de protection.
- **Active / Expired — blocking / Lifted / Ended at closure :** l’état d’une mesure de protection ou d’une restriction. Une mesure temporaire expirée doit être renouvelée ou levée.
- **Critical defect without an active safeguard / Criticality unknown and no safeguard :** des alertes. Ces défauts apparaissent aussi sous **Alertes d’état** dans le [Centre de commande PMS](/app/pms-command-center).
- **Inconnu :** la criticité n’a pas été évaluée. Inconnu ne signifie ni « Faible » ni « Non critique ».

## Ce que vous ne pouvez pas faire / prudence
- Si vous avez enregistré l’action corrective, vous ne pouvez pas enregistrer la vérification, l’efficacité ou la remise en service ; ces étapes requièrent leur propre autorité et une autre personne.
- Vérifié, Efficace et Clos sont des enregistrements humains distincts. Une étape ou une mesure déclarée n’autorise pas la mise en service, ne vérifie pas l’efficacité et ne permet pas la clôture.
- Si la déclaration change après la vérification, la clôture est bloquée et l’enregistrement revient en examen.
- La criticité de l’équipement n’est pas reprise comme évaluation de ce défaut.
- Hors ligne, les sources actuelles des défauts sont indisponibles ; il n’existe pas de stockage hors ligne.

## En cas de problème
- **Quelqu’un d’autre a modifié cet enregistrement :** utilisez **Recharger** pour lire l’enregistrement actuel et enregistrez de nouveau.
- **Le résultat de l’enregistrement est inconnu :** **Réessayer la même commande** envoie la même commande.
- **Vous ne disposez pas de l’autorisation serveur pour cette action ou cet enregistrement :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Obstacles à la clôture :** complétez l’enregistrement manquant dans la liste. Si un enregistrement ne peut pas être vérifié, contrôlez la preuve avec le vérificateur (13.03 §10).

## Pages associées
