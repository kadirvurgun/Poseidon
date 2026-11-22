---
key: towage-execution
title: Exécution du remorquage
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La console d’exécution enregistre sous forme de chronologie l’**exécution réelle** d’une mission de remorqueur soumise : le démarrage, les observations, les actions **Suspendre / Reprendre / Interrompre / Achever** et la vérification ultérieure. Elle répond à la question : « Quand l’opération a-t-elle démarré, quand la remorque a-t-elle été connectée et larguée, quelles observations ont été enregistrées, dans quel état se trouve l’opération et l’enregistrement a-t-il été vérifié ? » Le démarrage et les transitions sont des enregistrements humains du Capitaine de remorqueur ou du Capitaine à bord du remorqueur qui exécute ; la console n’émet jamais de GO.

## Qui l’utilise
- **Capitaine de remorqueur / Capitaine :** pour démarrer l’opération et enregistrer la chronologie Démarrer/Suspendre/Reprendre/Interrompre/Achever et ses preuves (13.03 §3).
- **Vérificateur (une personne disposant de l’autorité de vérification de l’exécution) :** pour vérifier un enregistrement achevé ou interrompu, ou en rejeter la vérification (13.03 §1).
- **Coordinateur des opérations de remorquage / Opérations à terre :** pour suivre l’exhaustivité des preuves et l’historique des missions achevées ; ils ne peuvent ni démarrer ni interrompre une opération (13.03 §7).

## Avant de commencer
- Ouvrez le remorqueur depuis **Opérations de remorqueur** et choisissez la mission dans **Sélectionnez une mission de remorqueur soumise**.
- Pour démarrer, tous les éléments suivants doivent être à jour : l’affectation active du remorqueur, un **Plan de remorquage** approuvé (le remorqueur doit figurer dans le plan), un **Risques et briefing de remorquage** émis et dont chaque participant a accusé réception, et un enregistrement **GO** sur le **Contrôle de préparation opérationnelle** lié à l’évaluation en cours.
- Le démarrage et les transitions exigent une **Désignation** (Capitaine de remorqueur ou Capitaine) enregistrée sur le serveur pour ce remorqueur.

## Étape par étape
1. Dans la section **Contrôle de démarrage**, lisez la **Décision de disponibilité** et les **Points bloquants** de chaque remorqueur.
2. S’il n’y a aucun point bloquant, lisez le texte de confirmation en tant que personne désignée à bord, choisissez votre **Désignation**, saisissez éventuellement une **Position de départ** et appuyez sur **Démarrer l'opération**.
3. Une fois l’exécution ouverte, suivez **État**, **Vérification** et les champs du registre (remorque connectée/larguée, effort de remorquage maximal observé, communication, treuil, test de largage d’urgence, minutes actives).
4. Dans **Enregistrer une observation**, choisissez le type d’observation (par exemple remorque connectée, effort de remorquage, contrôle de communication, météo, état du treuil, test de largage d’urgence, position, preuve, note), saisissez les valeurs et appuyez sur **Enregistrer une observation**.
5. Dans **Actions du capitaine de remorqueur**, choisissez l’**Action** (Suspendre, Reprendre, Interrompre, Achever) et saisissez votre **Désignation** et un **Motif**. L’interruption et l’achèvement exigent un motif d’au moins 20 caractères et ouvrent le champ **Position de fin** ; l’achèvement n’est pas accepté sans position de fin.
6. Si des **Points bloquants pour l'achèvement** sont listés, levez-les ; par exemple, si la remorque est encore connectée, enregistrez d’abord son largage.
7. En tant que vérificateur, rédigez un motif et appuyez sur **Vérifier l'enregistrement** ou **Rejeter la vérification**.
8. Consultez chaque événement dans l’ordre dans la **Chronologie**.

## Signification des états
- **Active / Suspendue / Achevée / Interrompue :** l’état de l’opération. Achevée ne signifie pas que l’enregistrement est vérifié.
- **En attente / Vérifié / Rejeté :** l’état de vérification. Achevé ≠ Vérifié ; la vérification est l’enregistrement d’une autre personne.
- **Bonne / Perdue / Réussi / Échoué / Conditionnel / Inconnu :** le résultat des observations de communication, de treuil et de largage d’urgence. Inconnu indique que rien n’a été enregistré ; il ne remplace jamais « Bonne » ou « Réussi ».
- **Événements de sécurité — Inconnu :** cette console ne compte pas les événements de sécurité ; aucune conclusion « aucun événement » n’en est tirée.
- **GO (enregistrement humain) / NO GO (enregistrement humain) :** la décision enregistrée par le Capitaine de remorqueur ou le Capitaine sur le contrôle de préparation.

## Ce que vous ne pouvez pas faire / prudence
- Les utilisateurs à terre, l’IA et les identités système ne peuvent ni démarrer ni interrompre une opération. Une affectation à terre ne prime jamais sur la décision de sécurité du Capitaine de remorqueur à bord (13.03 §7).
- Seul le Capitaine de remorqueur ou le Capitaine désigné peut suspendre, reprendre, interrompre ou achever. L’interruption reste toujours disponible tant que l’opération est en cours.
- La personne qui a démarré ou terminé l’opération ne peut pas vérifier le même enregistrement ; la vérification a lieu après la fin de l’opération.
- Le registre est en ajout seul ; les événements enregistrés ne sont jamais supprimés ni modifiés.
- Aucune commande n’est envoyée hors ligne. Les observations peuvent être mises en file d’attente dans **Opérations hors ligne du remorqueur** ; le démarrage, les transitions et la vérification restent en ligne.

## En cas de problème
- **Message « Le serveur a listé des éléments manquants ou bloquants. » :** levez les codes dans **Points bloquants** ou **Points bloquants pour l'achèvement** (par exemple plan non approuvé, accusé de réception du briefing en attente, enregistrement GO manquant ou non à jour).
- **Message « L’enregistrement a changé ou la commande est en conflit. » :** utilisez **Recharger la dernière version** pour examiner l’enregistrement.
- **Message « Le résultat est inconnu. » :** **Réessayer** renvoie la même commande à l’identique.
- **Aucune autorisation :** vérifiez votre rôle, votre remorqueur et votre contexte de désignation ; contactez votre administrateur de locataire si nécessaire (13.03 §10).

## Pages associées
