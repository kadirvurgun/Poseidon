---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## À quoi sert cette page
La page Users and permissions gère les **adhésions** (memberships) de votre espace de travail. Elle répond à la question : « Que peut voir et faire cette personne, et sur quels navires ? » Le compte lui-même (nom d’utilisateur, mot de passe, MFA) se trouve dans le fournisseur d’identité central (IdP), pas dans POSEIDON. Cette page **invite** ce compte à une adhésion et accorde des ensembles de rôles, des autorisations et un périmètre de navires (13.03 §9). Un nouveau membre est invité : l’invitation n’accorde rien tant que la personne ne s’est pas connectée et ne l’a pas acceptée.

## Qui l’utilise
- **Tenant administrator (administrateur de locataire) :** attribue le périmètre d’utilisateur, de rôle, de navire et d’objet selon le moindre privilège, invite de nouveaux membres et gère les délégations et la suspension (13.03 §9).
- Les autres utilisateurs disposant d’un accès en lecture aux adhésions peuvent consulter la liste mais pas la modifier.
- La **personne invitée** ouvre seulement le lien d’invitation ; elle ne voit jamais cette page.

## Avant de commencer
- La consultation exige la lecture des adhésions ; la modification exige la gestion des adhésions plus la gestion à l’échelle du locataire. Avec un accès en lecture seul, vous voyez « You can view but not change memberships. ».
- La personne a besoin d’un compte dans le fournisseur d’identité. Le champ **Expected IdP subject (optional)** limite l’invitation à ce seul compte ; sans lui, toute personne qui se connecte avec le lien peut l’accepter.
- Prévoyez un canal de confiance pour remettre le lien d’invitation (par exemple la messagerie propre à votre organisation). POSEIDON n’envoie pas d’e-mail.
- Vous ne pouvez accorder que les autorisations que vous détenez vous-même. C’est un autre administrateur qui modifie votre propre adhésion.
- L’administration des adhésions exige une connexion active au serveur ; rien ne peut être modifié hors ligne.

## Étape par étape
1. Consultez les membres dans la liste **Members**. Filtrez par **Statut** (par exemple **Invited**) et **Role bundle**, puis appuyez sur **Apply**. Pour rechercher, saisissez un préfixe d’identifiant d’adhésion ou de compte, ou le sujet IdP exact, et appuyez sur **Rechercher**.
2. Ouvrez un membre avec **Ouvrir**. Examinez **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** et **Delegations**.
3. Pour inviter un membre, appuyez sur **Add member**. Vérifiez **IdP issuer**, saisissez éventuellement **Expected IdP subject (optional)**, choisissez au moins un **Role bundle**, ajoutez des **Individual permissions** si nécessaire et définissez le périmètre (tout le locataire ou au moins un navire). Appuyez ensuite sur **Create membership**.
4. Le membre s’ouvre à l’état **Invited** avec l’**Invitation link**. Appuyez sur **Copy invitation link** et remettez le lien. Le lien n’est affiché qu’à ce moment ; il ne fonctionne qu’une fois et expire au bout de 24 heures.
5. Si le lien a été perdu ou a expiré, appuyez sur **Create a new invitation link** ; le lien précédent cesse de fonctionner. Pour retirer l’invitation, appuyez sur **Revoke invitation**.
6. La personne invitée ouvre le lien, se connecte, vérifie ce que l’invitation accorde et appuie sur **Accept invitation**. Après s’être reconnectée, elle travaille avec l’attribution que vous avez définie ; le membre affiche alors **Active** et **Invitation accepted**.
7. Pour modifier l’attribution d’un membre existant, appuyez sur **Edit grant**, puis sur **Save grant**. Chaque enregistrement crée une nouvelle révision de l’attribution ; les révisions plus anciennes restent dans **Grant history**.
8. Pour une passation temporaire, utilisez **Delegate from this member** et renseignez le délégué, **Valid from**, **Valid until**, **Motif** et **Delegated permissions**.
9. Pour couper l’accès, utilisez **Suspend** dans **Lifecycle** ; **Resume** le rouvre. **Revoke membership** est définitif.

