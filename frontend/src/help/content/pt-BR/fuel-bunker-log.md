---
key: fuel-bunker-log
title: Combustível, abastecimento e ROB
roles: [Chief Engineer, Master]
permissions: [operations.fuel.read, operations.fuel.write]
related: [noon-report, engine-daily-log, port-activity-sof, operational-downtime]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página de combustível, abastecimento e ROB mantém as **declarações de combustível e de abastecimento** do navio selecionado como rascunhos não verificados, com seu histórico de origem. Ela responde: "Que combustível foi recebido ou consumido, que ROB declaramos antes e depois, quais referências de BDN, fornecedor, amostra e tanque pertencem a ele, e a cadeia de ROB declarada continua de um registro para o seguinte?" A página não corrige quantidades, não calcula emissões e não estabelece conformidade nem verificação.

## Quem usa
- **Chefe de máquinas / oficiais de máquinas:** para registrar declarações de abastecimento, consumo e ROB com suas referências de evidência (13.03 §5).
- **Comandante:** para acompanhar as declarações de combustível ao lado dos resumos de meio-dia revisados pelo Comandante (13.03 §2).

## Antes de começar
- Na barra de contexto superior, selecione a **empresa e o navio corretos**. Se nenhum navio estiver selecionado, a página mostra "Select a ship".
- A leitura exige `operations.fuel.read`. Criar e salvar exige `operations.fuel.write` e direitos de edição neste navio.
- Os horários devem incluir data, segundos e Z ou um deslocamento UTC explícito; um horário local sem fuso horário não é aceito.

## Passo a passo
1. Escolha o navio. **Registros de combustível e ROB** lista os registros retornados. Use **Filtrar página retornada** para restringir apenas esta página.
2. Leia **Revisão de continuidade de ROB** abaixo da lista. Ela mostra o saldo (antes + recebido − consumido − depois), **Continuidade com o registro anterior** e **Sinalizações de revisão** por tipo de combustível declarado.
3. Leia **Resumos de combustível e ROB de Noon revisados pelo comandante** como contexto. Eles são mostrados como texto declarado e não são copiados para o livro de registros.
4. Pressione **Novo registro em rascunho**. Preencha **Contexto de registro e entrega**, **Declarações de quantidades e ROB** e **Propriedades do combustível e registro de amostras**. Para cada campo, mantenha um valor declarado, **Ausente** ou **Desconhecido**.
5. Pressione **Salvar rascunho não verificado**. Cada salvamento adiciona uma revisão ao **Histórico de revisões registradas**.
6. Para começar a partir de um relatório de meio-dia ou de um diário de máquinas salvo, use **Abrir registro de combustível da fonte salva** naquela página. A **Declaração diária vinculada** permanece somente leitura aqui.

## O que significam os estados
- **Declaração em rascunho / Registro em rascunho não verificado:** uma pessoa declarou os valores; nada é verificado.
- **Revisão necessária:** o estado de revisão de conformidade de todos os registros; a conformidade não foi avaliada.
- **Pendente; verificação não estabelecida:** o estado de revisão da verificação; não existe verificação.
- **Nenhuma discrepância detectada:** os números declarados fecham. Isto não é verificação.
- **Desconhecido:** a continuidade não pode ser avaliada, por exemplo quando falta um valor ou o histórico mais antigo está fora da janela avaliada. Desconhecido não significa "Nenhuma discrepância" nem "Pronto".
- **Ausente / Desconhecido:** distintos de zero.

## O que você não pode fazer / cuidado
- Revisão humana, aprovação operacional ou legal, finalização e captura offline não estão disponíveis nesta página.
- O sistema sinaliza diferenças para revisão humana; ele nunca corrige uma quantidade nem decide sobre a conformidade do combustível.
- Apenas os 100 registros mais recentes são avaliados. Sem uma referência de tanque, uma diferença em uma embarcação com vários tanques é uma questão de dados de tanque ausentes, não um erro de dados.
- Quantidades do relatório de meio-dia ou do diário de máquinas com unidade não resolvida nunca são convertidas em valores recebidos, consumidos ou de ROB.

## Se algo der errado
- **"The saved source changed.":** seus dados são mantidos; recarregue ou descarte explicitamente antes de salvar novamente.
- **"Outcome unknown.":** use **Repetir comando operacional exato**; não digite novamente.
- **"Check the declared values, explicit time offsets and current source permission.":** corrija o campo marcado ou mantenha-o como Ausente ou Desconhecido.
- **Fonte operacional ou acesso indisponível:** use **Repetir leitura da fonte**; se o acesso continuar negado, verifique seu papel e o contexto do navio com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
