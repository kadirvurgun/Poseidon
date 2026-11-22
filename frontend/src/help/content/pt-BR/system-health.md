---
key: system-health
title: Integridade do sistema
roles: [Tenant Admin, Support / Engineering]
permissions: [directory.read, audit.read, system.health.read]
related: [audit, notification-escalation, offline-conflict-center, user-admin]
screenshots: []
reviewed: false
---
## Para que serve esta página
"Integridade do sistema" mostra aos administradores as **observações do serviço** em uma única tela: API, gravador de auditoria, sincronização, tarefas de notificação, serviço de arquivos e atualidade dos dados. Ela responde: "Quais sinais do serviço foram observados para o navio selecionado e este tenant, e quais não foram?" A configuração e a telemetria ausente não comprovam que o serviço está íntegro nem que o navio está pronto.

## Quem usa
- **System / Tenant Admin:** para perceber problemas de serviço e de sincronização e tratá-los de forma controlada (13.03 §9).
- **Support / Engineering:** para revisar as observações quando um erro da aplicação é relatado (13.03 §10).

## Antes de começar
- A página só é visível para usuários com acesso de leitura em todo o tenant, de leitura do histórico de ações e de leitura da integridade do sistema.
- Na barra de contexto superior, verifique se a **empresa correta** está selecionada.
- As observações são solicitadas por navio; você precisa selecionar um navio primeiro.

## Passo a passo
1. Escolha o navio em **Escopo de observação do navio**. Para uma lista longa, use **Navios anteriores** / **Próximos navios**.
2. Verifique o horário "Lido em".
3. Em **Disponibilidade do serviço**, leia o horário de início do processo e o tempo de atividade.
4. Nos cartões **API** e **Gravador de auditoria**, leia os números de solicitações, erros, registros persistidos e latência. Eles abrangem este tenant desde o início do processo.
5. Em **Sincronização**, veja os resultados confirmados, os conflitos não resolvidos e a última sincronização confirmada.
6. Em **Tarefas de notificação**, veja as notificações abertas, os escalonamentos vencidos e o estado da tarefa de avaliação.
7. Leia os cartões **Serviço de arquivos** e **Atualidade dos dados**.
8. Em **Métricas do piloto**, revise as observações dos clientes inscritos no piloto.
9. Pressione **Atualizar observações** para obter dados atuais.

## O que significam os estados
- **Não observado / Indisponível:** não há observação para este sinal. Isso não significa "íntegro" nem "sem problema".
- **Configurado — não sondado:** o serviço de arquivos parece configurado, mas nenhuma verificação ao vivo do armazenamento ou do scanner foi feita.
- **Dentro do cronograma / Em execução / Atrasada / Agendador não habilitado / Execução abandonada (bloqueio expirado):** o estado da tarefa de avaliação de notificações.
- **Conflitos não resolvidos:** conflitos offline aguardando uma resolução humana. Eles nunca são mesclados automaticamente.
- **Atual / Desatualizado / Desconhecido / Indisponível:** contagens das avaliações de atualidade dos dados. Desconhecido não significa "Atual".
- **Este instantâneo tem mais de dois minutos:** atualize antes de confiar nele.

## O que você não pode fazer / cuidado
- Esta página é somente leitura; nenhuma ação é executada sobre serviços, navios ou registros.
- Os registros de notificação existem apenas dentro do aplicativo; não são prova de entrega nem de qualquer decisão de segurança.
- A disponibilidade do serviço mostra apenas que este processo da API respondeu; não prova que outros serviços estão em execução.
- As métricas do piloto são declaradas e não verificadas; não constituem uma declaração de aceitação.
- Não corrija dados diretamente em produção; publique alterações de configuração de forma controlada (13.03 §9).

## Se algo der errado
- **Acesso à integridade indisponível neste contexto:** verifique seu papel e o escopo do tenant.
- **Observações do serviço indisponíveis:** nenhum estado íntegro é presumido; use "Atualizar observações" depois de algum tempo.
- **Há um conflito não resolvido:** encaminhe o usuário envolvido ao Centro de conflitos (13.03 §10).
- **Erro da aplicação:** relate-o ao suporte/engenharia com o Correlation ID, a tela, o horário e os passos.

## Páginas relacionadas
