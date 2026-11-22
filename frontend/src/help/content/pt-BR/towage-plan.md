---
key: towage-plan
title: Plano de reboque
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [towage-risk-briefing, tug-assignment, tug-readiness, towage-execution]
screenshots: []
reviewed: false
---
## Para que serve esta página
O plano de reboque registra, para um serviço de rebocador enviado, as **funções e a configuração dos rebocadores, a troca de informações, os limites operacionais, a comunicação, a liberação de emergência, a contingência e os controles da zona de retorno do cabo (snap-back)**. Ele responde: "Como este serviço será executado, dentro de quais limites e com quais disposições de emergência?" A aprovação do plano é um registro humano do Comandante ou do Mestre do rebocador designado; não é uma decisão GO nem uma permissão. Os limites são dados de entrada do plano informados com sua fonte; o sistema não fornece valores padrão.

## Quem usa
- **Coordenador / planejador de operações de rebocador:** para preparar o plano e enviá-lo para aprovação (13.03 §7).
- **Mestre do rebocador / Comandante:** para confirmar a configuração, o cabo, o BP/capacidade, a comunicação, os limites, a contingência e as condições de interrupção do trabalho e aprovar ou devolver o plano (13.03 §3).

## Antes de começar
- Abra o rebocador em **Operações de rebocador** e escolha o serviço em **Selecione um serviço de reboque enviado**.
- Editar o plano exige acesso de escrita de rebocador e de escrita do plano. Os rebocadores do plano são escolhidos entre os que têm uma proposta de atribuição vigente para este serviço.
- Aprovar ou devolver exige uma **Designação** registrada no servidor (Mestre do rebocador ou Comandante) para o rebocador coordenador.

## Passo a passo
1. Leia **Estado** e **Revisão** e, em seguida, a lista **Pendências antes do envio**.
2. Em **Funções e configuração dos rebocadores**, use **Adicionar** para cada rebocador; informe **Posição**, **Método de reboque**, **Rebocador coordenador (principal)**, **Origem do cabo de reboque**, **Ponto de conexão**, **ID do equipamento do cabo de reboque** e **ID do equipamento de liberação de emergência**.
3. Escreva o texto de **Troca de informações Comandante / prático / Mestre do rebocador**.
4. Em **Limites operacionais (dados de entrada do plano)**, informe um **Valor** e a **Fonte do limite** para cada **Parâmetro**. Um novo limite começa vazio.
5. Preencha **Plano de comunicação** (canal principal e de reserva, idioma de trabalho, procedimento em caso de perda de comunicação), **Liberação de emergência**, **Contingência** e **Controles da zona de retorno do cabo (snap-back)**.
6. Salve com **Criar rascunho** ou **Salvar rascunho**; quando nada estiver faltando, pressione **Enviar para aprovação**.
7. O aprovador escolhe uma **Designação**, escreve um **Motivo** de pelo menos 10 caracteres e pressiona **Aprovar** ou **Devolver**.
8. Se um plano aprovado precisar mudar, escreva um motivo e use **Alterar**. As listas **Registros de aprovação** e **Revisões** mostram o histórico.

## O que significam os estados
- **Rascunho:** o plano pode ser editado.
- **Em revisão:** o plano foi enviado para aprovação; aguarda-se uma decisão.
- **Devolvido:** o aprovador devolveu o plano com um motivo; corrija-o e envie novamente.
- **Aprovado:** o Comandante ou o Mestre do rebocador aprovou esta revisão do plano. Isso não é uma decisão GO; a verificação de prontidão e o briefing continuam sendo necessários.
- **Desconhecido:** nenhum valor foi escolhido para o campo. "Desconhecido" para as zonas de snap-back nunca substitui "Não".
- **A solicitação de rebocador mudou depois que este plano foi salvo.:** o plano se baseia em uma revisão anterior da solicitação e deve ser revisado.

## O que você não pode fazer / cuidado
- A pessoa que redigiu ou enviou o plano não pode aprová-lo.
- Um plano com seções obrigatórias faltando não pode ser enviado; um rascunho incompleto pode ser salvo.
- O sistema não propõe nenhum valor de limite, canal ou limiar; cada valor vem do planejador com sua fonte.
- Um plano aprovado não é permissão para iniciar; o início exige confirmações de recebimento vigentes do briefing e o registro GO do Mestre do rebocador (13.03 §3).
- Os comandos ficam desativados offline.

## Se algo der errado
- **Mensagem "Required sections are missing.":** complete os códigos em **Pendências antes do envio**.
- **Mensagem "The record changed":** use **Recarregar** para revisar o plano atual.
- **Mensagem "The outcome is unknown":** **Tentar novamente** envia a mesma solicitação de novo.
- **Sem autorização:** verifique seu papel, o rebocador e o contexto de designação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
