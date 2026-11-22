---
key: safety-events
title: Événements de sécurité et presque-accidents
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: [1]
reviewed: false
---
## À quoi sert cette page
La page Événements de sécurité et presque-accidents vous permet de signaler rapidement un événement de sécurité sur un navire et de suivre les étapes humaines qui suivent : tri, enquête, revue indépendante, actions correctives et préventives, vérification, efficacité et clôture. Elle répond à la question : « Que s’est-il passé, qui s’en occupe, et l’action a-t-elle réellement fonctionné ? » Rien ici ne décide automatiquement de la gravité, de la cause profonde ou de la clôture ; chacune est un enregistrement humain distinct. La page suit la chaîne Constat → Action → Vérification, et **clôturé n’est pas la même chose qu’efficace** (13.03 §4).

## Qui l’utilise
- **Équipage / OOW / Mécanicien et Capitaine :** signalent les presque-accidents, incidents, accidents, la fatigue, les événements environnementaux ou cyber (13.03 §2, §6).
- **DPA / HSEQ :** en tant que relais de sécurité, surveillent les événements critiques et les actions en retard et gèrent l’enquête et l’efficacité des actions (13.03 §4).
- Le tri, l’enquête, la revue, la lecture de l’identité, la rédaction d’actions, la vérification et la revue d’efficacité sont des permissions distinctes ; chaque utilisateur ne voit que les parties qu’il détient.

## Avant de commencer
- Choisissez le bon navire (**Choisir un navire**). Sans accès, vous voyez « Vous n’avez pas accès aux événements de sécurité de ce navire. »
- Signaler nécessite la permission de signalement ; lire la liste nécessite la permission de lecture.
- Un signalement **Confidentiel** et **Soumettre le rapport** nécessitent une connexion. Hors ligne, vous pouvez seulement enregistrer un brouillon nominatif, et uniquement sur un appareil préparé à l’avance.
- L’heure est saisie en UTC.

## Étape par étape
1. Consultez les événements du navire dans la liste **Événements**. Utilisez **Mes signalements uniquement** pour ne voir que les vôtres.
2. Pour un nouvel enregistrement, appuyez sur **Signaler un événement**. Choisissez **Nominatif** ou **Confidentiel** comme **Mode de signalement**. Pour un signalement confidentiel qui nécessite un suivi, choisissez **Me fournir un code de suivi privé**.
3. Remplissez **Type d’événement**, **Quand cela s’est-il produit ?**, **Opération**, **Emplacement à bord**, **Résumé**, **Ce qui s’est passé**, **Une action immédiate a-t-elle été prise ?** et, facultativement, **Votre estimation de la gravité**. Si l’heure n’est pas connue, choisissez **Inconnu** et remplissez **Pourquoi l’heure est-elle inconnue ?**.
4. Vous pouvez utiliser **Enregistrer le brouillon** tant qu’il est incomplet. Lorsque **Encore requis avant la soumission** est vide, appuyez sur **Soumettre le rapport**. Un brouillon non soumis peut être retiré avec **Retirer le brouillon** et un motif.
5. Si un code de suivi a été émis, conservez immédiatement et en privé le code affiché dans **Votre code de suivi confidentiel** et appuyez sur **Je l’ai conservé**. Il n’est affiché qu’une seule fois. Plus tard, saisissez-le comme **Code de suivi** sous **Suivi confidentiel** et appuyez sur **Ouvrir** pour lire les réponses de l’équipe.
6. Une personne chargée du tri enregistre la gravité et **Enquête requise** avec un motif dans **Tri** et appuie sur **Enregistrer le tri**, ou renvoie le signalement avec **Renvoyer au déclarant**.
7. Un enquêteur appuie sur **Ouvrir une enquête**, remplit **Facteurs contributifs**, **Causes profondes**, **Barrières** et **Incertitudes**, puis appuie sur **Enregistrer l’enquête** et **Soumettre à la revue**. Dans **Revue indépendante**, une autre personne choisit **Approuver**, **Renvoyer** ou **Rejeter**.
8. Dans **Actions correctives et préventives**, utilisez **Ajouter une action** avec un responsable, une échéance et le constat qu’elle traite. Le responsable enregistre **Déclarer la réalisation**, une autre personne enregistre **Vérifier** et une troisième enregistre **Évaluer l’efficacité**.
9. Lorsque **Obstacles à la clôture** est vide, un réviseur (permission de revue) qui n’est ni le déclarant ni un auteur de l’enquête appuie sur **Clôturer l’événement**. Dans **Messages**, vous pouvez envoyer des messages avec la visibilité **Équipe uniquement** ou **Partagé avec le déclarant**.

