---
key: tug-job-board
title: Quadro de serviços
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [canRead, canWrite]
related: [tug-command, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 7]
reviewed: false
---
## Para que serve esta página
O quadro de serviços lista as **solicitações de serviço de rebocador em rascunho** do contexto de rebocador selecionado para planejamento. Ele responde: "Quais solicitações existem, qual janela de tempo pedem, quantos rebocadores e quanta tração estática (BP) precisam, e o que ainda falta para o planejamento?" O quadro mostra apenas declarações em rascunho; não é uma atribuição operacional, elegibilidade, permissão nem aprovação de segurança.

## Quem usa
- **Coordenador de operações de rebocador / Operações em terra:** para ver as solicitações, acompanhar as informações de conflito e de prontidão e preparar uma proposta de atribuição (13.03 §7).
- **Mestre do rebocador:** para ver com antecedência o trabalho futuro do seu rebocador (13.03 §3).

## Antes de começar
- Na barra de contexto superior, selecione a **empresa e o rebocador corretos**. O quadro mostra apenas as solicitações deste contexto.
- Você precisa de acesso de leitura para ver o quadro. Criar uma solicitação exige acesso de escrita; sem ele, o botão "Nova solicitação" não aparece.

## Passo a passo
1. No resumo do topo, veja o número de solicitações nesta página, os serviços com um bloqueio registrado, os que aguardam revisão de prontidão e os com prontidão desconhecida. **As contagens abrangem apenas esta página.**
2. Se necessário, digite um número de serviço ou o nome do navio assistido em **Buscar solicitações**, escolha os filtros **Prioridade**, **Estado** e **Janela de tempo** e pressione **Aplicar filtros**.
3. Em cada linha, leia a janela solicitada (UTC), as atribuições em rascunho, o número de rebocadores necessário, o BP necessário em relação à capacidade de planejamento, a prontidão e o conflito operacional.
4. Abra **Motivo / próxima ação** em uma linha para ver as referências dos rebocadores atribuídos, o escopo da comparação e os motivos registrados.
5. Use **Abrir serviço** para ir aos detalhes da solicitação.
6. Para uma nova solicitação, pressione **Nova solicitação** para abrir o **Recebimento de serviços** (exige acesso de escrita).
7. Use **Atualizar quadro** para ler os dados novamente e **Anterior** / **Próximo** para outras páginas.

## O que significam os estados
- **Rascunho / Enviado / Cancelado:** o estado do ciclo de vida da solicitação. Enviar registra a solicitação para planejamento; não é uma aprovação, atribuição nem decisão de prontidão.
- **Bloqueado:** a solicitação tem um bloqueio registrado. O motivo está em "Motivo / próxima ação".
- **Revisão necessária:** a informação de prontidão aguarda revisão por uma pessoa autorizada.
- **Desconhecido:** não há dados de origem suficientes para prontidão ou conflito. Desconhecido não significa "Pronto" nem "sem conflito", e o quadro não tira nenhuma conclusão de prontidão a partir dele.
- **Revisão humana autorizada necessária / Revisão de contato de limites necessária:** duas solicitações podem se sobrepor no tempo ou se tocar de ponta a ponta. Uma pessoa decide.
- **BP do perfil (não verificada):** a tração estática mostrada é um valor declarado, não medido nem verificado.

## O que você não pode fazer / cuidado
- O quadro nunca declara uma solicitação "pronta" ou "segura". Uma proposta de atribuição feita em terra não retira a autoridade de segurança a bordo do Mestre do rebocador (13.03 §7).
- Os campos de uma solicitação enviada ou cancelada não são alterados a partir do quadro; uma solicitação cancelada não pode ser reiniciada, crie uma nova em seu lugar.
- O filtro de janela de tempo usa o horário do servidor. Solicitações sem início permanecem na lista como "Desconhecido".
- Os dados mostrados offline podem estar antigos e o quadro não pode ser atualizado.

## Se algo der errado
- **Quadro atual indisponível:** verifique sua conexão e use "Atualizar quadro".
- **Não há solicitações em rascunho:** se você aplicou filtros, tente outra busca ou prioridade; novas solicitações aparecem neste contexto.
- **Acesso indisponível:** verifique seu papel e o contexto do rebocador; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **Bloqueio ou revisão inesperados:** abra "Motivo / próxima ação" e revise os motivos registrados com o responsável pela solicitação.

## Páginas relacionadas
