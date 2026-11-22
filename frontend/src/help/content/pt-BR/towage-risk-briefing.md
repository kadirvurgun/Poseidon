---
key: towage-risk-briefing
title: Risco e briefing de reboque
roles: [Tug Master, Master, Crew, Tug Operations Coordinator]
permissions: [mode.tug, tug.read, tug.write, hse.risk.read]
related: [towage-plan, tug-readiness, towage-execution, tug-operations-offline]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página prepara e emite o **registro de risco e briefing de reboque** de um serviço de rebocador enviado e coleta as **confirmações de recebimento** dos participantes. Ela responde: "Quais condições, perigos, barreiras preventivas e de mitigação, autoridade para interromper o trabalho e disposições de emergência foram apresentados no briefing, qual é o estado da avaliação de risco vinculada e todos os participantes confirmaram o recebimento da revisão emitida?" Emitir o briefing é um registro humano do Comandante ou do Mestre do rebocador designado; não é uma decisão GO nem uma permissão. Um briefing incompleto bloqueia READY.

## Quem usa
- **Mestre do rebocador / Comandante:** para preparar e emitir o briefing e garantir que nenhuma decisão READY seja tomada antes que o risco e o briefing estejam completos (13.03 §3).
- **Tripulação e outros participantes (prático, imediato, marinheiros de convés, maquinista):** para confirmar o recebimento do briefing emitido ou contestá-lo no contexto de função correto (13.03 §6).
- **Coordenador de operações de rebocador:** para acompanhar o estado do briefing e das confirmações de recebimento (13.03 §7).

## Antes de começar
- Abra o rebocador em **Operações de rebocador** e escolha o serviço em **Selecione um serviço de reboque enviado**.
- O **Plano de reboque** do serviço deve estar aprovado e a avaliação de risco vinculada também; caso contrário, o briefing não pode ser emitido.
- A emissão exige uma **Designação** registrada no servidor (Mestre do rebocador ou Comandante) para o rebocador coordenador. A emissão também exige acesso de leitura à avaliação de risco e acesso de escrita de rebocador no rebocador coordenador. A confirmação de recebimento é apenas para as pessoas da lista de participantes.

## Passo a passo
1. Na linha superior, leia **Estado**, **Revisão**, **Confirmação de recebimento**, **Estado do risco** e **Estado do plano**.
2. Em **Dado de prontidão (um briefing incompleto bloqueia READY)**, veja os resultados e os códigos de motivo enviados à verificação de prontidão.
3. Preencha **Condições confirmadas no briefing** (condições meteorológicas, corrente, visibilidade), **Perigos**, **Barreiras preventivas**, **Barreiras de mitigação**, **Autoridade para interromper o trabalho** e **Disposições de emergência**.
4. Informe o **ID da avaliação de risco vinculada**; em **Participantes**, use **Adicionar** para informar o **ID da pessoa** e a **Função** de cada participante.
5. Salve com **Criar rascunho** ou **Salvar rascunho**; quando **Pendências antes do envio** estiver vazio, escolha uma **Designação** e pressione **Emitir briefing**.
6. Como participante, leia a revisão emitida e pressione **Confirmar recebimento**. Se você discordar, escreva um **Motivo** de pelo menos 10 caracteres e pressione **Contestar**.
7. Se o briefing precisar mudar, escreva um motivo e use **Alterar**. As listas **Confirmações de recebimento** e **Revisões** mostram o histórico.

## O que significam os estados
- **Rascunho / Emitido:** o estado do briefing. Emitido significa que as confirmações de recebimento podem começar; por si só não é um resultado de prontidão.
- **Não emitido / Pendente / Concluído / Contestada:** o estado das confirmações de recebimento dos participantes. Só passa a **Concluído** quando todos os participantes confirmaram o recebimento da revisão emitida.
- **Aprovado e reconhecido / Atenção / Bloqueado / Revisão necessária:** o estado da avaliação de risco vinculada.
- **Aprovado / Reprovado / Desconhecido:** o resultado da verificação enviado à verificação de prontidão. Um briefing ausente ou não emitido e confirmações de recebimento pendentes são um **Reprovado** que bloqueia READY.

## O que você não pode fazer / cuidado
- Nenhuma decisão READY é tomada antes que o risco e o briefing estejam completos e as confirmações de recebimento dos participantes estejam registradas (13.03 §3).
- Uma contestação coloca o estado das confirmações em **Contestada** e envia um Reprovado à verificação de prontidão, de modo que READY e GO ficam bloqueados. O motivo aparece em **Confirmações de recebimento**. Uma confirmação de recebimento não pode ser alterada para a mesma revisão; para resolver uma contestação, o briefing é alterado (nova revisão), emitido novamente e todos os participantes confirmam o recebimento de novo.
- Se o plano ou a avaliação de risco mudarem depois do briefing, o briefing deve ser revisado novamente; confirmações anteriores não valem para uma nova revisão.
- Ninguém confirma o recebimento em nome de outra pessoa; cada participante registra sua própria confirmação (13.03 §6).
- Nenhum comando é enviado a partir desta página offline. Uma confirmação de recebimento pode ser colocada na fila em **Operações offline do rebocador**.

## Se algo der errado
- **Mensagem "Required sections are missing.":** complete os códigos que faltam; verifique se o plano e a avaliação de risco estão aprovados.
- **Mensagem "The record changed":** use **Recarregar** para revisar o briefing atual.
- **Mensagem "The outcome is unknown":** **Tentar novamente** envia a mesma solicitação de novo.
- **Sem autorização:** verifique seu papel, o rebocador e o contexto de designação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
