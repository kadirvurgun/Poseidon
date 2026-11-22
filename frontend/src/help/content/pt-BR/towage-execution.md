---
key: towage-execution
title: Execução do reboque
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.execution.verify]
related: [tug-readiness, towage-risk-briefing, towage-plan, tug-daily-log, tug-operation-history]
screenshots: []
reviewed: false
---
## Para que serve esta página
O console de execução registra a **execução real** de um serviço de rebocador enviado como uma linha do tempo: o início, as observações, as ações **Suspender / Retomar / Abortar / Concluir** e a verificação posterior. Ele responde: "Quando a operação começou, quando o cabo de reboque foi conectado e liberado, quais observações foram registradas, em que estado está a operação e o registro foi verificado?" O início e as transições são registros humanos do Mestre do rebocador ou do Comandante a bordo do rebocador executante; o console nunca emite um GO.

## Quem usa
- **Mestre do rebocador / Comandante:** para iniciar a operação e registrar a linha do tempo de início/suspensão/retomada/aborto/conclusão e suas evidências (13.03 §3).
- **Verificador (uma pessoa com autoridade de verificação da execução):** para verificar um registro concluído ou abortado ou rejeitar sua verificação (13.03 §1).
- **Coordenador de operações de rebocador / Operações em terra:** para acompanhar a completude das evidências e o histórico dos serviços concluídos; eles não podem iniciar nem abortar uma operação (13.03 §7).

## Antes de começar
- Abra o rebocador em **Operações de rebocador** e escolha o serviço em **Selecione um serviço de reboque enviado**.
- Para iniciar, tudo isto deve estar vigente: a designação ativa do rebocador, um **Plano de reboque** aprovado (o rebocador deve estar no plano), um **Risco e briefing de reboque** emitido e com ciência de todos os participantes, e um registro **GO** na **Verificação de prontidão operacional** vinculado à avaliação atual.
- O início e as transições exigem uma **Designação** registrada no servidor (Mestre do rebocador ou Comandante) para esse rebocador.

## Passo a passo
1. Na seção **Controle de início**, leia a **Decisão de prontidão** e os **Bloqueios** de cada rebocador.
2. Se não houver bloqueios, leia o texto de confirmação como a pessoa designada a bordo, escolha sua **Designação**, informe opcionalmente uma **Posição inicial** e pressione **Iniciar operação**.
3. Depois que a execução estiver aberta, acompanhe **Estado**, **Verificação** e os campos do registro (cabo de reboque conectado/liberado, carga máxima de reboque observada, comunicação, guincho, teste de liberação de emergência, minutos ativos).
4. Em **Registrar observação**, escolha o tipo de observação (por exemplo cabo de reboque conectado, carga de reboque, teste de comunicação, condições meteorológicas, estado do guincho, teste de liberação de emergência, posição, evidência, nota), informe os valores e pressione **Registrar observação**.
5. Em **Ações do comandante do rebocador**, escolha a **Ação** (Suspender, Retomar, Abortar, Concluir) e informe sua **Designação** e um **Motivo**. Abortar e concluir exigem um motivo de pelo menos 20 caracteres e abrem o campo **Posição final**; a conclusão não é aceita sem uma posição final.
6. Se houver **Impedimentos para conclusão** listados, resolva-os; por exemplo, se o cabo de reboque ainda estiver conectado, registre primeiro a sua liberação.
7. Como verificador, escreva um motivo e pressione **Verificar registro** ou **Rejeitar verificação**.
8. Veja todos os eventos em ordem na **Linha do tempo**.

## O que significam os estados
- **Ativo / Suspenso / Concluído / Abortado:** o estado da operação. Concluído não significa que o registro foi verificado.
- **Pendente / Verificado / Rejeitado:** o estado da verificação. Concluído ≠ Verificado; a verificação é o registro de outra pessoa.
- **Boa / Perdida / Aprovado / Reprovado / Condicional / Desconhecido:** o resultado das observações de comunicação, guincho e liberação de emergência. Desconhecido mostra que nada foi registrado; nunca substitui "Boa" ou "Aprovado".
- **Eventos de segurança — Desconhecido:** este console não conta eventos de segurança; nenhuma conclusão de "sem eventos" é tirada.
- **GO (registro humano) / NO GO (registro humano):** a decisão registrada pelo Mestre do rebocador ou pelo Comandante na verificação de prontidão.

## O que você não pode fazer / cuidado
- Usuários em terra, IA e identidades do sistema não podem iniciar nem abortar uma operação. Uma designação feita em terra nunca se sobrepõe à decisão de segurança a bordo do Mestre do rebocador (13.03 §7).
- Somente o Mestre do rebocador ou o Comandante designado pode suspender, retomar, abortar ou concluir. Abortar está sempre disponível enquanto a operação está em andamento.
- A pessoa que iniciou ou encerrou a operação não pode verificar o mesmo registro; a verificação ocorre depois que a operação terminou.
- O registro só aceita acréscimos; eventos registrados nunca são excluídos nem alterados.
- Nenhum comando é enviado offline. As observações podem ser colocadas na fila em **Operações offline do rebocador**; o início, as transições e a verificação continuam exigindo conexão.

## Se algo der errado
- **Mensagem "The server listed missing or blocking items.":** resolva os códigos em **Bloqueios** ou **Impedimentos para conclusão** (por exemplo plano não aprovado, ciência do briefing pendente, registro GO ausente ou não vigente).
- **Mensagem "The record changed or the command conflicts.":** use **Recarregar a versão mais recente** para revisar o registro.
- **Mensagem "The outcome is unknown.":** **Tentar novamente** envia de novo o comando idêntico.
- **Sem autorização:** verifique seu papel, o rebocador e o contexto de designação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
