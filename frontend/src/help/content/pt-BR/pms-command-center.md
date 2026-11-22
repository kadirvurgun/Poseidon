---
key: pms-command-center
title: Central de comando PMS
roles: [Chief Engineer, Technical Superintendent, Master]
permissions: [equipment.read]
related: [work-order-center, defects, critical-spares, departure-signoff, today]
screenshots: []
reviewed: false
---
## Para que serve esta página
A central de comando PMS reúne em uma única tela as **exceções** de manutenção da embarcação selecionada. Ela responde: "Esta embarcação tem trabalhos críticos atrasados, ordens de serviço abertas, alertas de defeito ou faltas de peças críticas, e por quê?" A página é uma visão consultiva de exceções; não é uma decisão de aptidão, prontidão ou segurança. Os itens em bom estado não são listados, apenas contados.

## Quem usa
- **Chefe de máquinas / Superintendente técnico:** para acompanhar trabalhos atrasados, ordens de serviço abertas, alertas de defeito e faltas de peças críticas (13.03 §5).
- **Comandante:** para ver o impacto de restrição operacional dos registros de PMS e de defeitos (13.03 §2).

## Antes de começar
- Você precisa da permissão de leitura de equipamentos (`equipment.read`) para ver a página.
- Cada seção depende da permissão de leitura do seu próprio módulo. Por exemplo, sem permissão de leitura de defeitos, a seção **Alertas de condição** permanece "Acesso necessário: não exibido".
- Escolha primeiro uma embarcação. Sem embarcação, a página mostra apenas a lista **Escolha uma embarcação.**.

## Passo a passo
1. Escolha a embarcação na lista. Se nenhuma estiver no seu escopo, a página informa "Nenhuma embarcação no seu escopo.".
2. Leia o selo **Resumo de exceções** e o horário da **Última atualização** no topo.
3. Revise as seções: **Trabalhos críticos atrasados**, **Trabalhos devidos, atrasados e a revisar**, **Ordens de serviço abertas**, **Alertas de condição** e **Faltas de peças críticas**.
4. Abra **Por quê** em um item para ver os códigos de motivo registrados e, se houver, o horário **Observado**.
5. Use **Abrir registro de origem** para ir até a fonte. Um item de defeito abre [Defeitos e medidas temporárias](/app/defects), um item de ordem de serviço abre [Central de ordens de serviço](/app/work-order-center).
6. Pressione **Atualizar** para ler os dados novamente.

## O que significam os estados
- **Bloqueado / Revisão necessária / Atenção:** o estado do resumo de exceções. Os detalhes estão nos itens das seções.
- **Sem exceções abertas (consultivo):** as fontes lidas não retornaram nenhuma exceção aberta. Isso não é um resultado de prontidão ou de aptidão.
- **Crítico / Atenção / Revisão necessária:** a gravidade de um item.
- **Fonte indisponível: desconhecido, não zero:** a fonte da seção não pôde ser lida. Desconhecido não significa "zero", "nenhum problema" nem "Pronto".
- **Acesso necessário: não exibido:** você não tem permissão para esta seção; nem o conteúdo nem a contagem são exibidos.
- **Sem exceções desta fonte:** a seção foi lida e não retornou nenhuma exceção. O número de itens em bom estado é exibido separadamente.
- **Dados desatualizados / Atualidade desconhecida:** a leitura por trás do item não é atual, ou sua atualidade é desconhecida.

## O que você não pode fazer / cuidado
- A página não altera nenhum registro; ela apenas lê. Ordens de serviço, defeitos e peças são tratados em suas próprias páginas.
- A página nunca declara uma embarcação "pronta" ou "segura". A decisão de partida cabe ao Comandante (13.03 §2).
- Um defeito só conta como encerrado por meio de um registro humano de encerramento, e apenas para a revisão da declaração que foi encerrada. Se a declaração mudar depois, o item volta para revisão.
- Os dados exibidos offline podem estar desatualizados, e **Atualizar** não fica disponível.

## Se algo der errado
- **Não foi possível carregar o registro:** verifique sua conexão e use **Recarregar**.
- **Você não tem a permissão do servidor para esta ação ou registro:** verifique sua função e o contexto da embarcação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **Exceção inesperada:** abra **Por quê** e revise o registro de origem com o responsável ou o especialista no assunto (13.03 §10).
- **Erro da aplicação:** informe ao suporte o Correlation ID, a tela, o horário e os passos (13.03 §10).

## Páginas relacionadas
