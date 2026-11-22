---
key: tug-daily-log
title: Diário do rebocador
roles: [Tug Master, Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-execution, tug-operation-history, tug-operations-offline, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## Para que serve esta página
O diário do rebocador registra, para uma data e um quarto de serviço, os **serviços, os horímetros dos motores, o combustível e os defeitos**, e os compara com os reboques executados no dia para **conciliação**. Ele responde: "Quantos serviços foram concluídos neste dia/quarto, as horas dos horímetros conferem com os tempos de execução, quanto combustível foi consumido e quais defeitos foram relatados?" A conciliação é orientativa; a revisão do Comandante é um registro humano separado.

## Quem usa
- **Tripulação / oficial de quarto:** para manter o registro do quarto em tempo real e em ordem (13.03 §6).
- **Mestre do rebocador / Comandante:** para revisar um diário enviado e verificá-lo ou devolvê-lo (13.03 §3).

## Antes de começar
- Abra o rebocador em **Operações de rebocador**.
- Criar e editar um diário exige acesso de escrita de rebocador.
- A revisão exige uma **Designação** registrada no servidor (Mestre do rebocador ou Comandante) para esse rebocador. Quem redigiu ou enviou o diário não pode revisá-lo.

## Passo a passo
1. Abra um diário existente na lista, ou escolha uma **Data do diário** e um **Quarto de serviço** e pressione **Criar diário**.
2. Informe o **Fuso horário (IANA)**.
3. Em **Horímetros dos motores**, informe a **Leitura inicial**, a **Leitura final** e, se necessário, a **Evidência de zeramento ou estouro de contagem** de cada **Horímetro**.
4. Em **Combustível consumido**, informe o valor, a unidade e a **Referência da fonte de combustível**.
5. Marque em **Reboques executados no dia** as execuções que pertencem a este diário; preencha **Linked defect identifiers** e **Resumo diário** (resumo de pelo menos 20 caracteres).
6. Pressione **Salvar**. Na linha **Conciliação**, verifique os serviços concluídos, as horas de execução, as horas dos horímetros e os defeitos relatados.
7. Quando a lista **Bloqueios** estiver vazia, pressione **Enviar para revisão do comandante**.
8. Como Comandante, escolha sua **Designação**, escreva um motivo e pressione **Comandante: verificar diário** ou **Comandante: devolver diário**.

## O que significam os estados
- **Rascunho / Enviado / Devolvido / Verificado:** o estado do diário.
- **Revisão do Comandante — Pendente / Atenção / Revisado:** Pendente = aguardando revisão; Atenção = devolvido, ou enviado enquanto a conciliação não está como Conciliado; Revisado = verificado pelo Comandante. Um diário enviado não é um diário verificado.
- **Conciliado / Revisão necessária / Incompleto:** o resultado orientativo da comparação. Os códigos de motivo são listados abaixo dele.
- **intervalo aberto:** uma execução ainda está em andamento; as horas de execução ainda não são finais.
- **Eventos de segurança — Desconhecido:** esta página não conta eventos de segurança; nenhuma conclusão de "sem eventos" é tirada.
- **Sincronizado / Aguardando sincronização / Conflito / Falhou:** o estado da sincronização. Aguardando sincronização mostra que a alteração ainda não chegou ao servidor.

## O que você não pode fazer / cuidado
- Quem redigiu o diário ou o enviou para revisão do Comandante não pode verificá-lo.
- O resultado da conciliação não é uma aprovação; a revisão é feita pelo Comandante.
- Concluir um registro e verificá-lo não são a mesma ação (13.03 §1).
- Offline, uma alteração não salva é mantida na memória desta página e enviada com sua chave original quando a conexão voltar; mantenha a página aberta. O envio e a revisão acontecem online.
- Se aparecer um conflito, não presuma que o seu próprio registro prevalece automaticamente (13.03 §6).

## Se algo der errado
- **Mensagem "The server listed missing or blocking items.":** resolva os códigos em **Bloqueios**.
- **Mensagem "The record changed or the command conflicts.":** use **Recarregar a versão mais recente** para revisar o diário atual; a alteração local mantida é descartada.
- **Mensagem "The outcome is unknown.":** **Tentar novamente** envia de novo o comando idêntico.
- **Conflito:** uma alteração mantida nesta página nunca é mesclada; use **Recarregar a versão mais recente**. Entradas diárias capturadas em **Operações offline do rebocador** que entram em conflito são resolvidas lá; elas também aparecem na **Central de conflitos materiais** (13.03 §10).

## Páginas relacionadas
