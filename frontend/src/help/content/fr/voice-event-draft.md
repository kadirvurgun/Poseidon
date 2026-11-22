---
key: voice-event-draft
title: Brouillon d’événement vocal
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Brouillon d’événement vocal transforme une courte note dictée ou saisie en **suggestions de brouillon** pour un rapport d’événement de sécurité. Elle répond à la question : « Le type d’événement, le lieu, l’heure, ce qui s’est passé et la mesure immédiate ont-ils été correctement tirés de ce que j’ai dit ou écrit ? » Les suggestions proviennent de règles locales fixes, chacune rattachée à vos propres mots avec un niveau de confiance. Une personne modifie et confirme le brouillon ; rien n’est soumis, clôturé ni signalé automatiquement (13.03 §1, §6).

## Qui l’utilise
- **Équipage / OOW / Mécanicien et Capitaine :** prennent une note rapide sur le terrain et lancent un rapport d’événement de sécurité (13.03 §2, §6).
- **DPA / HSEQ :** suivent **Utilisabilité mesurée (ce navire)**. Ces chiffres sont une observation opérationnelle, pas une affirmation d’exactitude.

## Avant de commencer
- Choisissez un navire de votre périmètre. La création d’un brouillon exige l’autorisation d’écriture des brouillons vocaux et un périmètre de modification sur ce navire.
- La remise d’un brouillon confirmé à un brouillon d’événement de sécurité exige aussi l’autorisation de signaler les événements de sécurité.
- Confidentialité : l’audio n’est pas stocké. La parole n’utilise qu’un moteur local sur cette installation ; s’il n’y en a aucun, saisissez ou collez la note. Évitez les noms et les données de santé personnelles.
- Hors ligne, rien n’est envoyé.

## Étape par étape
1. Appuyez sur **Nouvelle note vocale ou saisie**.
2. Si un moteur local est configuré, appuyez sur **Enregistrer avec le moteur local**, puis sur **Arrêter et transcrire**. Sinon, saisissez une courte note dans **Texte de la note** : quoi, où, quand, ce que vous avez fait.
3. Vérifiez la **Date de la note**. Elle sert uniquement à interpréter des mots comme « aujourd’hui » ou « hier ».
4. Appuyez sur **Créer les suggestions de brouillon**.
5. Vérifiez chaque champ : **Type d’événement**, **Quand (heure locale)**, **Emplacement à bord**, **Précision du lieu**, **Ce qui s’est passé**, **Mesure immédiate prise**. Sous chaque champ, vous voyez le niveau de confiance de la suggestion, la règle et la partie correspondante de votre note. Lisez aussi **Remarques des règles**.
6. Corrigez les suggestions erronées et appuyez sur **Enregistrer les modifications**. Lorsque **Encore requis avant confirmation :** est vide, appuyez sur **Confirmer le brouillon**. La confirmation enregistre que vous avez vérifié chaque champ ; elle ne soumet ni ne clôture aucun événement.
7. Après la confirmation, si un brouillon d’événement de sécurité a été créé, suivez **Ouvrir le brouillon d'événement de sécurité** vers la page Événements de sécurité et presque-accidents, et complétez-le et soumettez-le là-bas. S’il n’en existe pas encore et que vous détenez l’autorisation, utilisez **Créer maintenant le brouillon d'événement de sécurité**.
8. Si la note n’est pas un événement ou est erronée, choisissez un **Motif**, ajoutez si nécessaire **Note (obligatoire pour Autre)** et appuyez sur **Rejeter le brouillon**.

## Signification des états
- **Brouillon (non confirmé) :** aucune personne n’a encore confirmé les suggestions.
- **Confirmé par une personne :** les champs ont été vérifiés ; ce n’est pas un rapport d’événement de sécurité et rien n’a été soumis.
- **Rejeté :** le motif du rejet est enregistré pour évaluation.
- **Suggéré, inchangé / Modifié par vous / Saisi par vous / Vide :** l’origine d’un champ. Suggéré, inchangé est une suggestion qu’une personne doit encore vérifier.
- **Saisi ou collé / Moteur vocal local :** la manière dont la note a été saisie. Modifier un texte issu du moteur local le transforme en note saisie.
- Des remarques de règles comme **Heure sans jour ; aucune date supposée** signalent une information manquante ; le système ne devine pas la valeur manquante.
- Une mention comme **2 manquants** : le nombre de champs obligatoires du brouillon encore vides dans la liste.

## Ce que vous ne pouvez pas faire / prudence
- Les suggestions ne sont qu’un brouillon ; un niveau de confiance n’est pas une garantie d’exactitude. Vérifiez vous-même chaque champ avant de confirmer.
- Cette page ne soumet, ne clôture ni ne signale aucun événement de sécurité. Vous soumettez le rapport sur la page Événements de sécurité et presque-accidents.
- Si des preuves sont mentionnées, joignez-les vous-même ; la page ne joint aucune preuve.
- **Confirmer le brouillon** n’est pas disponible tant que des modifications ne sont pas enregistrées ; enregistrez-les d’abord.
- Sans l’autorisation de signaler les événements de sécurité, aucun brouillon d’événement de sécurité n’est créé à partir d’un brouillon confirmé.

## En cas de problème
- **Capacité vocale indisponible : aucun moteur local n’est configuré.** ou **La transcription n’a pas abouti.** Saisissez ou collez la note.
- **Accès au microphone refusé.** Vérifiez l’autorisation du navigateur ou saisissez la note.
- **The record changed meanwhile.** Appuyez sur **Recharger** et refaites votre modification.
- **Vous êtes hors ligne. Rien n’a été envoyé.** Une fois en ligne, utilisez **Réessayer la même commande**.
- **Vous n’avez pas l’autorisation pour cette action.** Vérifiez votre rôle et votre contexte de navire ; si nécessaire, contactez l’administrateur de locataire (13.03 §10).

## Pages associées
