---
key: tug-readiness
title: Contrôle de préparation du remorqueur
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le contrôle de préparation évalue la préparation opérationnelle d’une affectation de remorqueur à partir de contrôles des sources, **à titre d’avis uniquement**, et permet au Capitaine du remorqueur ou au Capitaine désigné d’enregistrer une **décision GO/NO-GO** motivée. Il répond à la question : « Que disent les contrôles d’équipage, d’équipement, de PMS, de certificats, de communication, de météo, de marge de traction au point fixe, d’affectation et de briefing, et quels blocages existent ? » L’évaluation n’est pas une décision : « Le système n’émet jamais de GO ; seul le Capitaine ou le Capitaine du remorqueur désigné enregistre une décision GO/NO-GO avec un motif. »

## Qui l’utilise
- **Capitaine du remorqueur / Capitaine :** pour examiner l’évaluation et les blocages et enregistrer une décision GO ou NO-GO (13.03 §3).
- **Coordinateur des opérations de remorquage / Opérations à terre :** pour suivre la préparation et les blocages ; ils ne décident pas (13.03 §7).

## Avant de commencer
- Ouvrez la page depuis une affectation : sur **Affectation et conflits**, utilisez le lien **Contrôle de préparation opérationnelle** sous une proposition. Sans affectation sélectionnée, la page affiche « Sélectionnez une affectation dans la liste des affectations du travail. »
- L’enregistrement d’une décision exige un accès en écriture remorqueur et une **Désignation** (Capitaine du remorqueur ou Capitaine) enregistrée sur le serveur pour ce remorqueur, et l’affectation doit encore être à l’état Proposé.
- Il n’y a pas de READY sans le briefing : **Risques et briefing de remorquage** doit être émis et chaque participant doit en avoir accusé réception (13.03 §3).

## Étape par étape
1. Lisez le badge d’état sur la ligne **Évaluation**. La note à côté indique « Indicatif, pas une décision GO. »
2. Contrôlez **Traction au point fixe requise (t)**, **Traction au point fixe disponible vérifiée (t)** et **Marge de traction au point fixe (t)**. Une valeur manquante affiche « Inconnu ».
3. Examinez les codes sous **Blocages critiques**, **Motifs d’examen** et **Conditions**.
4. Dans le tableau des catégories, consultez le résultat de chaque catégorie (Équipage, Compétence de remorquage, Équipement, Équipement de remorquage, PMS et défauts, Certificats, Communication, Météo, Marge de traction au point fixe, Affectation, Risques et briefing de remorquage). Pour le détail, lisez les codes de motif dans le tableau **Contrôles des sources**.
5. Si vous détenez l’autorité de décision, choisissez **NO-GO** ou **GO** dans le formulaire **Enregistrer la décision du capitaine**, saisissez votre **Désignation** et un **Motif**, puis appuyez sur **Enregistrer**.
6. Consultez les décisions enregistrées dans la liste **Décisions du capitaine**.

## Signification des états
- **Prêt (READY) :** chaque contrôle des sources est conforme (aucun blocage, inconnu ou condition). C’est une information, non une décision ; la décision GO est toujours enregistrée par le Capitaine du remorqueur ou le Capitaine.
- **Conditionnel (CONDITIONAL) :** les conditions listées dans **Conditions** s’appliquent. GO est désactivé et la page explique : « Cette évaluation est CONDITIONAL. GO exige une évaluation READY : aucune source ne définit quelles conditions permettraient un GO, le serveur le refuse donc. Résolvez les conditions et réévaluez, ou enregistrez un NO-GO. »
- **Non prêt (NOT_READY) :** au moins un blocage critique existe.
- **Examen requis (REVIEW_REQUIRED) :** certaines sources sont inconnues ou attendent un examen. Un contrôle inconnu n’est jamais compté comme « Prêt ».
- **Conforme / Non conforme / Inconnu :** le résultat d’une catégorie ou d’un contrôle individuel.
- **GO / NO-GO :** la décision humaine et le motif enregistrés par le Capitaine du remorqueur ou le Capitaine.

## Ce que vous ne pouvez pas faire / prudence
- Un GO ne peut être enregistré que sur une évaluation READY ; le serveur refuse un GO sur CONDITIONAL, NOT_READY ou REVIEW_REQUIRED. La page n’active GO que sur une évaluation READY ; sinon elle indique « Un GO ne peut être enregistré que sur une évaluation PRÊT. » Un NO-GO peut toujours être enregistré avec un motif.
- Il n’y a pas de READY tant que les risques et le briefing sont incomplets ou que des accusés de réception des participants manquent ; l’option GO est alors désactivée (13.03 §3).
- La proposition d’affectation d’un utilisateur à terre ne prime jamais sur la décision de sécurité du Capitaine du remorqueur à bord (13.03 §7).
- Si l’évaluation change avant votre décision, la décision est refusée ; réexaminez l’évaluation actuelle.
- Les commandes sont désactivées hors ligne.

## En cas de problème
- **Message « L’évaluation a changé » :** utilisez **Recharger**, examinez l’évaluation actuelle et décidez à nouveau.
- **Message « Le résultat est incertain » :** **Réessayer** renvoie la même commande.
- **Préparation inattendue :** ouvrez le tableau des catégories et les motifs des contrôles des sources ; examinez-les avec le responsable du contrôle ou un expert du domaine (13.03 §10).
- **Aucune autorisation :** vérifiez votre rôle, votre remorqueur et votre contexte de désignation ; contactez votre administrateur de locataire si nécessaire (13.03 §10).

## Pages associées
