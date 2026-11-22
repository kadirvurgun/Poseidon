---
key: role-uat
title: Enregistrements UAT par rôle
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Enregistrements UAT par rôle enregistre les sessions de tests d’acceptation utilisateur (UAT) avec les rôles navire et remorqueur, leurs résultats de scénarios et leurs constats comme **preuves humaines**, et les compare au seuil convenu. Elle répond à la question : « Les preuves UAT attestées pour chaque rôle et chaque scénario atteignent-elles le seuil convenu par les humains ? » L’avis en haut de page s’applique : « N’enregistre que des preuves UAT humaines. Le système n’accepte jamais l’UAT et ne décide jamais d’un GO du pilote ; les résultats de tests automatisés sont des pièces jointes, pas un PASS. »

## Qui l’utilise
- **Animateur UAT :** ouvre une session, enregistre les résultats de scénarios observés et les constats du participant, et soumet la session à attestation. Les rôles de participants navire comprennent Capitaine, DPA, Chef mécanicien et Équipage (13.03 §2, §4, §5, §6).
- **Attestateur :** une personne autre que l’animateur atteste la session.
- **Responsables UAT / Product Owner :** enregistrent le seuil convenu et utilisent le résultat sur la page **Revue go/no-go du pilote**.

## Avant de commencer
- La consultation nécessite la lecture à l’échelle du locataire et la lecture UAT. L’enregistrement des sessions et des résultats, l’enregistrement du seuil et l’attestation nécessitent chacun leur propre permission.
- Les responsables UAT doivent avoir convenu du seuil au préalable ; le système n’en fixe jamais.
- Choisissez un pseudonyme pour le participant. N’utilisez jamais de nom, d’adresse e-mail ni de numéro d’équipage.
- L’enregistrement nécessite une connexion active ; il est désactivé hors ligne.

## Étape par étape
1. Choisissez **Navire** ou **Remorqueur** avec les boutons **Mode d’exploitation**.
2. Dans **Preuves UAT par rapport au seuil convenu**, examinez l’état, les nombres de **Bloquants ouverts**, **Constats majeurs ouverts**, **Sessions attestées** et **En attente d’attestation**, ainsi que le tableau des scénarios.
3. Si aucun seuil n’existe, remplissez **Taux de réussite minimal par scénario**, **Nombre maximal de constats majeurs ouverts**, **Conformité au design**, **Référence de l’accord** et **Justification** dans **Enregistrer le seuil convenu** et appuyez sur **Enregistrer**.
4. Dans **Sessions UAT**, utilisez le formulaire **Nouvelle session UAT** pour saisir **Rôle**, **Environnement**, **Commit de build** et **Pseudonyme du participant**, puis appuyez sur **Enregistrer**.
5. Ouvrez la session. Avec **Enregistrer le résultat**, choisissez le **Scénario** et le **Résultat** ; ajoutez si pertinent **Aide apportée** et une **Référence de preuve automatisée**. Pour un problème, utilisez **Enregistrer un constat** avec **Gravité** et **Résumé du constat**.
6. Lorsque vous avez enregistré les scénarios, appuyez sur **Soumettre la session à attestation** (animateur uniquement). Les scénarios sans résultat comptent comme **Non exécuté**. Une personne autre que l’animateur rédige une **Déclaration d’attestation** dans **Attester la session** et appuie sur **Enregistrer**.
7. Dans **Constats ouverts**, clôturez un constat avec une **Résolution** et une **Note de résolution**, puis appuyez sur **Résoudre**.

## Signification des états
- **Seuil non défini :** « Aucun seuil convenu par des humains n’est enregistré. L’UAT ne peut pas réussir tant qu’un seuil n’est pas enregistré. »
- **Criteria not met / Evidence incomplete :** les preuves attestées n’atteignent pas le seuil, ou tous les rôles et scénarios ne sont pas couverts.
- **Critères satisfaits ; décision humaine en attente :** le meilleur état possible. Ce n’est ni une acceptation de l’UAT ni un GO du pilote ; la décision est humaine.
- **Non exécuté :** aucun résultat n’existe pour le scénario. Un scénario qui n’a pas été exécuté ne compte jamais comme réussi.
- **Open / Submitted / Attested :** l’état de la session. Seules les sessions attestées comptent pour le seuil.
- **Passed / Failed / Blocked / Not run :** le résultat de scénario observé par le participant.
- **Blocker / Major / Minor :** la gravité du constat. Un bloquant ouvert fait échouer le jalon.
- **Jeu fictif :** un enregistrement d’exemple synthétique ; ce n’est pas une preuve UAT réelle.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas attester une session que vous avez animée : « L’attestation doit être enregistrée par un humain différent de l’animateur. »
- Vous ne pouvez pas résoudre un constat que vous avez signalé. Un bloquant ne se clôt que comme **Résolu (corrigé et retesté)** ; **Risque accepté** n’est pas proposé.
- Un résultat automatisé (Playwright) n’est qu’une preuve jointe ; le résultat est celui observé par le participant.
- Le système n’accepte jamais l’UAT, ne fixe jamais de seuil et ne fabrique jamais d’attestations ni de validations.

## En cas de problème
- **Vous n’avez pas accès à ces enregistrements.** Vérifiez votre rôle et le périmètre du locataire et contactez l’administrateur du locataire (13.03 §10).
- **L’enregistrement est introuvable dans votre locataire.** Vérifiez le lien de la session et le mode sélectionné.
- **L’enregistrement a changé ou a déjà été utilisé. Rechargez.** Ouvrez l’état actuel avec **Recharger**.
- **Le résultat est incertain. Réessayer envoie la même commande.** Utilisez **Réessayer** pour renvoyer la même commande en toute sécurité.
- **Refusé par les règles.** Par exemple, le scénario n’appartient pas au rôle de la session, ou un bloquant ne peut pas être clôturé comme risque accepté.
- **Non autorisé.** Par exemple, vous avez animé cette session et ne pouvez pas l’attester, ou vous avez signalé ce constat et ne pouvez pas le résoudre.
- **Vous êtes hors ligne. L’enregistrement est désactivé jusqu’au retour de la connexion.** Lorsqu’elle revient, appuyez sur **Actualiser**.

## Pages associées
