---
key: engine-daily-log
title: Diário de máquinas
roles: [Chief Engineer, Crew / OOW]
permissions: [daily.engine.read, daily.engine.write, daily.engine.review]
related: [noon-report, deck-daily-log, fuel-bunker-log, operational-downtime, work-order-center, defects, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para que serve esta página
O diário de máquinas mantém os **registros do quarto de máquinas** do navio selecionado como rascunhos, com a versão de origem registrada. Ele responde: "O que o quarto de máquinas declarou sobre o status e as horas dos motores, o consumo de combustível e de óleo lubrificante, a água doce, as medições críticas, os alarmes, os defeitos, o abastecimento, o porão e os controles ambientais, e o Chefe de máquinas registrou uma revisão desta revisão exata?" Uma entrada salva é uma declaração não verificada. Os dados do rascunho não são transformados em conclusões sobre emissões, garantia de combustível ou prontidão.

## Quem usa
- **Engenheiro de quarto (Crew / OOW / Engenheiro):** para manter o registro do quarto de máquinas em tempo real e em ordem cronológica (13.03 §6).
- **Chefe de máquinas:** para revisar o diário salvo, vinculá-lo a ordens de serviço e defeitos e registrar a revisão como um ato humano (13.03 §5). Somente uma designação com a função de Chefe de máquinas pode registrar uma revisão do diário de máquinas.

## Antes de começar
- Na barra de contexto superior, selecione a **empresa e o navio corretos**.
- A leitura exige `daily.engine.read`; salvar exige `daily.engine.write` e direitos de edição neste navio. Registrar uma revisão exige `daily.engine.write` com direitos de edição neste navio, `daily.engine.review` e uma designação vigente de Chefe de máquinas para este registro exato.
- Vincular ordens de serviço ou defeitos também exige acesso de leitura a esses registros.

## Passo a passo
1. Escolha o navio. **Rascunhos registrados** lista cada diário por data e revisão com "Draft · review required".
2. Pressione **Novo rascunho** ou abra um diário listado para **Editar rascunho registrado**.
3. Preencha **Contexto do quarto de máquinas**, **Status e horas dos motores**, **Consumo e produção**, **Medições, alarmes e defeitos** e **Contexto de trabalho e meio ambiente**. Para cada campo, escolha **Conhecido**, **Desconhecido** ou **Ausente**.
4. Pressione **Salvar rascunho**. Cada salvamento adiciona uma revisão ao **Histórico de revisões registradas**.
5. Em um diário salvo, use **Ordens de serviço e defeitos**: escolha um **Tipo de registro** e um **Registro** e depois **Vincular**. Um vínculo fixa uma revisão exata; **Remover vínculo** o remove.
6. Se necessário, abra **Abrir registro de combustível da fonte salva** ou **Abrir registro de indisponibilidade da fonte salva**. Quantidades e unidades não são copiadas.
7. O Chefe de máquinas abre **Registro de revisão humana**, escolhe uma **Nomeação humana autorizada** e uma **Entrada de revisão**, escreve uma **Justificativa da revisão** e pressiona **Registrar entrada de revisão**.

## O que significam os estados
- **Declaração não verificada / Conhecido / Desconhecido / Ausente:** valores declarados e seu estado de dados. Desconhecido não significa "Não", "Pronto" nem "Não aplicável".
- **Pendente — revisão não registrada:** não existe revisão humana para esta revisão.
- **Metadados humanos registrados — não avaliados:** existe uma entrada de revisão do Chefe de máquinas para esta revisão. Ela não estabelece verificação nem aprovação do Chefe de máquinas.
- **Revisão atual / Existe revisão mais recente:** se uma ordem de serviço ou um defeito vinculado mudou desde que foi vinculado. Um vínculo é apenas linhagem; ele não verifica, não encerra nem aprova esse registro.
- **consulta indisponível — DESCONHECIDO:** o tipo de registro vinculado não pode ser lido com o seu acesso.

## O que você não pode fazer / cuidado
- O sistema nunca revisa nem finaliza o diário. A revisão é um ato humano do Chefe de máquinas designado (13.03 §5).
- Quem foi autor de qualquer revisão do diário não pode revisá-lo. Nenhuma designação é oferecida a um autor e a página informa "You authored this log revision; an independent reviewer must record the review."; o servidor também recusa essa revisão por segregação de funções.
- Mantenha as entradas em tempo real e em ordem cronológica; não altere o registro revisado de outra pessoa (13.03 §6).
- Vincular um defeito aqui não registra uma restrição nem o encerra; faça isso na página do defeito ou da ordem de serviço (13.03 §5).
- Os vínculos exigem uma conexão online ativa; nada é vinculado offline. Conflitos offline nunca são mesclados automaticamente.

## Se algo der errado
- **"The log entry changed. Reload it before changing links.":** recarregue e vincule novamente.
- **"Save outcome unknown":** use **Repetir comando salvo exato**; não digite novamente.
- **"Human conflict resolution required":** abra **Revisar conflito** ou a Central de conflitos e escolha explicitamente (13.03 §10).
- **Nenhuma designação disponível ou acesso negado:** verifique seu papel e o contexto do navio com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
