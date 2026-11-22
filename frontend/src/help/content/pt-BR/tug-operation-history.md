---
key: tug-operation-history
title: Histórico do rebocador
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [towage-execution, tug-daily-log, tug-job-board, tug-operations-offline]
screenshots: []
reviewed: false
---
## Para que serve esta página
O histórico de operações mostra o **histórico somente leitura** de serviços de rebocador, execuções, diários e evidências. Ele responde: "Quais serviços terminaram com qual resultado, as execuções foram verificadas, existe um registro de evidência e quais diários pertencem a esses serviços?" Registros cancelados e abortados continuam visíveis; nada é excluído ou reclassificado.

## Quem usa
- **Coordenador de operações de rebocador / Operações em terra:** para acompanhar a completude das evidências e os registros de eventos e de histórico dos serviços concluídos (13.03 §7).
- **Mestre do rebocador:** para ver os serviços anteriores e as linhas do tempo de execução do seu próprio rebocador (13.03 §3).

## Antes de começar
- Abra o rebocador em **Operações de rebocador**; sem um rebocador selecionado, a página mostra "Select a tug vessel to continue."
- O acesso de leitura de rebocador é suficiente; a página não altera nenhum dado.
- O histórico é lido do servidor; não pode ser atualizado offline.

## Passo a passo
1. Em **Filtros**, defina opcionalmente **ID do rebocador**, **De (data UTC)**, **Até (data UTC)** e **Resultado** e pressione **Aplicar filtros**. O intervalo de datas é de no máximo 400 dias.
2. Em cada linha da tabela, leia **Serviço** (número do serviço, navio assistido, tipo de serviço, estado do plano e do briefing), **Hora da atividade**, **Resultado** e **Evidência**.
3. Em **Execuções**, veja o estado de cada execução, o estado da verificação, a duração, o registro do Comandante e o número de entradas de evidência; use **Abrir linha do tempo da execução** para ir ao console de execução.
4. Use os links em **Diários** para abrir o diário relacionado. A nota "not linked by the log" mostra que o diário não vincula esta execução.
5. Use **Carregar mais** para registros mais antigos. **Lido em** mostra quando os dados foram lidos.

## O que significam os estados
- **Não iniciado / Em andamento / Concluído (registrado) / Abortado / Cancelado / Misto:** o resultado geral do serviço. Concluído (registrado) não significa que o serviço foi verificado.
- **Verificado, evidência registrada / Verificado, sem entrada de evidência / Verificação rejeitada / Evidência não verificada / Desconhecido:** o estado das evidências e da verificação.
- **verificação pendente / verificado / verificação rejeitada:** o estado de verificação de uma execução individual.
- **Nenhum:** não existe registro vinculado para este serviço. Isso não significa que nenhum registro era necessário.

## O que você não pode fazer / cuidado
- Nenhum registro é editado, excluído ou verificado nesta página.
- Nenhuma exclusão silenciosa: serviços cancelados e execuções abortadas ou rejeitadas permanecem no histórico.
- Concluído ≠ Verificado: a verificação é o registro de outra pessoa.
- Os dados mostrados são o estado em **Lido em**; não são atualizados offline.

## Se algo der errado
- **Mensagem "The filter is not valid":** verifique se o ID do rebocador é um UUID e se o intervalo de datas não ultrapassa 400 dias.
- **Mensagem "The history could not be loaded.":** verifique sua conexão e pressione **Tentar novamente**.
- **"You do not have access to this tug scope.":** verifique seu papel e o contexto do rebocador; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **O registro não pode ser verificado:** verifique a referência da evidência e sua fonte na linha do tempo da execução; entre em contato com o verificador (13.03 §10).

## Páginas relacionadas