## Signification des états
- **Draft / Submitted / Returned / Triaged / Investigating / Action tracking / Closed / Withdrawn :** le cycle de vie de l’événement.
- **Severity — Not triaged / Unassessed :** aucune classification humaine pour l’instant. L’**Estimation du déclarant** n’est qu’une suggestion. **Severity policy: not configured – human classification only** indique que le système ne déduit aucune gravité.
- **Besoin d’enquête UNKNOWN :** aucune décision n’a été prise ; cela ne signifie pas « non » et bloque la clôture.
- **Action — Open / Completed / Verified :** Réalisé n’est pas Vérifié, et Vérifié n’est pas Efficace.
- **Effectiveness — Pending / Effective / Not effective / Inconclusive / Aucune action / Not visible :** l’efficacité est une revue humaine par action et est distincte de la clôture. **Aucune action** ne signifie pas efficace.
- Synthèse **Événements de sécurité ouverts** : **Événements de gravité élevée ouverts**, **Revue requise** ou **Aucun événement de gravité élevée ouvert**. C’est uniquement un avertissement ; elle ne donne aucune décision de disponibilité.
- **Confidentiel (protégé) :** l’identité du déclarant se trouve dans un coffre restreint et n’est pas affichée aux lecteurs.
- **Captured offline / Waiting to sync / Saved on this device. Not yet sent.** En file d’attente n’est pas envoyé.

## Ce que vous ne pouvez pas faire / prudence
- Un déclarant ne peut pas trier son propre signalement. Un auteur de l’enquête, la personne qui l’a soumise ou le déclarant ne peuvent pas effectuer la revue indépendante.
- Seul le responsable de l’action déclare sa réalisation. Ni le responsable ni la personne qui l’a réalisée ne peuvent la vérifier ; ni le responsable, ni la personne qui l’a réalisée, ni le vérificateur ne peuvent évaluer l’efficacité. Ce sont des enregistrements distincts par des personnes différentes.
- Les causes profondes sont des déclarations humaines ; le système n’en propose jamais.
- Un signalement soumis ne peut jamais être retiré ni supprimé par son auteur.
- Dans un signalement confidentiel, l’identité n’est ouverte que selon le principe du besoin d’en connaître, par une personne disposant de la permission de lecture de l’identité et choisissant une **Finalité** ; chaque lecture est enregistrée avec sa finalité. Le signalement anonyme nécessite le canal de réception public, qui n’est pas configuré.
- Une revue Inconclusive peut être répétée ; Effective et Not effective sont définitives ; une action qui n’a pas fonctionné nécessite une nouvelle action de suivi.

## En cas de problème
- **Le serveur a refusé la commande :** complétez les éléments manquants ou bloquants listés en dessous.
- **L’enregistrement a été modifié entre-temps. Rechargez et vérifiez avant de réessayer.** Appuyez sur **Recharger**.
- **Le résultat est inconnu (connexion perdue).** Utilisez **Réessayer la même commande** ; elle ne sera pas appliquée deux fois.
- **Cette action ne vous est pas autorisée.** Vérifiez votre rôle, le navire et le contexte de l’objet ; contactez si nécessaire l’administrateur du locataire (13.03 §10).
- **The device key is unavailable** ou **Online re-authorisation is needed :** les données saisies sont conservées ; réautorisez-vous une fois en ligne. Pour une erreur de l’application, contactez le support avec l’identifiant de corrélation, l’écran, l’heure et vos étapes (13.03 §10).

## Pages associées
