---
key: port-activity-sof
title: Activité portuaire et SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Activité portuaire et SOF conserve les **événements portuaires** du navire sélectionné comme un enregistrement opérationnel avec des références de preuve, et permet à une personne désignée de finaliser en interne le **Statement of Facts (SOF)**. Elle répond à la question : « Quels événements portuaires avons-nous déclarés, dans quel ordre et intervalle UTC, avec quelles références de cargaison, de tirant d’eau, de pilote, de remorqueur, de retard et de réserve, et une personne désignée a-t-elle finalisé cette révision du SOF ? » La page ne produit ni signature, ni laytime, ni surestaries, et les événements enregistrés ne sont ni des instructions ni des fenêtres planifiées approuvées.

## Qui l’utilise
- **Capitaine :** pour tenir l’arrivée et l’activité portuaire comme un enregistrement opérationnel avec des preuves et pour finaliser le SOF (13.03 §2).
- **Second capitaine / Chef mécanicien :** pour enregistrer les événements portuaires et, avec une désignation en cours, pour finaliser le SOF.

## Avant de commencer
- Dans la barre de contexte supérieure, sélectionnez la **bonne société et le bon navire**. Si aucun navire n’est sélectionné, la page affiche « Sélectionner un navire ».
- La lecture nécessite `operations.port.read` ; l’enregistrement de brouillons nécessite `operations.port.write` et des droits de modification sur ce navire.
- La finalisation nécessite `operations.port.write`, des droits de modification sur ce navire et `operations.port.finalize`, plus une désignation de finalisation en cours (Capitaine, Second capitaine ou Chef mécanicien) pour ce navire.

## Étape par étape
1. Choisissez le navire. **Chronologie enregistrée des événements portuaires** liste les événements renvoyés dans l’ordre UTC du serveur. **Filtrer la page renvoyée** restreint uniquement cette page.
2. Lisez la **Chronologie du Statement of Facts** sous la liste : chaque événement avec **Intervalle enregistré**, **Motif du retard** et **Remarques**.
3. Appuyez sur **Nouvel enregistrement provisoire**. Remplissez **Contexte d’escale**, **Activité et horaires enregistrés**, **Déclarations de cargaison et de tirant d’eau** et **Assistance, exceptions et preuves**. Les heures des événements nécessitent Z ou un décalage UTC explicite.
4. Appuyez sur **Enregistrer le brouillon non vérifié**. Chaque enregistrement ajoute une révision à l’**Historique des révisions enregistrées**.
5. Sur un enregistrement sauvegardé, ouvrez **Finalisation du Statement of Facts**. Si **Required facts missing** est affiché, complétez d’abord ces champs.
6. Cochez chaque référence non résolue listée pour en prendre acte, choisissez votre **Désignation** et appuyez sur **Finaliser le SOF**. Le système enregistre qui a finalisé, quand, la fonction et le motif.

## Signification des états
- **BROUILLON :** le SOF n’est pas finalisé.
- **FINAL (interne) :** une personne désignée a finalisé cette révision. C’est interne : ni une signature ni un accord externe.
- **Signature non enregistrée :** toujours affiché ; aucune signature n’existe sur cette page.
- **Modifié après FINAL : une nouvelle revue et finalisation sont requises. :** l’enregistrement a été sauvegardé à nouveau après la finalisation et est revenu à BROUILLON.
- **UNKNOWN (unresolved references) :** aucune recherche propriétaire n’existe pour ces références requises. En prendre acte ne les rend pas connues.
- **Review required / Unverified draft record :** les événements portuaires sont des déclarations ; rien n’est vérifié.
- **Draft (not finalized) / Final (internal; not a signature) :** l’état du SOF que le serveur a enregistré pour cette révision, affiché dans le résumé de l’enregistrement. Final ne provient que d’une finalisation humaine enregistrée.

## Ce que vous ne pouvez pas faire / prudence
- Le système ne finalise jamais le SOF. La finalisation est un acte humain d’un Capitaine, Second capitaine ou Chef mécanicien désigné (13.03 §2).
- Finaliser le SOF reste désactivé tant que des faits requis manquent, qu’aucune désignation n’existe ou que des références non résolues n’ont pas été prises en compte.
- La finalisation n’est ni une signature, ni un accord externe, ni un calcul de laytime ou de surestaries.
- Le pied de page indique : « La finalisation du SOF est un acte humain interne enregistré dans le panneau de finalisation par un Capitaine, Second capitaine ou Chef mécanicien désigné. Les signatures, l’approbation opérationnelle/juridique et la saisie hors ligne restent indisponibles. »
- L’enregistrement de brouillons et la finalisation nécessitent une session en ligne en cours ; cette page n’offre aucune saisie hors ligne.

## En cas de problème
- **« Aucune désignation de finalisation en cours pour ce navire. » :** vérifiez votre rôle et le contexte du navire avec l’administrateur de votre locataire (13.03 §10).
- **« Résultat inconnu : réessayer envoie la même commande une fois. » :** utilisez **Réessayer** ; ne lancez pas une nouvelle finalisation.
- **« Commande non acceptée » :** lisez le code affiché, rechargez l’enregistrement avec **Lire l’enregistrement actuellement enregistré** et réessayez.
- **« La source enregistrée a changé. » :** rechargez ou abandonnez explicitement avant d’enregistrer à nouveau.

## Pages associées
