---
key: tug-assignment
title: Atribuição de rebocador
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read, tug.write, tug.plan.write]
related: [tug-job-board, tug-readiness, tug-crew-readiness, tug-equipment-readiness, towage-plan]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página mostra as **propostas de atribuição de rebocadores** de um serviço de rebocador enviado e seus **conflitos** com outros serviços. Ela responde: "Qual rebocador foi proposto para este serviço, com qual função e janela de tempo, ele conflita com outro trabalho e o Mestre do rebocador aceitou a proposta?" A terra propõe, o Mestre do rebocador confirma; a página não toma nenhuma decisão de despacho ou de liberação. O aviso no topo diz isso: "The system never issues GO."

## Quem usa
- **Coordenador de operações de rebocador / Operações em terra:** para fazer uma proposta de atribuição depois de revisar os conflitos e a prontidão no quadro de serviços (13.03 §7).
- **Mestre do rebocador:** para confirmar em contexto uma proposta para o seu próprio rebocador e aceitá-la ou rejeitá-la (13.03 §3).

## Antes de começar
- Abra o rebocador em **Operações de rebocador**; se nenhum rebocador estiver selecionado, a página indica esse caminho.
- Propor exige acesso de escrita de rebocador e de escrita do plano; o serviço deve estar **Enviado**.
- Responder exige uma **Designação** registrada no servidor (Mestre do rebocador ou Comandante) para esse rebocador. A pessoa que fez uma proposta não pode respondê-la.

## Passo a passo
1. Abra o número do serviço em **Selecione um serviço de reboque enviado**. O link **Atribuições e prontidão** no detalhe do serviço também abre esta página.
2. Nas propostas existentes, leia **Estado**, **Reconhecimento do comandante do rebocador**, **Função**, **Início planejado** e **Fim previsto**.
3. Em **Conflitos**, veja os conflitos de horário e de recursos com outros serviços, seu selo de resultado e o código de motivo.
4. Para uma nova proposta, escolha **Rebocador**, **Início planejado**, **Fim previsto** e **Função** (principal, apoio, escolta) no formulário **Propor atribuição** e pressione **Propor atribuição**.
5. Como Mestre do rebocador, escolha sua **Designação**, adicione um **Motivo** se necessário e pressione **Aceitar** ou **Rejeitar**. Para rejeitar, um motivo é obrigatório.
6. Para remover uma proposta, informe um motivo e pressione **Retirar proposta**.
7. Use os links **Verificação de prontidão operacional** e **Tripulação e competência de reboque** de cada proposta para ir às telas de prontidão.

## O que significam os estados
- **Proposto / Retirado:** o ciclo de vida da proposta. Proposto não significa que o rebocador foi despachado.
- **Pendente / Aceito / Rejeitado:** a resposta do Mestre do rebocador à proposta. Aceito confirma o escopo da proposta; não é uma decisão GO.
- **Aprovado / Reprovado / Desconhecido:** o resultado de uma verificação de conflito. Desconhecido mostra que não havia dados de origem suficientes para a verificação; nunca significa "sem conflito".
- **Nenhum conflito observado; os dados podem estar incompletos.:** nenhum conflito foi observado, mas isso não é um resultado completo de elegibilidade.
- **A solicitação mudou após esta proposta; é necessária uma nova proposta.:** a solicitação do serviço mudou depois da revisão em que a proposta se baseou.

## O que você não pode fazer / cuidado
- Uma proposta de atribuição feita em terra nunca se sobrepõe à decisão de segurança a bordo do Mestre do rebocador (13.03 §3, §7).
- Quem propõe não pode aceitar a própria proposta; a resposta cabe apenas à pessoa designada para esse rebocador.
- Nenhuma proposta pode ser feita para um serviço em rascunho ou cancelado.
- Os comandos ficam desativados offline.

## Se algo der errado
- **Mensagem "The outcome is uncertain":** **Tentar novamente** envia o mesmo comando; não crie uma nova proposta.
- **Mensagem "The record changed":** use **Recarregar** para revisar o registro atual e depois aja novamente.
- **"You are not authorized for this action." / "You do not have access to this record.":** verifique seu papel, o rebocador e o contexto de designação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **Conflito inesperado:** anote o código de motivo e revise-o com o responsável pelo outro serviço.

## Páginas relacionadas
