---
key: tug-job-intake
title: Recebimento de serviços
roles: [Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-job-board, tug-command, tug-assignment, towage-plan]
screenshots: []
reviewed: false
---
## Para que serve esta página
O recebimento de serviços estrutura uma nova **solicitação de serviço de rebocador como rascunho** no contexto de rebocador selecionado. Ele responde: "Para este serviço, qual é o navio assistido, o tipo de serviço, a janela de tempo, a localização e os recursos necessários (quantidade de rebocadores, tração estática)?" A solicitação salva é uma declaração em rascunho; não é uma atribuição operacional, elegibilidade, permissão nem aprovação de segurança.

## Quem usa
- **Coordenador de operações de rebocador / Operações em terra:** para estruturar a solicitação e informar o navio assistido, o tipo de serviço, a janela de tempo e a capacidade necessária (13.03 §7).

## Antes de começar
- Na barra de contexto superior, selecione a **empresa e o rebocador corretos**.
- Você precisa de acesso de escrita de rebocador. Sem ele, o formulário não abre e "O acesso está indisponível." é exibido.
- Rascunhos incompletos podem ser salvos. Os dados operacionais obrigatórios e a revisão autorizada devem ser resolvidos antes do envio.

## Passo a passo
1. Em **Solicitação e navio assistido**, preencha **Fonte da solicitação**, **Referência do cliente**, **Navio assistido**, **IMO do navio assistido**, **Tipo de serviço**, **Prioridade** e **Responsável pela solicitação**. Listas sem escolha permanecem "Desconhecido".
2. Em **Horário e localização solicitados**, informe **Início solicitado (UTC)**, **Fim solicitado (UTC, condicional)**, **Área de operação**, **Ponto de embarque** e **Ponto de destino**. Os horários são em UTC.
3. Em **Necessidades de recursos e notas**, informe **BP exigida (toneladas, decimal exato)**, **Quantidade exigida de rebocadores**, **Requisitos especiais** e **Observações**.
4. Pressione **Salvar rascunho**. Em caso de sucesso, o detalhe do serviço abre com "Draft saved. Operational approval remains unavailable."
5. Para abandonar suas edições, use **Descartar alterações**.
6. No detalhe do serviço, use **Enviar solicitação** para registrar a solicitação para planejamento. Se faltarem dados obrigatórios, o envio é bloqueado e os campos que faltam são listados.

## O que significam os estados
- **Rascunho:** a solicitação está salva e pode ser editada; ainda não foi enviada.
- **Enviado:** a solicitação está registrada para planejamento. Não é uma aprovação, atribuição nem decisão de prontidão.
- **Cancelado:** a solicitação está encerrada; não pode ser reiniciada.
- **Desconhecido:** nenhum valor foi escolhido para o campo. Desconhecido não é um valor padrão e nunca substitui "Não" ou "Não aplicável".
- **Verifique este valor.:** o servidor rejeitou este campo; corrija-o e salve novamente.

## O que você não pode fazer / cuidado
- Esta página não atribui nenhum rebocador e nunca declara o serviço pronto. A proposta de atribuição é uma etapa separada e nunca se sobrepõe à decisão de segurança a bordo do Mestre do rebocador (13.03 §7).
- Uma solicitação não pode ser enviada enquanto faltarem dados obrigatórios; um rascunho incompleto ainda pode ser salvo.
- Os campos de uma solicitação enviada não podem ser alterados neste formulário; uma solicitação cancelada não pode ser reiniciada, crie uma nova em seu lugar.
- Offline, o rascunho fica mantido apenas nesta página e não pode ser salvo.

## Se algo der errado
- **Mensagem "Save outcome uncertain":** a tentativa exata é mantida; depois de reconectar, use **Tentar novamente** para enviar o mesmo comando. Descartar as entradas locais não cancela um comando já enviado.
- **Mensagem "Conflict":** seu rascunho é mantido; descarte e recarregue antes de uma nova tentativa.
- **Verificar campos:** corrija os campos marcados.
- **O acesso está indisponível:** verifique seu papel e o contexto do rebocador; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
