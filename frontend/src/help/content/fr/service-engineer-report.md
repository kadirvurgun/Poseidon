---
key: service-engineer-report
title: Rapports de service et d’ingénieur
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Cette page enregistre et liste les **rapports de visites de service et d’ingénieurs** sur le navire sélectionné. Elle répond à la question : « Qui a effectué quel travail sur quel équipement et pour quel ordre de travail, quelles pièces et mesures ont été enregistrées, et le rapport est-il vérifié ? » Un rapport est lié à un identifiant d’équipement, à un ordre de travail et à des défauts ; il est donc rattaché à l’historique de l’ordre de travail et de l’équipement (13.03 §5). Un rapport vérifié est un enregistrement humain ; il ne clôture ni un défaut ni un ordre de travail.

## Qui l’utilise
- **Chef mécanicien / Surintendant technique :** pour lier les rapports à l’historique des équipements et des ordres de travail, suivre ce qui manque et vérifier les rapports dont ils ne sont pas les auteurs (13.03 §5).
- **Mécanicien :** pour saisir un rapport en brouillon après un service et le soumettre à vérification (13.03 §6).

## Avant de commencer
- Si aucun navire n’est sélectionné, la page indique **Choisissez un navire.** et liste les navires de votre périmètre. Sélectionnez le bon navire.
- Vous avez besoin d’un accès en lecture aux rapports de service et aux équipements pour voir les rapports. Le bouton **Nouveau rapport** n’est affiché qu’avec un accès en écriture et en ligne.
- Préparez les identifiants de l’équipement, de l’ordre de travail, des défauts et des fichiers de preuve. Les champs ordre de travail, défaut et preuve ne sont affichés que si vous pouvez lire ces enregistrements.

## Étape par étape
1. Ouvrez un rapport depuis la liste **Rapports**, ou démarrez un nouveau brouillon avec **Nouveau rapport**.
2. Remplissez **N° de rapport**, **Prestataire**, **Mécanicien**, **Date du service**, **Motif et périmètre** et **Travaux effectués**.
3. Si nécessaire, utilisez **Ajouter une pièce** et **Ajouter une mesure** pour saisir les pièces utilisées et les mesures. Si vous ne connaissez pas le résultat d’une mesure, laissez-le à **Inconnu**.
4. Sous **Liens**, saisissez l’**ID de l’équipement**, l’**ID de l’ordre de travail** et les identifiants des défauts et des fichiers de preuve liés (un par ligne).
5. Enregistrez avec **Enregistrer le brouillon**. Si le rapport affiche une liste **Éléments manquants avant soumission**, complétez d’abord ces éléments.
6. Envoyez le rapport avec **Soumettre à vérification**.
7. Sur un rapport soumis dont vous n’êtes pas l’auteur, rédigez un **Motif** d’au moins 10 caractères et utilisez **Enregistrer la vérification** ou **Renvoyer à l’auteur**.
8. Pour modifier un rapport vérifié, rédigez un motif et utilisez **Modifier (nouvelle révision)**. Chaque révision reste dans l’**Historique immuable**.

## Signification des états
- **Draft / Submitted / Returned :** le cycle de vie du rapport. Soumettre n’est pas vérifier.
- **Verified (recorded human decision) / Verified by a non-author :** une personne autorisée qui n’a ni rédigé ni soumis le rapport a enregistré une vérification. Terminé et Vérifié ne sont pas la même chose.
- **En attente de vérification :** le rapport a été soumis et n’est pas encore vérifié.
- **Une révision antérieure a été vérifiée, pas celle-ci :** après une modification, la nouvelle révision doit à nouveau être vérifiée.
- **current revision / changed since linked / unavailable :** l’état d’un équipement, d’un ordre de travail ou d’un défaut lié par rapport à la révision qui a été liée.
- **Transfert accepté, pas une preuve vérifiée :** un fichier joint n’est qu’un transfert accepté.
- Un résultat de mesure **Inconnu** ne signifie pas « Dans les limites ».

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas vérifier un rapport que vous avez rédigé ou soumis ; le serveur le refuse.
- Un rapport vérifié ne clôture ni un défaut ni un ordre de travail, et il ne montre pas que l’équipement est sûr.
- Les originaux des preuves ne sont pas modifiés ; une modification ouvre une nouvelle révision et l’ancienne révision reste dans l’historique (13.03 §1).
- Rien n’est enregistré hors ligne. Lorsque la connexion revient, **Réessayer la même commande** envoie à nouveau la même commande.

## En cas de problème
- **Des données ou enregistrements requis manquent :** complétez la liste des éléments manquants et enregistrez à nouveau.
- **Quelqu’un d’autre a modifié cet enregistrement :** utilisez **Recharger** pour ouvrir la dernière version, puis enregistrez à nouveau.
- **Vous n’avez pas la permission serveur pour cette action ou cet enregistrement :** vérifiez votre rôle et le contexte du navire ; contactez si nécessaire l’administrateur de votre locataire (13.03 §10).
- **Un rapport ne peut pas être vérifié :** contrôlez les enregistrements liés et les fichiers de preuve ; adressez-vous à un vérificateur autorisé (13.03 §10).
- **L’enregistrement n’a pas pu être chargé :** envoyez au support l’écran, l’heure et vos étapes (13.03 §10).

## Pages associées
