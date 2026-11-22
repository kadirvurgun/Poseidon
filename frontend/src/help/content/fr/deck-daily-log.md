---
key: deck-daily-log
title: Journal quotidien du pont
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## À quoi sert cette page
Le journal quotidien du pont conserve les **enregistrements de quart à la passerelle** du navire sélectionné sous forme de brouillons, avec la version de source enregistrée. Il répond à la question : « Qu’a déclaré le quart, pour cette date et cette période, concernant la position, le cap, la vitesse, la météo, la visibilité, la navigation, les opérations portuaires et de cargaison, le ballast, les travaux de pont, les observations de sécurité et la relève, et un examen a-t-il été enregistré pour cette révision exacte ? » Une entrée enregistrée est une déclaration non vérifiée, et non un journal approuvé.

## Qui l’utilise
- **Officier de quart (Crew / OOW) :** pour tenir l’enregistrement du quart en temps réel et dans l’ordre chronologique (13.03 §6).
- **Capitaine ou second capitaine :** pour examiner le journal enregistré et consigner cet examen comme un acte humain (13.03 §2). Seule une désignation portant la fonction de capitaine ou de second capitaine peut enregistrer l’examen d’un journal de pont.

## Avant de commencer
- Dans la barre de contexte supérieure, sélectionnez la **bonne société et le bon navire**. Si aucun navire n’est sélectionné, la page affiche « Sélectionner un navire ».
- La lecture requiert `daily.deck.read`. La création et l’enregistrement requièrent `daily.deck.write` et des droits de modification sur ce navire ; sinon vous voyez « Accès en lecture seule. La modification et l’enregistrement ne sont pas autorisés. »
- L’enregistrement d’un examen requiert `daily.deck.write` avec des droits de modification sur ce navire, `daily.deck.review`, ainsi qu’une désignation en cours de capitaine ou de second capitaine pour cet enregistrement exact.

## Étape par étape
1. Choisissez le navire. **Brouillons enregistrés** liste chaque journal par date et révision avec « Brouillon · examen requis ».
2. Appuyez sur **Nouveau brouillon** pour un **Nouveau brouillon quotidien**, ou ouvrez un journal listé pour **Modifier le brouillon enregistré**.
3. Renseignez d’abord **Contexte du quart** (**Date du journal**, **Quart / période**, **Référence de l’officier de quart**). La liste des officiers ne propose que les personnes disponibles à la date de journal déclarée.
4. Renseignez **Navigation et météo**, **Activités portuaires et cargaison** et **Travaux de pont et relève**. Pour chaque champ, choisissez **Connu**, **Inconnu** ou **Manquant**.
5. Appuyez sur **Enregistrer le brouillon** pendant le quart ou juste après. Chaque enregistrement ajoute une révision à **Historique des révisions enregistrées** ; les révisions antérieures restent inchangées.
6. Le capitaine ou le second capitaine ouvre **Enregistrement d’examen humain**, choisit une **Désignation humaine habilitée** et une **Entrée d’examen**, rédige un **Motif de l’examen** et appuie sur **Enregistrer l’entrée d’examen**.

## Signification des états
- **Draft · declarations unverified / Unverified declaration :** déclaré par une personne ; non vérifié.
- **Connu / Inconnu / Manquant :** l’état des données du champ. Inconnu ne signifie ni « Non », ni « Prêt », ni « Sans objet ».
- **En attente · examen non enregistré :** aucun examen humain n’existe pour cette révision.
- **Métadonnées humaines enregistrées · non évaluées :** une entrée d’examen existe pour cette révision. Elle n’établit ni vérification, ni soumission, ni approbation de sécurité par le capitaine.
- **Record reviewed declaration (unassessed) / Return for correction :** les deux types d’entrée d’examen qu’un examinateur peut enregistrer.
- **Historical revision :** l’examen appartient à une révision antérieure, et non au brouillon actuel.

## Ce que vous ne pouvez pas faire / prudence
- Le système n’examine ni ne finalise jamais le journal. L’examen est un acte humain du capitaine ou du second capitaine désigné (13.03 §2).
- Quiconque a rédigé une révision quelconque du journal ne peut pas l’examiner. Aucune désignation n’est proposée à un auteur et la page indique « You authored this log revision; an independent reviewer must record the review. » ; le serveur refuse également un tel examen au titre de la séparation des tâches.
- Tenez les entrées en temps réel et dans l’ordre chronologique ; ne réécrivez pas un ancien quart pour le faire passer pour un nouveau (13.03 §6).
- Ne modifiez pas l’enregistrement examiné de quelqu’un d’autre ; un enregistrement ultérieur constitue une nouvelle révision et nécessite un nouvel examen (13.03 §6).
- La saisie hors ligne ne couvre que les champs explicitement classés. Un conflit hors ligne n’est jamais fusionné automatiquement ; ne supposez pas que votre enregistrement l’emporte (13.03 §6).

## En cas de problème
- **« The record changed. Your inputs are retained. » :** abandonnez explicitement et rechargez avant une nouvelle tentative.
- **« Save outcome unknown » :** utilisez **Réessayer la commande enregistrée exacte** ; ne ressaisissez pas les champs.
- **Référence d’officier indisponible :** laissez le champ à Manquant ou Inconnu ; un nom saisi ne peut pas remplacer la recherche.
- **« Human conflict resolution required » :** ouvrez **Examiner le conflit** ou le centre des conflits et choisissez explicitement (13.03 §10).
- **Aucune désignation disponible :** vérifiez votre rôle et votre contexte de navire avec votre administrateur de locataire (13.03 §10).

## Pages associées
