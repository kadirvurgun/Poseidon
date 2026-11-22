---
key: notification-escalation
title: Notifications
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page des notifications liste les **exceptions de source configurées dans l’application** (maintenance due, examen de maintenance requis, source de maintenance modifiée ou indisponible) pour le navire ou le remorqueur sélectionné. Elle répond à la question : « Quelles notifications de maintenance ont atteint mon rôle, quelle est leur gravité, lesquelles ont été escaladées parce que personne n’en a accusé réception à temps, et sur quel enregistrement source reposent-elles ? » Un accusé de réception ne vérifie pas de preuve et n’approuve pas d’opérations.

## Qui l’utilise
- **Chef mécanicien / Surintendant technique :** pour suivre les échéances de maintenance et les demandes d’examen et accéder au plan source (13.03 §5).
- **Capitaine du remorqueur / Capitaine / Équipage :** pour voir les notifications acheminées vers leurs rôles et en accuser réception (13.03 §6).

## Avant de commencer
- Vous avez besoin d’un accès en lecture aux notifications, à la maintenance et aux équipements. L’accusé de réception et l’enregistrement des préférences requièrent des autorisations distinctes ; sans elles, les boutons correspondants ne sont pas affichés.
- Choisissez un navire ou un remorqueur autorisé dans **Navire de notification**.
- Aucune commande d’accusé de réception ou de préférence ne peut être envoyée hors ligne.

## Étape par étape
1. Choisissez un navire dans **Navire de notification**.
2. Lisez la ligne de politique : « Configuré · politique source non vérifiée · remise dans l’application uniquement ».
3. Pour chaque notification, lisez le titre, le badge de gravité (**Information**, **Avertissement**, **Critique**) et le badge d’état ; vérifiez **Source / révision**, **Date d’échéance** et **Créé le**. **Règle d’acheminement** affiche le code de la règle.
4. Utilisez **Ouvrir le plan source** pour accéder au plan de maintenance associé.
5. Pour enregistrer que vous avez vu une notification, appuyez sur **Accuser réception** (uniquement à l’état **Ouvert** ou **Escaladé**).
6. Si vous disposez de l’accès aux préférences, modifiez **Désactiver les notifications indicatives non obligatoires** et **Heures calmes** (début, fin, fuseau horaire IANA) et appuyez sur **Enregistrer la préférence indicative**.
7. Utilisez **Notifications précédentes** / **Autres notifications** pour les autres pages et **Recharger la boîte de réception** pour relire.

## Signification des états
- **Ouvert :** la notification attend un accusé de réception.
- **Accusé de réception enregistré :** une personne a enregistré qu’elle l’a vue. Cela ne signifie pas que la maintenance a été effectuée ni que la preuve a été vérifiée.
- **Escaladé :** la notification n’a pas fait l’objet d’un accusé de réception à temps et a été acheminée vers le rôle d’escalade de sa règle.
- **Source changed / Source unavailable :** l’enregistrement de base a changé ou ne peut pas être confirmé. La base enregistrée est conservée ; cela n’efface ni ne réarme la notification.
- **Acheminement obligatoire · ne peut pas être désactivé :** les préférences ne peuvent pas désactiver cette notification.
- **Unknown (Due date) :** la source n’a pas de date d’échéance. Cela ne signifie pas que rien n’est dû.

## Ce que vous ne pouvez pas faire / prudence
- Un accusé de réception ne vérifie pas de preuve et n’approuve pas d’opérations.
- Les acheminements obligatoires et critiques ne peuvent pas être désactivés par les préférences indicatives. Les heures calmes ne font que retarder les avis facultatifs.
- Une page vide ne signifie pas que tout est en ordre. Si la politique ou les sources sont indisponibles, la page l’indique séparément.
- Il n’y a pas de nouvelle tentative automatique ; une commande incertaine est conservée dans cet onglet et les changements de contexte restent verrouillés.

## En cas de problème
- **Message « Outcome unknown » :** utilisez **Réessayer la commande de notification exacte** pour renvoyer la commande conservée.
- **Message « Notification version changed » :** utilisez **Recharger la boîte de réception** pour charger la liste actuelle.
- **Message « Notification sources unavailable or access denied. » :** vérifiez votre rôle et votre contexte de navire ; contactez votre administrateur de locataire si nécessaire (13.03 §10).
- **Message « The saved quiet hours could not be applied » :** tous les avis sont affichés ; vérifiez vos paramètres d’heures calmes.

## Pages associées