## Signification des états
- **Invited :** l’adhésion attend la personne invitée. Elle n’accorde rien et ne réserve le compte nulle part, pas non plus dans une autre organisation.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted :** l’état du dernier lien d’invitation. Un lien expiré, remplacé ou révoqué ne peut jamais être accepté.
- **Active / Suspended / Revoked :** le cycle de vie de l’adhésion. La requête suivante d’un membre suspendu ou révoqué est refusée.
- **Grant source — Managed here :** l’attribution a été donnée sur cette page et peut y être modifiée.
- **Grant source — Operator configuration :** l’attribution provient de la configuration du serveur (bootstrap/break-glass) et ne peut pas être modifiée ici.
- **Grant source — No grant :** un enregistrement existe mais n’accorde rien.
- **Not held by you / You do not hold every permission of this bundle :** vous ne pouvez pas accorder cette autorisation ou cet ensemble parce que vous ne la détenez pas.
- **Saved. The change takes effect on the member's next request.** L’accès d’un membre invité commence à la connexion suivant l’acceptation.

## Ce que vous ne pouvez pas faire / prudence
- Vous ne pouvez pas accorder une autorisation, un navire ou un accès à l’échelle du locataire que vous ne détenez pas.
- Vous ne pouvez pas modifier votre propre adhésion ; le dernier administrateur actif ne peut pas être retiré.
- Un compte ne peut détenir une adhésion acceptée que dans une seule organisation. Une invitation ne déplace jamais un compte qui appartient déjà ailleurs ; la personne invitée voit alors seulement que l’invitation ne peut pas être acceptée.
- Traitez le lien d’invitation comme une clé : toute personne qui le détient et se connecte peut l’accepter, sauf si vous avez saisi un sujet attendu. Ne le publiez pas dans des canaux partagés. Transmettez toujours le lien complet : la partie secrète après **#** n’est lue que par le navigateur et n’atteint jamais le serveur ni ses journaux.
- Les autorisations de décision, de vérification et d’approbation ne figurent jamais dans un ensemble. Seul un administrateur qui les détient peut les accorder, une par une. Les actions soumises à désignation (par exemple les décisions de remorqueur ou de départ) exigent aussi une désignation par l’opérateur. Accorder un ensemble de rôles ne désigne personne comme Capitaine ou vérificateur.
- Vous ne pouvez pas approuver une délégation dont vous êtes la source ou le délégué. Une délégation ne dépasse jamais l’autorité actuelle de la source et ne s’applique jamais à tout le locataire.
- POSEIDON ne stocke aucun mot de passe, ne conserve qu’une empreinte du lien d’invitation et ne réaffiche jamais les sujets IdP dans la liste.

## En cas de problème
- **This identity is configured by the operator, already a member here or already invited here.** Ouvrez plutôt le membre existant, ou révoquez d’abord l’ancienne invitation.
- **La personne invitée voit « This invitation cannot be accepted » :** le lien a expiré, a été utilisé, remplacé ou révoqué, appartient à un autre compte, ou son compte appartient déjà à une organisation. Vérifiez l’état de l’**Invitation** et appuyez sur **Create a new invitation link** si nécessaire.
- **Le lien n’a pas été copié :** sélectionnez le champ du lien et copiez-le manuellement.
- **Exceeds your own authority :** choisissez un ensemble ou un périmètre plus restreint, ou demandez à un administrateur qui détient l’autorisation.
- **L’enregistrement a changé entre-temps :** appuyez sur **Reload current state** et refaites votre modification.
- **The outcome is unknown. Retry with the same request or reload.** Vous pouvez renvoyer la même commande en toute sécurité.
- **Not the trusted identity provider :** vérifiez l’émetteur. Si le compte provient d’un autre IdP, contactez l’administrateur système (13.03 §10).

## Pages associées
