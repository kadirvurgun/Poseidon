---
key: high-risk-work
title: Trabalhos de alto risco
roles: [DPA / HSEQ, Master, Tug Master]
permissions: [hse.risk.read, hse.high_risk_work.read]
related: [risk-assessment, risk-template-library, safety-events]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Trabalhos de alto risco mostra, em uma única lista **somente leitura**, as exceções de trabalhos da frota como trabalho a quente, espaço confinado, trabalho em altura, isolamento elétrico, manutenção crítica, reboque e abastecimento. Ela responde: "Qual trabalho de alto risco tem uma pendência em risco, permissão, barreira crítica, tripulação ou equipamento, e quem está com a próxima ação?" Cada dimensão é mostrada separadamente; não há pontuação agregada. As ações abrem o registro responsável; esta página não altera nenhum registro e não toma nenhuma decisão de início de trabalho (13.03 §4).

## Quem usa
- **DPA / HSEQ:** como parte da governança de riscos, acompanha as exceções de trabalhos de alto risco em toda a frota (13.03 §4).
- **Comandante e Comandante do rebocador:** veem os bloqueios dos trabalhos de alto risco na própria embarcação e vão ao registro responsável. A decisão de iniciar ou interromper o trabalho continua sendo deles (13.03 §2, §3).

## Antes de começar
- A visualização exige as permissões de leitura de riscos e de leitura de trabalhos de alto risco. Sem elas, você vê "You do not have access to the high-risk work view."
- A lista abrange apenas as frotas e embarcações do seu escopo.
- Offline, a lista mostrada pode estar desatualizada.

## Passo a passo
1. Escolha os filtros **Frota**, **Embarcação** e **Tipo de trabalho**. **Todos** remove um filtro.
2. Na tabela, leia separadamente os estados de **Risco**, **Permissão**, **Barreira crítica**, **Tripulação** e **Equipamento** de cada trabalho. Os selos **Bloqueios críticos** e **Itens para revisão** mostram contagens.
3. Use as colunas **Próxima ação** e **Responsável / prazo** para ver quem precisa fazer o quê.
4. Para ver por que uma linha está em seu estado, pressione **Por quê**. Abre-se uma tabela com **Dimensão**, **Estado** e **Justificativa**; feche-a com **Ocultar**.
5. Para alterar qualquer coisa, siga o link do trabalho ou **Abrir registro**; a ação é realizada na própria página do registro responsável.
6. Navegue pelas páginas com **Anterior** e **Próximo**. **Avaliado em** e **Cobertura das fontes** mostram quando e a partir de quais fontes a lista foi calculada.

## O que significam os estados
- **Pronto / Condicional / Atenção / Não pronto / Bloqueado:** o estado da dimensão em sua fonte. Uma dimensão Pronto não significa que o trabalho pode começar; a decisão continua sendo da pessoa autorizada.
- **Revisão necessária:** uma pessoa precisa revisá-lo.
- **Desconhecido:** a fonte está indisponível; nunca é considerado pronto. O POSEIDON ainda não tem uma fonte de permissão de trabalho, por isso a coluna **Permissão** sempre mostra **Desconhecido**.
- **Íntegro / Degradado / Com falha:** o estado da barreira crítica.
- **Não classificado:** o tipo de trabalho não é conhecido ou sua criticidade não foi determinada; ele não é excluído da lista.
- **Aguardando a fonte / Resolver parada ou contestação / Renovar item vencido / Concluir item obrigatório / Reavaliar / Abrir registro do responsável:** a próxima ação, mostrada em uma ordem de prioridade fixa.
- **Vencido e não resolvido / Não planejado:** a data de vencimento ou o início planejado não é conhecido.

## O que você não pode fazer / cuidado
- Esta página é somente leitura; ela não altera, não aprova nem permite nada.
- Não há pontuação agregada; um trabalho com um bloqueio crítico sempre fica no topo, e outras dimensões estarem em ordem nunca o oculta.
- Não interprete **Desconhecido** como pronto. Uma permissão ativa histórica nunca é uma permissão válida atual.
- A decisão de iniciar ou interromper o trabalho continua sendo do Comandante ou do Comandante do rebocador.

## Se algo der errado
- **O serviço está indisponível. Tente novamente.** Pressione **Tentar novamente**.
- **Você está offline. A lista exibida pode estar desatualizada.** Quando estiver online, pressione **Recarregar**.
- Se um estado parecer inesperado, abra a tabela **Por quê** e pergunte ao responsável pelo registro ou ao responsável pelo controle (13.03 §10).
- Sem acesso, verifique seu papel e o contexto da embarcação; se necessário, contate o administrador do tenant (13.03 §10).

## Páginas relacionadas
