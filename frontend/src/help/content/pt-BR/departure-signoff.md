---
key: departure-signoff
title: Prontidão de partida e aprovação do Comandante
roles: [Master, Shore Operations, Technical Superintendent]
permissions: [departure.readiness.read, departure.decide]
related: [today, fleet, defects, safety-events, certificate-command-center, data-trust-center]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página mostra uma **avaliação de prontidão consultiva** para o navio selecionado e o período de partida previsto, e registra a decisão do Comandante designado. Ela responde: "O que dizem as fontes de tripulação, técnicas, estatutárias, de emergência e de eventos de segurança para este período, e por quê?" A decisão de liberar, reter ou impedir a partida cabe ao **Comandante** (13.03 §2). O sistema nunca libera uma partida; uma aprovação registrada não é uma autorização de partida.

## Quem usa
- **Comandante:** para revisar a avaliação e a lista "Por quê" e, em seguida, registrar Liberar, Reter ou Negar com uma justificativa (13.03 §2).
- **Operações em terra / Superintendente Técnico:** para acompanhar a avaliação e as decisões registradas do Comandante em modo somente leitura. A equipe de terra não pode registrar uma decisão no lugar do Comandante.

## Antes de começar
- Na barra de contexto superior, verifique se a **empresa correta** está selecionada. A página lista apenas os navios do seu escopo (13.03 §1).
- Você precisa de acesso de leitura à prontidão de partida para ver a página.
- A permissão de decisão sozinha não basta para registrar uma decisão: você também precisa de uma **designação vigente de Comandante** para este navio. Sem ela, a página informa "You hold no current Master appointment for this ship. Decisions are read-only."

## Passo a passo
1. Escolha o navio na lista "Escolha um navio".
2. Em **Período de partida previsto**, preencha "Data de partida" e "Fim da viagem" e pressione **Avaliar prontidão**.
3. Na linha **Avaliação**, leia o selo de estado, o modo "Atual" ou "Previsão", "Avaliado em" e "Válido até".
4. Na tabela de categorias, confira os resultados de Tripulação, Técnico, Estatutário, Emergência e Eventos de segurança.
5. Leia a lista **Por quê**. Cada linha mostra uma observação que não foi aprovada, seu código de motivo e sua fonte. Revise uma linha inesperada na própria página da fonte.
6. Se a avaliação não estiver atual e Pronto, leia **Por que Liberar não está disponível**. Essa seção informa quando o período é uma previsão, lista as categorias que ainda não foram aprovadas com seus resultados e explica qual registro fecharia as lacunas de fonte de Emergência (nenhuma fonte de exercícios 61.02 conectada ainda) e Estatutário (nenhum conjunto de certificados obrigatórios definido). Ela apenas explica as categorias do servidor; não altera o controle.
7. Em **Decisões do Comandante**, veja as decisões registradas anteriormente e sua validade.
8. Se você for o Comandante, em **Registrar decisão do Comandante** escolha Liberar, Reter ou Negar, selecione sua designação em "Designação", escreva uma "Justificativa" (10–2000 caracteres) e pressione **Registrar**.
9. Use **Atualizar** para ler a avaliação novamente.

## O que significam os estados
- **Pronto:** nenhuma fonte registrada tem uma observação que não foi aprovada. Isto é apenas uma informação; a decisão de partida continua sendo do Comandante.
- **Condicional:** pelo menos uma fonte informa uma condição. Liberar não é oferecido para esta avaliação.
- **Não pronto:** pelo menos uma fonte retorna "Reprovado". Outras categorias aprovadas não compensam isso.
- **Revisão necessária:** pelo menos uma entrada é "Desconhecido". Uma pessoa autorizada precisa analisá-la.
- **Aprovado / Condição / Reprovado / Desconhecido:** o resultado da categoria na tabela de categorias. **Desconhecido** significa que a fonte não pôde ser lida ou não tem dados; não significa "Aprovado" nem "Pronto". Nesta versão, nenhuma fonte de preparação para emergências está conectada à categoria Emergência, por isso essa linha mostra Desconhecido. Estatutário pode permanecer Desconhecido mesmo quando todos os certificados registrados são válidos, porque nenhuma fonte define o conjunto de certificados obrigatórios para o navio. Estatutário também inclui sinais de confiança de dados (DATA_TRUST_*); sem acesso de leitura à confiança de dados, Estatutário mostra Desconhecido.
- **Previsão:** o período não inclui o dia de hoje. É apenas uma previsão de planejamento e não pode ser liberada.
- **Atual / Desatualizada — entradas alteradas:** a validade de uma decisão registrada. Quando as entradas mudam, a decisão anterior fica desatualizada e não é excluída.

## O que você não pode fazer / cuidado
- O sistema nunca libera uma partida e não mostra nenhum estado como autorização de partida. Uma decisão registrada é o próprio registro humano do Comandante designado.
- Liberar só pode ser registrado para uma avaliação READY atual; todas as categorias devem ser aprovadas. Como a categoria Emergência permanece Desconhecido nesta versão, a avaliação não pode chegar a Pronto e Liberar não é oferecido; **Por que Liberar não está disponível** mostra o motivo. Mesmo em Pronto, a decisão de liberação é do Comandante; o sistema não aprova nada. "Hold and Deny can always be recorded with a reason."
- Uma justificativa é obrigatória para toda decisão. As decisões registradas não são alteradas; uma nova decisão é um novo registro.
- Você não pode registrar uma decisão enquanto a avaliação estiver desatualizada ou você estiver offline; atualize primeiro.

## Se algo der errado
- **Você está offline:** a avaliação pode estar desatualizada e a aprovação fica desativada. Atualize quando estiver online novamente (13.03 §10).
- **Você não tem acesso à prontidão de partida deste navio.:** verifique seu papel e o contexto da embarcação; contate o administrador do seu tenant, se necessário.
- **Prontidão inesperada:** abra a lista "Por quê"; se uma fonte parecer errada, leve a questão ao responsável pelo controle ou a um especialista no assunto.
- **O resultado é incerto:** "Tentar novamente" envia o mesmo comando. Não escreva uma nova decisão.
- **O histórico de decisões mudou / A avaliação mudou:** use "Recarregar", revise a avaliação atual e depois decida.
- **O comando não foi aceito.:** informe ao suporte com uma captura de tela, o horário e seus passos.

## Páginas relacionadas
