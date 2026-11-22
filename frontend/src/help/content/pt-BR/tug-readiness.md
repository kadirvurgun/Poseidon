---
key: tug-readiness
title: Verificação de prontidão do rebocador
roles: [Tug Master, Master, Tug Operations Coordinator, Shore Operations]
permissions: [mode.tug, tug.read, tug.write]
related: [tug-assignment, tug-crew-readiness, tug-equipment-readiness, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Para que serve esta página
A verificação de prontidão avalia a prontidão operacional de uma atribuição de rebocador a partir de verificações de fontes, **apenas como orientação**, e permite que o Mestre do rebocador ou o Comandante designado registre uma **decisão GO/NO-GO** fundamentada. Ela responde: "O que dizem as verificações de tripulação, equipamentos, PMS, certificados, comunicação, condições meteorológicas, margem de tração estática, atribuição e briefing, e quais bloqueios existem?" A avaliação não é uma decisão: "The system never issues GO; only the appointed Master or Tug Master records a GO/NO-GO decision with a reason."

## Quem usa
- **Mestre do rebocador / Comandante:** para revisar a avaliação e os bloqueios e registrar uma decisão GO ou NO-GO (13.03 §3).
- **Coordenador de operações de rebocador / Operações em terra:** para acompanhar a prontidão e os bloqueios; eles não decidem (13.03 §7).

## Antes de começar
- Abra a página a partir de uma atribuição: em **Atribuição e conflitos**, use o link **Verificação de prontidão operacional** de uma proposta. Sem uma atribuição selecionada, a página mostra "Select an assignment from the job's assignment list."
- Registrar uma decisão exige acesso de escrita de rebocador e uma **Designação** registrada no servidor (Mestre do rebocador ou Comandante) para esse rebocador, e a atribuição ainda deve estar como Proposto.
- Não há READY sem o briefing: o **Risco e briefing de reboque** deve estar emitido e com confirmação de recebimento de todos os participantes (13.03 §3).

## Passo a passo
1. Leia o selo de estado na linha **Avaliação**. A nota ao lado diz "Advisory, not a GO decision."
2. Verifique **Tração estática exigida (t)**, **Tração estática disponível verificada (t)** e **Margem de tração estática (t)**. Um valor ausente mostra "Desconhecido".
3. Revise os códigos em **Bloqueios críticos**, **Motivos de revisão** e **Condições**.
4. Na tabela de categorias, veja o resultado de cada categoria (Tripulação, Competência de reboque, Equipamentos, Equipamento de reboque, PMS e defeitos, Certificados, Comunicação, Condições meteorológicas, Margem de tração estática, Atribuição, Risco e briefing de reboque). Para detalhes, leia os códigos de motivo na tabela **Verificações de fontes**.
5. Se você tem autoridade de decisão, escolha **NO-GO** ou **GO** no formulário **Registrar decisão do comandante**, informe sua **Designação** e um **Motivo** e pressione **Registrar**.
6. Veja as decisões registradas na lista **Decisões do comandante**.

## O que significam os estados
- **Pronto (READY):** todas as verificações de fontes passam (sem bloqueio, desconhecido ou condição). Isso é informação, não uma decisão; a decisão GO continua sendo registrada pelo Mestre do rebocador ou pelo Comandante.
- **Condicional (CONDITIONAL):** as condições em "Condições" se aplicam. GO fica desativado e a página explica: "This assessment is CONDITIONAL. GO needs a READY assessment: no source defines which conditions would permit GO, so the server refuses it. Resolve the conditions and reassess, or record NO-GO."
- **Não pronto (NOT_READY):** existe pelo menos um bloqueio crítico.
- **Revisão necessária (REVIEW_REQUIRED):** algumas fontes são desconhecidas ou aguardam revisão. Uma verificação desconhecida nunca é contada como "Pronto".
- **Aprovado / Reprovado / Desconhecido:** o resultado de uma categoria ou verificação individual.
- **GO / NO-GO:** a decisão humana e o motivo registrados pelo Mestre do rebocador ou pelo Comandante.

## O que você não pode fazer / cuidado
- GO só pode ser registrado sobre uma avaliação READY; o servidor recusa GO em CONDITIONAL, NOT_READY ou REVIEW_REQUIRED. A página só habilita GO em uma avaliação READY; caso contrário, diz "GO can only be recorded against a READY assessment." NO-GO sempre pode ser registrado com um motivo.
- Não há READY enquanto o risco e o briefing estiverem incompletos ou faltarem confirmações de recebimento dos participantes; nesse caso, a opção GO fica desativada (13.03 §3).
- A proposta de atribuição de um usuário em terra nunca se sobrepõe à decisão de segurança a bordo do Mestre do rebocador (13.03 §7).
- Se a avaliação mudar antes da sua decisão, a decisão é recusada; revise novamente a avaliação atual.
- Os comandos ficam desativados offline.

## Se algo der errado
- **Mensagem "The assessment changed":** use **Recarregar**, revise a avaliação atual e decida novamente.
- **Mensagem "The outcome is uncertain":** **Tentar novamente** envia o mesmo comando.
- **Prontidão inesperada:** abra a tabela de categorias e os motivos das verificações de fontes; revise-os com o responsável pelo controle ou um especialista no assunto (13.03 §10).
- **Sem autorização:** verifique seu papel, o rebocador e o contexto de designação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
