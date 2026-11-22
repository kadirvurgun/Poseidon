---
key: risk-assessment
title: Avaliação de riscos
roles: [Master, Tug Master, DPA / HSEQ, Crew / OOW / Engineer]
permissions: [hse.risk.read, hse.risk.write, hse.risk.approve, hse.risk.acknowledge]
related: [risk-template-library, high-risk-work, safety-events]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Avaliação de riscos registra os perigos, as consequências potenciais, as medidas de controle, o risco inicial e residual e as condições de parada de trabalho de um trabalho ou operação em uma embarcação. Ela responde: "Os perigos e os controles deste trabalho foram considerados, revisados e confirmados pelos participantes?" A página é **apenas uma meta de desenvolvimento**: uma aprovação aqui é uma revisão humana registrada, não uma permissão de trabalho, uma aceitação de risco por especialista nem a aprovação de um portão de HSE (13.03 §2, §4).

## Quem usa
- **Comandante e Mestre do rebocador:** mandam preparar a avaliação para o trabalho no navio ou no rebocador e tomam a decisão de parada de trabalho. A autoridade de decisão permanece com eles (13.03 §2, §3).
- **DPA / HSEQ:** supervisionam a governança de riscos e registram a decisão de revisão (13.03 §4).
- **Crew / OOW / Engenheiro:** como participantes, confirmam ou contestam por si mesmos a avaliação aprovada (13.03 §6).

## Antes de começar
- Escolha uma embarcação para abrir suas avaliações de riscos. A leitura exige a permissão de leitura de riscos; escrever um rascunho exige a permissão de escrita de riscos e escopo de edição naquela embarcação. A decisão de revisão exige a permissão de aprovação de riscos; a confirmação exige a permissão de confirmação de riscos.
- As classificações exigem um método de risco (matriz) publicado no tenant. Sem ele, as classificações permanecem **Não avaliado**. Um responsável de HSEQ do tenant configura a matriz e outra pessoa a publica (página Biblioteca de modelos de risco).
- Os comandos de risco exigem uma conexão online vigente; nada é enfileirado ou aprovado offline.

## Passo a passo
1. Escolha a embarcação e abra uma avaliação da lista, ou pressione **Nova avaliação**.
2. Insira um **Número da avaliação**. Opcionalmente, insira um **Tipo de trabalho** e use **Começar de um modelo publicado (apenas cópia)** para escolher um modelo correspondente; o modelo é apenas copiado para o novo rascunho.
3. Preencha **Título**, **Contexto de operação / trabalho**, **Tipo de trabalho**, **Escopo do trabalho** e **Método de risco**. Em **Parada de trabalho necessária**, escolha **Sim**, **Não** ou **Desconhecido** e, se necessário, escreva as **Stop-work conditions**.
4. Com **Adicionar perigo**, insira para cada perigo a **Etapa do trabalho**, as **Potential consequences**, o **Risco inicial** e o **Risco residual** (probabilidade, gravidade, justificativa). Com **Adicionar controle**, adicione as **Medidas de controle**, o **Responsável pelo controle** e as **Barrier references**.
5. Em **Participantes**, escreva um ID de ator e uma função por linha. Se necessário, vincule registros relacionados com **Adicionar vínculo**.
6. Pressione **Salvar rascunho** e depois **Enviar para revisão**. **Itens bloqueantes** lista o que está faltando.
7. Um revisor escreve um **Motivo** de pelo menos 10 caracteres e pressiona **Aprovar (registro de revisão humana)** ou **Devolver**.
8. Após a aprovação, cada participante pressiona **Confirmar ciência** ou, com um motivo, **Contestar** por si mesmo. Se for necessária uma alteração, use **Emendar (abre novo rascunho)**.

## O que significam os estados
- **Rascunho / Em revisão / Devolvido / Aprovado (registro humano):** o ciclo de vida da avaliação. Aprovado é um registro de revisão humana, não uma permissão de trabalho.
- **Não avaliado / Método não configurado:** não há classificação. O sistema não usa nenhuma matriz padrão, multiplicação nem limite baixo/médio/alto.
- **Parada de trabalho necessária — Desconhecido:** ainda não foi tomada nenhuma decisão; isso nunca é lido como "Não".
- **Confirmação não aplicável antes da aprovação / Confirmações pendentes / Todos os participantes confirmaram / Contestado – não resolvido:** o estado de confirmação dos participantes. Uma contestação nunca é encerrada pela confirmação de outra pessoa.
- **Alterado desde a fixação / Indisponível:** um registro vinculado mudou depois da revisão fixada ou não pode mais ser lido; revise-o novamente.

## O que você não pode fazer / cuidado
- Um autor ou quem enviou o ciclo de revisão não pode aprovar a mesma avaliação (segregação de funções).
- Cada pessoa confirma apenas por si mesma e apenas a revisão exata aprovada.
- Uma aprovação aqui não é uma permissão de trabalho, uma aceitação de risco por especialista nem um portão de HSE. A decisão de iniciar ou parar o trabalho permanece com o Comandante ou o Mestre do rebocador.
- Um rascunho iniciado a partir de um modelo não traz nenhuma classificação, assinatura, confirmação de participante ou aprovação.
- Offline, uma avaliação não pode ser enviada, aprovada nem confirmada.

## Se algo der errado
- **Bloqueado até que os itens listados sejam concluídos.** Conclua os itens bloqueantes e envie novamente.
- **This record changed elsewhere.** Pressione **Recarregar** para ver a revisão atual e depois repita a ação.
- **Você não tem permissão para esta ação (segregação de funções ou escopo).** Peça a outra pessoa autorizada que faça a revisão, ou verifique sua função e o contexto da embarcação; se necessário, entre em contato com o administrador do tenant (13.03 §10).
- **A área de riscos não está disponível com seu acesso atual.** Verifique suas permissões.
- Se uma classificação parecer inesperada, verifique o método de risco e sua revisão; consulte o responsável pelo controle ou o responsável de HSEQ (13.03 §10).

## Páginas relacionadas
