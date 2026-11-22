---
key: audit
title: Historique des actions
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: []
reviewed: false
---
## À quoi sert cette page
La page Historique des actions affiche un enregistrement attribué, **en lecture seule**, des modifications apportées aux enregistrements métier de votre périmètre autorisé. Elle répond à la question : « Quand cet enregistrement a-t-il changé, avec quelle action, et sous quel acteur et quel contexte de rôle ? » Les événements de sécurité sont restreints séparément et n’apparaissent pas ici (13.03 §4, §9).

## Qui l’utilise
- **DPA / HSEQ :** retrace les enregistrements de constats, d’actions et de vérifications (13.03 §4).
- **Utilisateur Réglementaire / Assurance :** vérifie l’historique d’un enregistrement avant un audit ou une revue (13.03 §8).
- **Tenant Admin :** contrôle les modifications d’adhésion et d’autorisations ainsi que les heures de début et de fin des délégations (13.03 §9).

## Avant de commencer
- La consultation requiert la lecture d’audit. Sans elle, vous voyez « Une autorisation d’audit est nécessaire. »
- Seuls les enregistrements du périmètre de locataire et de navire pour lequel vous êtes autorisé sont listés.
- Si vous recherchez un enregistrement précis, gardez son **ID du navire** ou son **ID de l’objet** à portée de main. Les liens d’historique des autres pages ouvrent cette page déjà filtrée.

## Étape par étape
1. Renseignez les filtres : **ID du navire**, **ID de l’objet**, **Type d’objet** et **Action** (**Tous**, **Créé**, **Mis à jour**, **Supprimé**).
2. Appuyez sur **Filtrer**. Utilisez **Effacer les filtres** pour réinitialiser.
3. Examinez les colonnes **Heure**, **Objet** et **Action** dans **Enregistrements autorisés**. Passez d’une page à l’autre avec **Première page** et **Suivant**.
4. Appuyez sur le lien de l’objet d’une ligne pour ouvrir **Détails de l’enregistrement**. Lisez **Référence de l’acteur**, **Références des rôles affectés**, **Capacités de confiance**, **Motif**, **Métadonnées**, **Version de la ressource** et **Corrélation**.
5. S’il existe une preuve de révision immuable, appuyez sur **Ouvrir la révision immuable exacte** pour voir l’enregistrement tel qu’il était. Appuyez sur **Fermer** pour fermer les détails.

## Signification des états
- **Créé / Mis à jour / Supprimé :** le type d’action effectuée sur l’enregistrement.
- **Enregistré :** les métadonnées ont été entièrement capturées.
- **Historical metadata incomplete / Historical identifier unavailable / Historical context unavailable :** les enregistrements plus anciens n’ont pas conservé cette information. Une information manquante n’est jamais complétée par supposition.
- **Valeurs non communiquées :** la valeur du champ est masquée pour des raisons de confidentialité ; seul le fait qu’elle a changé est affiché.
- **Les preuves de révision immuable sont indisponibles pour cet enregistrement.** Il n’existe pas d’instantané de révision pour ce type d’enregistrement.

## Ce que vous ne pouvez pas faire / prudence
- La page est en lecture seule. Une entrée de l’historique des actions ne peut être ni modifiée ni supprimée.
- La présence d’un enregistrement dans l’historique ne signifie pas que le travail a été vérifié ou efficace ; Vérifié ≠ Efficace (13.03 §4).
- Cette page enregistre les modifications métier. À elle seule, elle ne constitue ni un audit complet des actions utilisateur ni une preuve de conformité en matière de confidentialité ; les événements de sécurité sont conservés séparément.
- La liste n’affiche que le périmètre pour lequel vous êtes autorisé. Les enregistrements hors de votre périmètre ne sont pas listés ; ne pas voir un enregistrement ne signifie pas qu’il n’a jamais été modifié.

## En cas de problème
- **Une autorisation d’audit est nécessaire.** Vérifiez votre rôle et votre périmètre ; contactez le Tenant Admin si nécessaire (13.03 §10).
- **Connectez-vous pour continuer.** Reconnectez-vous.
- **L’historique est indisponible. Vérifiez les filtres et réessayez.** Vérifiez que les identifiants sont complets et corrects, puis appuyez sur **Réessayer**.
- **Aucun enregistrement autorisé ne correspond à ces filtres.** Élargissez les filtres.
- Si une erreur d’application persiste, signalez-la au support avec l’ID de corrélation, l’écran, l’heure et les étapes effectuées (13.03 §10).

## Pages associées
