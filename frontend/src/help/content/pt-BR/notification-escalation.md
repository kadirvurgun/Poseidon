---
key: notification-escalation
title: Notificações
roles: [Chief Engineer, Tug Master, Master, Crew]
permissions: [notification.read, maintenance.read, equipment.read, notification.acknowledge, notification.preference.write]
related: [today, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página de notificações lista as **exceções de fontes configuradas no aplicativo** (manutenção a vencer, revisão de manutenção necessária, fonte de manutenção alterada ou indisponível) para o navio ou rebocador selecionado. Ela responde: "Quais notificações de manutenção chegaram ao meu papel, qual é sua gravidade, quais foram escalonadas porque ninguém confirmou o recebimento a tempo e em qual registro de origem elas se baseiam?" A confirmação não verifica evidências nem aprova operações.

## Quem usa
- **Chefe de máquinas / Superintendente técnico:** para acompanhar as datas de vencimento de manutenção e as solicitações de revisão e ir ao plano de origem (13.03 §5).
- **Comandante do rebocador / Comandante / Tripulação:** para ver as notificações encaminhadas aos seus papéis e confirmar o recebimento (13.03 §6).

## Antes de começar
- Você precisa de acesso de leitura a notificações, manutenção e equipamentos. Confirmar e salvar preferências exigem permissões separadas; sem elas, os botões correspondentes não são mostrados.
- Escolha um navio ou rebocador autorizado em **Navio da notificação**.
- Nenhum comando de confirmação ou de preferência pode ser enviado offline.

## Passo a passo
1. Escolha uma embarcação em **Navio da notificação**.
2. Leia a linha de política: "Configured · unverified source policy · in-app delivery only".
3. Para cada notificação, leia o título, o selo de gravidade (**Informação**, **Alerta**, **Crítico**) e o selo de estado; confira **Fonte / revisão**, **Data de vencimento** e **Criado em**. **Regra de roteamento** mostra o código da regra.
4. Use **Abrir plano de origem** para ir ao plano de manutenção relacionado.
5. Para registrar que você viu uma notificação, pressione **Confirmar** (somente no estado **Aberto** ou **Escalonado**).
6. Se você tiver acesso às preferências, edite **Silenciar notificações orientativas não obrigatórias** e **Horário silencioso** (início, fim, fuso horário IANA) e pressione **Salvar preferência orientativa**.
7. Use **Notificações anteriores** / **Mais notificações** para outras páginas e **Recarregar caixa** para ler novamente.

## O que significam os estados
- **Aberto:** a notificação aguarda confirmação.
- **Ciência registrada:** uma pessoa registrou que a viu. Isso não significa que a manutenção foi feita nem que as evidências foram verificadas.
- **Escalonado:** a notificação não foi confirmada a tempo e foi encaminhada ao papel de escalonamento da sua regra.
- **Fonte alterada / Fonte indisponível:** o registro de base mudou ou não pode ser confirmado. A base registrada é mantida; isso não elimina nem rearma a notificação.
- **Rota obrigatória — não pode ser silenciada:** as preferências não podem silenciar esta notificação.
- **Desconhecido (Data de vencimento):** a fonte não tem data de vencimento. Isso não significa que nada esteja a vencer.

## O que você não pode fazer / cuidado
- A confirmação não verifica evidências nem aprova operações.
- Rotas obrigatórias e críticas não podem ser silenciadas por preferências orientativas. O horário silencioso apenas adia avisos opcionais.
- Uma página vazia não significa que está tudo em ordem. Se a política ou as fontes estiverem indisponíveis, a página informa isso separadamente.
- Não há nova tentativa automática; um comando incerto é mantido nesta aba e as mudanças de contexto permanecem bloqueadas.

## Se algo der errado
- **Mensagem "Outcome unknown":** use **Repetir comando exato de notificação** para enviar novamente o comando mantido.
- **Mensagem "Notification version changed":** use **Recarregar caixa** para carregar a lista atual.
- **Mensagem "Notification sources unavailable or access denied.":** verifique seu papel e o contexto da embarcação; contate o administrador do seu tenant, se necessário (13.03 §10).
- **Mensagem "The saved quiet hours could not be applied":** todos os avisos são mostrados; verifique suas configurações de horário silencioso.

## Páginas relacionadas
