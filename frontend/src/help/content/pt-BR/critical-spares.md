---
key: critical-spares
title: Sobressalentes críticos e requisições
roles: [Chief Engineer, Technical Superintendent]
permissions: [spare.read, spare.write, requisition.propose, requisition.approve]
related: [pms-command-center, work-order-center, defects]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página de sobressalentes críticos e requisições mostra, para a embarcação selecionada, o **estoque declarado, mínimo e máximo, alertas de estoque baixo e propostas de requisição técnica**. Ela responde: "Qual sobressalente crítico atingiu seu mínimo, quantas ordens de serviço abertas fazem referência a ele e uma requisição técnica foi proposta e decidida?" As contagens são declarações humanas e não são verificadas em um almoxarifado.

## Quem usa
- **Chefe de máquinas / Superintendente Técnico:** para acompanhar os limites mínimos de sobressalentes críticos e iniciar uma requisição técnica (13.03 §5). Quem propõe uma requisição e quem a aprova são pessoas diferentes.

## Antes de começar
- A visualização exige `spare.read`. Definir sobressalentes e registrar contagens exige `spare.write`, propor uma requisição `requisition.propose` e decidir `requisition.approve`.
- Propostas e decisões exigem um usuário humano autenticado.
- Escolha a embarcação correta na lista **Embarcação**.

## Passo a passo
1. Escolha a embarcação. A visualização **Sobressalentes críticos** é aberta. Leia as contagens **Estoque baixo** e **Revisão necessária** no topo.
2. Em cada linha da tabela, leia **Criticidade do sobressalente**, **Criticidade do equipamento**, **Estado do estoque**, **Projetado**, **Mín. / máx.** e **Ordens de serviço abertas**.
3. Pressione **Novo sobressalente crítico** para um novo sobressalente ou **Editar** para um existente. Informe **Número da peça**, **Nome**, **Unidade**, **Estoque mínimo** e **Estoque máximo**. Deixe os limites vazios quando não estiverem decididos.
4. Para inserir uma contagem, marque **Registrar uma nova contagem de estoque**, preencha **Quantidade contada** e pressione **Salvar**.
5. Em uma linha com **Estoque baixo** sem requisição aberta, pressione **Propor requisição**. O servidor calcula a quantidade proposta (máximo menos estoque projetado). Se nenhum máximo estiver definido, nenhuma quantidade é proposta e o aprovador deve informar a **Quantidade aprovada**.
6. Na visualização **Requisições técnicas**, uma pessoa autorizada que não fez a proposta confere a **Quantidade aprovada** e pressiona **Aprovar para exportação ERP**, ou escolhe um **Motivo da rejeição** e pressiona **Rejeitar**.
7. Abra **Referência de exportação ERP** em uma requisição aprovada para ver a referência.

## O que significam os estados
- **Estoque baixo:** o estoque projetado (última contagem declarada menos os sobressalentes registrados em ordens de serviço enviadas desde essa contagem) está no mínimo ou abaixo dele. A proposta de requisição é oferecida nesta linha.
- **Acima do mínimo (declarado):** o estoque projetado está acima do mínimo; a contagem não é verificada.
- **Mínimo não configurado:** nenhum limite foi informado. Um limite vazio é relatado como revisão necessária, nunca como saudável.
- **Estoque desconhecido / Consumo não resolvido:** não foi possível calcular o estoque. Desconhecido não significa "estoque suficiente".
- **Proposta / Aprovada para exportação ERP / Rejeitada:** o estado da requisição técnica.
- **Crítico (declarado) / Não crítico (declarado) / Desconhecido:** a criticidade do sobressalente é uma declaração.

## O que você não pode fazer / cuidado
- Você não pode decidir uma requisição que você mesmo propôs. A página informa "You proposed this; another person must decide."
- Proposta e aprovação são atos humanos separados. A aprovação apenas cria uma referência de exportação ERP; nenhum pedido de compra, fornecedor ou preço é gerenciado aqui.
- A página não tem um campo separado de ponto de reposição; o estoque baixo é avaliado em relação ao estoque mínimo.
- Offline, os dados não podem ser atualizados e nenhuma ação pode ser realizada.

## Se algo der errado
- **O registro mudou:** use **Tentar novamente** para atualizar a página e tente outra vez.
- **Resultado desconhecido:** **Repetir o mesmo comando** envia o mesmo comando.
- **A solicitação foi rejeitada por uma regra.:** verifique o estado do estoque e as informações de requisição aberta.
- **Você não tem permissão.:** verifique seu papel e o contexto da embarcação; contate o administrador do seu tenant, se necessário (13.03 §10).

## Páginas relacionadas
