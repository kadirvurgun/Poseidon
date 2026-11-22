---
key: fuel-bunker-log
title: Combustible, soutage et ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page combustible, soutage et ROB conserve les **déclarations de combustible et de soutage** du navire sélectionné sous forme de brouillons non vérifiés, avec leur historique de source. Elle répond à la question : « Quel combustible a été reçu ou consommé, quel ROB avons-nous déclaré avant et après, quelles références de BDN, de fournisseur, d’échantillon et de citerne s’y rapportent, et la chaîne des ROB déclarés se poursuit-elle d’un enregistrement au suivant ? » La page ne corrige pas les quantités, ne calcule pas les émissions et n’établit ni conformité ni vérification.

## Qui l’utilise
- **Chef mécanicien / officiers mécaniciens :** pour enregistrer les déclarations de soutage, de consommation et de ROB avec leurs références de preuve (13.03 §5).
- **Capitaine :** pour suivre les déclarations de combustible à côté des synthèses de midi examinées par le capitaine (13.03 §2).

## Avant de commencer
- Dans la barre de contexte supérieure, sélectionnez la **bonne société et le bon navire**. Si aucun navire n’est sélectionné, la page affiche « Sélectionner un navire ».
- La lecture requiert `operations.fuel.read`. La création et l’enregistrement requièrent `operations.fuel.write` et des droits de modification sur ce navire.
- Les heures doivent comporter la date, les secondes et Z ou un décalage UTC explicite ; une heure locale sans fuseau horaire n’est pas acceptée.

## Étape par étape
1. Choisissez le navire. **Relevés de combustible et de ROB** liste les enregistrements renvoyés. Utilisez **Filtrer la page renvoyée** pour restreindre cette page uniquement.
2. Lisez **Contrôle de continuité ROB** sous la liste. Il affiche le bilan (avant + reçu − consommé − après), **Continuité avec l’enregistrement précédent** et **Signalements de revue** par type de combustible déclaré.
3. Lisez **Synthèses combustible et ROB des Noon reports revues par le capitaine** à titre de contexte. Elles sont affichées sous forme de texte déclaré et ne sont pas copiées dans le registre.
4. Appuyez sur **Nouvel enregistrement provisoire**. Renseignez **Contexte d’enregistrement et de livraison**, **Déclarations de quantités et de ROB** et **Propriétés du combustible et relevé d’échantillon**. Pour chaque champ, conservez une valeur déclarée, **Manquant** ou **Inconnu**.
5. Appuyez sur **Enregistrer le brouillon non vérifié**. Chaque enregistrement ajoute une révision à **Historique des révisions enregistrées**.
6. Pour partir d’un rapport de midi ou d’un journal machine enregistré, utilisez **Ouvrir un relevé de combustible depuis la source enregistrée** sur cette page. La **Déclaration quotidienne liée** reste ici en lecture seule.

## Signification des états
- **Draft declaration / Unverified draft record :** une personne a déclaré les valeurs ; rien n’est vérifié.
- **Revue requise :** l’état d’examen de conformité de chaque enregistrement ; la conformité n’a pas été évaluée.
- **En attente ; vérification non établie :** l’état d’examen de vérification ; aucune vérification n’existe.
- **Aucun écart détecté :** les chiffres déclarés concordent. Ce n’est pas une vérification.
- **Inconnu :** la continuité ne peut pas être évaluée, par exemple lorsqu’une valeur manque ou qu’un historique plus ancien se situe hors de la fenêtre évaluée. Inconnu ne signifie ni « Aucun écart » ni « Prêt ».
- **Manquant / Inconnu :** distincts de zéro.

## Ce que vous ne pouvez pas faire / prudence
- L’examen humain, l’approbation opérationnelle ou juridique, la finalisation et la saisie hors ligne ne sont pas disponibles sur cette page.
- Le système signale les différences pour examen humain ; il ne corrige jamais une quantité et ne décide jamais de la conformité du combustible.
- Seuls les 100 enregistrements les plus récents sont évalués. Sans référence de citerne, un écart sur un navire à plusieurs citernes est une question de données de citerne manquantes, et non une erreur de données.
- Les quantités du rapport de midi ou du journal machine dont l’unité n’est pas résolue ne sont jamais converties en valeurs reçues, consommées ou de ROB.

## En cas de problème
- **« The saved source changed. » :** vos saisies sont conservées ; rechargez ou abandonnez explicitement avant d’enregistrer de nouveau.
- **« Outcome unknown. » :** utilisez **Réessayer la commande opérationnelle exacte** ; ne ressaisissez rien.
- **« Check the declared values, explicit time offsets and current source permission. » :** corrigez le champ signalé ou laissez-le à Manquant ou Inconnu.
- **Source opérationnelle ou accès indisponible :** utilisez **Réessayer la lecture de la source** ; si l’accès reste refusé, vérifiez votre rôle et votre contexte de navire avec votre administrateur de locataire (13.03 §10).

## Pages associées
