---
key: deck-daily-log
title: Diário de convés
roles: [Crew / OOW, Master]
permissions: [daily.deck.read, daily.deck.write, daily.deck.review]
related: [noon-report, engine-daily-log, port-activity-sof, offline-conflict-center, today]
screenshots: []
reviewed: false
---
## Para que serve esta página
O diário de convés mantém os **registros do serviço de quarto no convés** do navio selecionado como rascunhos, com a versão de origem registrada. Ele responde: "O que o quarto declarou para esta data e este período sobre posição, rumo, velocidade, tempo, visibilidade, navegação, trabalhos portuários e de carga, lastro, trabalhos de convés, observações de segurança e passagem de serviço, e foi registrada uma revisão para esta revisão exata?" Uma entrada salva é uma declaração não verificada, não um diário aprovado.

## Quem usa
- **Oficial de quarto (Crew / OOW):** para manter o registro do quarto em tempo real e em ordem cronológica (13.03 §6).
- **Comandante ou Imediato:** para revisar o diário salvo e registrar essa revisão como um ato humano (13.03 §2). Somente uma designação com a função de Comandante ou Imediato pode registrar uma revisão do diário de convés.

## Antes de começar
- Na barra de contexto superior, selecione a **empresa e o navio corretos**. Se nenhum navio estiver selecionado, a página mostra "Select a ship".
- A leitura exige `daily.deck.read`. Criar e salvar exige `daily.deck.write` e direitos de edição neste navio; caso contrário, você vê "Read-only access. Editing and saving are not granted."
- Registrar uma revisão exige `daily.deck.write` com direitos de edição neste navio, `daily.deck.review` e uma designação vigente de Comandante ou Imediato para este registro exato.

## Passo a passo
1. Escolha o navio. **Rascunhos registrados** lista cada diário por data e revisão com "Draft · review required".
2. Pressione **Novo rascunho** para um **Novo rascunho diário**, ou abra um diário listado para **Editar rascunho registrado**.
3. Preencha primeiro o **Contexto do serviço de quarto** (**Data do diário**, **Serviço de quarto / período**, **Referência do oficial de quarto**). A lista de oficiais oferece apenas pessoas disponíveis na data declarada do diário.
4. Preencha **Navegação e tempo**, **Atividades portuárias e de carga** e **Trabalhos de convés e passagem de serviço**. Para cada campo, escolha **Conhecido**, **Desconhecido** ou **Ausente**.
5. Pressione **Salvar rascunho** durante o quarto ou logo depois dele. Cada salvamento adiciona uma revisão ao **Histórico de revisões registradas**; as revisões anteriores permanecem inalteradas.
6. O Comandante ou o Imediato abre **Registro de revisão humana**, escolhe uma **Nomeação humana autorizada** e uma **Entrada de revisão**, escreve uma **Justificativa da revisão** e pressiona **Registrar entrada de revisão**.

## O que significam os estados
- **Rascunho — declarações não verificadas / Declaração não verificada:** declarado por uma pessoa; não verificado.
- **Conhecido / Desconhecido / Ausente:** o estado dos dados do campo. Desconhecido não significa "Não", "Pronto" nem "Não aplicável".
- **Pendente — revisão não registrada:** não existe revisão humana para esta revisão.
- **Metadados humanos registrados — não avaliados:** existe uma entrada de revisão para esta revisão. Ela não estabelece verificação, envio nem aprovação de segurança do Comandante.
- **Registrar declaração revisada (não avaliada) / Devolver para correção:** os dois tipos de entrada de revisão que um revisor pode registrar.
- **Revisão histórica:** a revisão pertence a uma revisão mais antiga, não ao rascunho atual.

## O que você não pode fazer / cuidado
- O sistema nunca revisa nem finaliza o diário. A revisão é um ato humano do Comandante ou do Imediato designado (13.03 §2).
- Quem foi autor de qualquer revisão do diário não pode revisá-lo. Nenhuma designação é oferecida a um autor e a página informa "You authored this log revision; an independent reviewer must record the review."; o servidor também recusa essa revisão por segregação de funções.
- Mantenha as entradas em tempo real e em ordem cronológica; não reescreva um quarto antigo para que pareça novo (13.03 §6).
- Não altere o registro revisado de outra pessoa; um salvamento posterior é uma nova revisão e precisa de uma nova revisão humana (13.03 §6).
- A captura offline cobre apenas campos explicitamente classificados. Um conflito offline nunca é mesclado automaticamente; não presuma que o seu registro prevalece (13.03 §6).

## Se algo der errado
- **"The record changed. Your inputs are retained.":** descarte explicitamente e recarregue antes de uma nova tentativa.
- **"Save outcome unknown":** use **Repetir comando salvo exato**; não digite os campos novamente.
- **Referência do oficial indisponível:** mantenha o campo como Ausente ou Desconhecido; um nome digitado não substitui a consulta.
- **"Human conflict resolution required":** abra **Revisar conflito** ou a Central de conflitos e escolha explicitamente (13.03 §10).
- **Nenhuma designação disponível:** verifique seu papel e o contexto do navio com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
