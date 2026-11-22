---
key: noon-report
title: Relatório do meio-dia
roles: [Master, Crew / OOW]
permissions: [daily.noon.read, daily.noon.write, daily.noon.review]
related: [deck-daily-log, engine-daily-log, fuel-bunker-log, port-activity-sof, offline-conflict-center, today]
screenshots: [1]
reviewed: false
---
## Para que serve esta página
A página do relatório do meio-dia mantém as **declarações diárias de meio-dia** do navio selecionado como rascunhos, com a versão de origem registrada. Ela responde: "O que declaramos sobre horário do relatório, viagem, posição, distância, tempo, carga, horas de máquinas, combustível e ROB, e o Comandante registrou uma revisão desta revisão exata?" Um rascunho salvo é uma declaração não verificada. Salvá-lo não estabelece aprovação do Comandante nem do Chefe de máquinas, e a página não tira dele nenhuma conclusão sobre emissões, garantia de combustível ou prontidão.

## Quem usa
- **Oficial que prepara o relatório (Crew / OOW):** para preencher e salvar o rascunho de meio-dia para o navio e a data corretos (13.03 §6).
- **Comandante:** para revisar o relatório salvo e registrar essa revisão como um ato humano (13.03 §2). Somente uma designação com a função de Comandante pode registrar uma revisão do relatório de meio-dia.

## Antes de começar
- Na barra de contexto superior, selecione a **empresa e o navio corretos**. Se nenhum navio estiver selecionado, a página mostra "Select a ship".
- A leitura exige `daily.noon.read`. Criar e salvar rascunhos exige `daily.noon.write` e direitos de edição neste navio; sem eles, você vê "Read-only access. Editing and saving are not granted."
- Registrar uma revisão exige `daily.noon.write` com direitos de edição neste navio, `daily.noon.review` e uma designação vigente de Comandante para este registro exato.

## Passo a passo
1. Escolha o navio. A lista **Rascunhos registrados** mostra cada relatório por data e revisão com o selo "Draft · review required". Use **Rascunhos anteriores** / **Mais rascunhos** para navegar.
2. Pressione **Novo rascunho** para abrir **Novo rascunho diário**, ou abra um relatório listado para **Editar rascunho registrado**.
3. Preencha os grupos (**Relatório e viagem**, **Posição e atividade**, **Tempo e carga**, **Máquinas, combustível e ROB**). Para cada campo, escolha um **Estado dos dados**: **Conhecido**, **Desconhecido** ou **Ausente**. Rascunhos incompletos podem ser salvos.
4. Pressione **Salvar rascunho**. A página confirma "Draft saved. Human review and operational approval remain separate." Cada salvamento adiciona uma revisão em **Histórico de revisões registradas**.
5. Em um relatório salvo, o Comandante abre **Registro de revisão humana**, escolhe uma **Nomeação humana autorizada**, uma **Entrada de revisão** ("Registrar declaração revisada (não avaliada)" ou "Devolver para correção") e escreve uma **Justificativa da revisão**; em seguida, pressiona **Registrar entrada de revisão**.
6. Se for preciso registrar combustível ou um evento portuário, use **Usar fonte operacional salva** e **Abrir registro de combustível da fonte salva** ou **Abrir evento portuário da fonte salva**. Quantidades, unidades e aprovações não são copiadas.

## O que significam os estados
- **Rascunho — declarações não verificadas / Declaração não verificada:** os valores são declarados por uma pessoa; ninguém os verificou.
- **Conhecido / Desconhecido / Ausente:** o estado dos dados de um campo. Desconhecido não significa "Não", "Pronto" nem "Não aplicável", e Ausente não é zero.
- **Pendente — revisão não registrada:** nenhuma revisão humana foi registrada para esta revisão.
- **Metadados humanos registrados — não avaliados / Registrado — não avaliado:** existe uma entrada de revisão do Comandante para a revisão mostrada. O registro da revisão não estabelece verificação, envio nem aprovação de segurança do Comandante.
- **Revisão histórica:** a entrada de revisão pertence a uma revisão mais antiga e não revisa o rascunho salvo atual.

## O que você não pode fazer / cuidado
- O sistema nunca revisa nem finaliza o relatório. Registrar uma revisão é um ato humano do Comandante designado (13.03 §2).
- Quem foi autor de qualquer revisão do diário não pode revisá-lo. Nenhuma designação é oferecida a um autor e a página informa "You authored this log revision; an independent reviewer must record the review."; o servidor também recusa essa revisão por segregação de funções. Se o Comandante escreveu o relatório, um revisor independente com autoridade de revisão ainda precisa registrar a revisão.
- Um novo salvamento cria uma nova revisão; a revisão humana anterior permanece em sua própria revisão. Revise novamente após alterações.
- Uma referência digitada em texto livre não pode substituir uma consulta autorizada; mantenha o campo como Ausente ou Desconhecido.
- Offline, apenas campos explicitamente classificados podem ir para a fila criptografada. Campos de revisão, aprovação e verificação são excluídos.

## Se algo der errado
- **"The record changed. Your inputs are retained.":** alguém salvou uma versão mais recente. Descarte explicitamente e recarregue antes de uma nova tentativa.
- **"Save outcome unknown":** os campos ficam congelados; use **Repetir comando salvo exato**, não digite novamente.
- **"No current source-authorized human appointment is available for this record.":** verifique seu papel e o contexto do navio com o administrador do seu tenant (13.03 §10).
- **"Human conflict resolution required":** uma captura offline está em conflito com o servidor. Abra **Revisar conflito** ou a Central de conflitos e escolha explicitamente; nada é mesclado automaticamente (13.03 §10).
- **Fontes diárias indisponíveis ou acesso negado:** use **Repetir leitura das fontes**; se persistir, informe ao suporte o horário e seus passos.

## Páginas relacionadas
