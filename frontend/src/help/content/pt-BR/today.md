---
key: today
title: Hoje
roles: [Master, Tug Master, Chief Engineer, Crew / OOW]
permissions: [canRead]
related: [fleet, tug-job-board, offline-conflict-center, sources]
screenshots: [1, 2, 3, 4, 5, 6]
reviewed: false
---
## Para que serve esta página
"Hoje" reúne em uma única lista, para o navio ou rebocador que você selecionar, os **alertas que precisam da sua atenção agora**. Ela responde: "Qual registro deste navio está aguardando uma decisão ou uma verificação hoje, e qual fonte diz isso?" A página não decide nada; ela mostra o alerta, sua fonte e o próximo passo.

## Quem usa
- **Comandante** e **Mestre do rebocador:** para ver as exceções do dia, seu responsável e o próximo passo (13.03 §2, §3).
- **Chefe de máquinas:** para acompanhar os alertas do plano de manutenção (13.03 §5).
- **Crew / OOW:** para ver o trabalho que cabe a eles (13.03 §6).

## Antes de começar
- Na barra de contexto superior, confirme que a **empresa e o navio corretos** estão selecionados (13.03 §1). Um alerta que você vê pertence ao navio selecionado.
- Você precisa de acesso de leitura ao espaço de trabalho para abrir esta página.
- Se nenhum navio estiver selecionado, a página mostra primeiro a lista "Selecione um navio".

## Passo a passo
1. Selecione o navio ou rebocador em que você está trabalhando. O navio selecionado aparece como "Navio atual"; use "Trocar de navio" para alterá-lo.
2. Veja a seção **Cobertura das fontes** no topo. Ela mostra quais fontes (regulatórias, manutenção planejada, operações de rebocador) puderam ser lidas.
3. Leia os cartões de alerta de cima para baixo. Cada cartão vem de uma fonte e sugere um próximo passo (por exemplo "Revisar rascunho", "Revisar plano de manutenção", "Ver serviço de rebocador").
4. Pressione **Por quê?** para ver o motivo por trás de um cartão. A seção mostra a regra, o registro de origem, sua revisão, o horário de leitura e quaisquer entradas desconhecidas.
5. Siga o link do cartão até a página do próprio registro e aja lá, dentro das suas permissões.
6. Use **Atualizar** para ler os dados novamente.

## O que significam os estados
- **Disponível (não verificado):** a fonte foi lida. Isso não significa que seu conteúdo foi verificado.
- **Ausente / Indisponível neste contexto:** a fonte não pôde ser lida para este navio. Um alerta que você não vê não significa que está tudo bem.
- **Visão parcial:** algumas fontes não puderam ser lidas. Uma contagem zero não significa "nenhum problema".
- **Todas as fontes listadas foram lidas e não retornaram itens:** nenhum alerta foi retornado. Isso não é uma liberação de segurança.
- **Desconhecido:** o valor está ausente na fonte ou não pôde ser determinado. Desconhecido não significa "Não", "Pronto" nem "Não aplicável".
- **Revisão necessária / Atenção ao planejamento / Aviso orientativo:** a prioridade do cartão. Um cartão com revisão necessária aguarda que uma pessoa autorizada o examine.
- **Concluído ≠ Verificado:** concluir um registro não significa que um verificador autorizado o confirmou (13.03 §1).

## O que você não pode fazer / cuidado
- Esta página não toma nenhuma decisão de saída, de operação ou de segurança. Essa decisão cabe ao Comandante ou ao Mestre do rebocador autorizado.
- Nenhum comando é executado a partir desta página; as ações acontecem na página do próprio registro.
- Os dados mostrados offline são antigos e não verificados. Os detalhes de "Por quê?" não abrem offline.
- Um horário de leitura ou de gravação não prova que a fonte está correta.

## Se algo der errado
- **Offline:** continue seu trabalho crítico e acompanhe o estado da sincronização. Se aparecer um conflito, vá ao Centro de conflitos (13.03 §10).
- **Acesso negado / Navio indisponível no seu escopo:** verifique seu papel e o contexto do navio; se necessário, entre em contato com o administrador do seu tenant.
- **Alerta inesperado:** abra "Por quê?"; se a regra ou a fonte parecerem erradas, encaminhe ao responsável pelo controle ou a um especialista no assunto.
- **Fonte indisponível / Muitas solicitações:** aguarde um momento e use "Tentar novamente".
- **A resposta do servidor não passou na validação:** nada dela é mostrado; relate ao suporte com uma captura de tela, o horário e os seus passos.

## Páginas relacionadas
