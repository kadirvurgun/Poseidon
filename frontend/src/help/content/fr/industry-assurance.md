---
key: industry-assurance
title: Assurance sectorielle (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Assurance sectorielle importe depuis un classeur les résultats d’inspection SIRE 2.0 d’un pétrolier, soumet les modifications à un examen humain, lie les constats à des actions de sécurité et enregistre des preuves terrain. Elle répond à la question : « Quels constats de cette inspection sont ouverts, lesquels ont une action liée, et où se trouve la preuve ? » Les importations sont des brouillons : une autre personne accepte chaque élément modifié. Les constats restent ouverts et non vérifiés ; cette page ne les clôt ni ne les vérifie jamais. La chaîne est Constat → Action → Vérification, et la vérification et l’efficacité sont des enregistrements humains distincts sur l’action dans Événements de sécurité et quasi-accidents (13.03 §4, §8).

## Qui l’utilise
- **DPA / HSEQ :** lie les constats d’inspection à des actions et suit la chaîne Constat → Action → Vérification (13.03 §4).
- **Utilisateur Réglementaire / Assurance :** gère les correspondances de colonnes et prépare ou examine les importations (13.03 §8).
- **Capitaine et personnel du navire :** enregistrent des preuves terrain (photos) pour un constat.

## Avant de commencer
- Choisissez un navire. SIRE 2.0 ne s’applique qu’aux pétroliers. Si le type de navire n’est pas déclaré dans le profil du navire, les importations restent en **revue requise**.
- La lecture requiert l’autorisation de lecture des inspections. L’importation, l’acceptation d’une importation, l’écriture de preuves, la liaison de constats et l’écriture de correspondances sont des autorisations distinctes. La liaison d’une action requiert aussi l’autorisation de lecture des événements de sécurité ; l’enregistrement de preuves requiert aussi la lecture des fichiers.
- Le schéma de colonnes est synthétique ; le format OCIMF sous licence n’est pas utilisé.
- Hors ligne, seules les prises de photos sont mises en file d’attente sur cet appareil ; les importations et les décisions nécessitent une connexion.

## Étape par étape
1. **Choisir un navire** et vérifiez la ligne **Applicabilité**.
2. Si nécessaire, dans **Correspondances de colonnes**, saisissez une **Clé de correspondance**, écrivez une ligne par colonne (en-tête=champ) et appuyez sur **Enregistrer comme nouvelle version**.
3. Dans **Préparer une importation**, choisissez **Version de correspondance**, **Référence d’inspection**, **Date d’inspection** et **Classeur (.xlsx)**, puis appuyez sur **Soumettre à la revue**.
4. Une autre personne, dans **Revue de l’importation**, choisit **Accepter** ou **Conserver l’actuel** pour chaque élément modifié, rédige un **Motif** d’au moins 10 caractères et appuie sur **Accepter l’importation** ou **Rejeter l’importation**.
5. Ouvrez une inspection depuis la liste **Inspections**. Examinez **Éléments** et **Constats**.
6. Pour lier un constat à une action, saisissez l’**Identifiant de l’action de sécurité** et appuyez sur **Lier l’action**.
7. Pour une preuve terrain, saisissez **Média**, **Identifiant du fichier sain**, **Révision du fichier** et **Empreinte du fichier (SHA-256)** et appuyez sur **Enregistrer une preuve terrain**. Hors ligne, le bouton devient **Enregistrer sur cet appareil** ; une fois en ligne, envoyez la preuve avec **Envoyer maintenant**.

## Signification des états
- **Tanker: applicable / Not applicable / Ship type unknown: review required :** avec un type de navire inconnu, le résultat reste **Revue requise** ; il n’est jamais présumé sans objet.
- **Awaiting review / Accepted / Rejected :** l’état de l’importation.
- **New / Changed / Unchanged / Not in this import :** l’élément comparé à l’importation acceptée précédemment. Un élément absent de cette importation est **Conservé (jamais supprimé)**.
- **Negative observation / Positive observation / No observation / Inconnu :** l’observation portant sur un élément.
- **Open / Unverified :** ici, les constats apparaissent toujours comme ouverts et non vérifiés.
- **No action linked / Action linked :** indique si le constat est lié à une action de sécurité. Une action liée ne signifie pas que le constat est vérifié ni que l’action est efficace.
- **Captured online / Captured offline :** la manière dont la preuve a été saisie.

## Ce que vous ne pouvez pas faire / prudence
- La personne qui a préparé une importation ne peut pas l’examiner : « Vous avez préparé cette importation ; une autre personne doit la revoir. »
- Cette page ne clôt ni ne vérifie les constats. Vérifié n’est pas Efficace ; l’efficacité est un examen humain distinct sur l’action.
- Les preuves vidéo sont indisponibles tant que l’opérateur n’a pas activé un type de fichier vidéo analysé.
- Un élément absent d’une importation ultérieure n’est jamais supprimé ; l’historique est conservé.

## En cas de problème
- **La demande a été refusée :** résolvez les motifs listés en dessous.
- **L’enregistrement a changé entre-temps. Rechargez et vérifiez à nouveau.** Appuyez sur **Recharger**.
- **Le résultat est inconnu (connexion perdue).** Utilisez **Renvoyer la même requête**.
- **Vous n’êtes pas autorisé à lire les dossiers d’assurance sectorielle.** ou **Vous n’êtes pas autorisé à faire cela.** Vérifiez votre rôle et votre contexte de navire ; si nécessaire, contactez l’administrateur du locataire (13.03 §10).
- Si une preuve ne peut pas être vérifiée, contrôlez l’identifiant du fichier, la révision et l’empreinte SHA-256 ; si nécessaire, contactez le vérificateur (13.03 §10).

## Pages associées
