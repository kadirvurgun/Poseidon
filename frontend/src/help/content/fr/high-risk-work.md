---
key: high-risk-work
title: Travaux à haut risque
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Travaux à haut risque affiche dans une seule liste **en lecture seule** les exceptions relatives aux travaux de la flotte tels que les travaux par point chaud, les espaces clos, les travaux en hauteur, l’isolation électrique, la maintenance critique, le remorquage et le soutage. Elle répond à la question : « Quel travail à haut risque présente un problème ouvert concernant le risque, le permis, une barrière critique, l’équipage ou l’équipement, et qui détient la prochaine action ? » Chaque dimension est affichée séparément ; il n’existe pas de score global. Les actions ouvrent l’enregistrement propriétaire ; cette page ne modifie aucun enregistrement et ne prend aucune décision de début des travaux (13.03 §4).

## Qui l’utilise
- **DPA / HSEQ :** dans le cadre de la gouvernance des risques, surveille les exceptions de travaux à haut risque dans toute la flotte (13.03 §4).
- **Capitaine et capitaine du remorqueur :** voient les blocages des travaux à haut risque sur leur propre navire et accèdent à l’enregistrement propriétaire. La décision de commencer ou d’arrêter les travaux leur appartient (13.03 §2, §3).

## Avant de commencer
- La consultation requiert les autorisations de lecture des risques et de lecture des travaux à haut risque. Sans elles, vous voyez « Vous n'avez pas accès à la vue des travaux à haut risque. »
- La liste ne couvre que les flottes et les navires de votre périmètre.
- Hors ligne, la liste affichée peut être obsolète.

## Étape par étape
1. Choisissez les filtres **Flotte**, **Navire** et **Type de travail**. **Tous** supprime un filtre.
2. Dans le tableau, lisez séparément pour chaque travail les états **Risque**, **Permis**, **Barrière critique**, **Équipage** et **Équipement**. Les badges **Blocages critiques** et **Éléments à examiner** affichent des nombres.
3. Utilisez les colonnes **Action suivante** et **Responsable / échéance** pour voir qui doit faire quoi.
4. Pour voir pourquoi une ligne est dans son état, appuyez sur **Pourquoi**. Un tableau **Dimension**, **État** et **Motif** s’ouvre ; fermez-le avec **Masquer**.
5. Pour modifier quoi que ce soit, suivez le lien du travail ou **Ouvrir l’enregistrement** ; l’action est effectuée sur la propre page de l’enregistrement propriétaire.
6. Naviguez entre les pages avec **Précédent** et **Suivant**. **Évalué le** et **Couverture des sources** indiquent quand et à partir de quelles sources la liste a été calculée.

## Signification des états
- **Ready / Conditional / Attention / Not ready / Blocked :** l’état de la dimension dans sa source. Une dimension Prête ne signifie pas que le travail peut commencer ; la décision reste celle de la personne autorisée.
- **Revue requise :** une personne doit l’examiner.
- **Inconnu :** la source est indisponible ; ce n’est jamais prêt. POSEIDON ne dispose pas encore de source de permis de travail ; la colonne **Permis** affiche donc toujours **Inconnu**.
- **Healthy / Degraded / Failed :** l’état de la barrière critique.
- **Non classé :** le type de travail n’est pas connu ou sa criticité n’est pas déterminée ; il n’est pas exclu de la liste.
- **Waiting for source / Resolve stop or dispute / Renew expired item / Complete required item / Reassess / Open owner record :** la prochaine action, affichée selon un ordre de priorité fixe.
- **Due unresolved / Not planned :** la date d’échéance ou le début prévu n’est pas connu.

## Ce que vous ne pouvez pas faire / prudence
- Cette page est en lecture seule ; elle ne modifie, n’approuve et n’autorise rien.
- Il n’existe pas de score global ; un travail comportant un blocage critique est toujours trié en tête, et le bon état des autres dimensions ne le masque jamais.
- Ne lisez pas **Inconnu** comme prêt. Un permis actif historique n’est jamais un permis valide en cours.
- La décision de commencer ou d’arrêter les travaux reste celle du capitaine ou du capitaine du remorqueur.

## En cas de problème
- **Le service est indisponible. Réessayez.** Appuyez sur **Réessayer**.
- **Vous êtes hors ligne. La liste affichée peut être obsolète.** Une fois en ligne, appuyez sur **Recharger**.
- Si un état semble inattendu, ouvrez le tableau **Pourquoi** et interrogez le responsable de l’enregistrement ou le responsable du contrôle (13.03 §10).
- Sans accès, vérifiez votre rôle et votre contexte de navire ; si nécessaire, contactez l’administrateur du locataire (13.03 §10).

## Pages associées
