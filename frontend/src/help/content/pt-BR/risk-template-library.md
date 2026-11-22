---
key: risk-template-library
title: Biblioteca de modelos de risco
roles: [DPA / HSEQ, Master, Tug Master, Crew / OOW / Engineer]
permissions: [hse.risk_template.read, hse.risk_template.write, hse.risk_template.publish, hse.risk_template.suggest, hse.risk_method.write, hse.risk_method.publish]
related: [risk-assessment, high-risk-work]
screenshots: []
reviewed: false
---
## Para que serve esta página
A Biblioteca de modelos de risco gerencia, com revisões, os modelos de avaliação de riscos usados em todo o tenant (tipo de trabalho, perigos, controles padrão, barreiras exigidas, fontes de SMS e de requisitos) e os métodos de risco (matriz). Ela responde: "Qual modelo publicado se aplica a este tipo de trabalho, e qual revisão está em vigor?" A página é **apenas uma meta de desenvolvimento**: uma publicação aqui é um registro humano de HSEQ, não uma permissão de trabalho, uma aceitação de risco por especialista nem a aprovação de um portão de HSE (13.03 §4).

## Quem usa
- **DPA / HSEQ:** como parte da governança de riscos, preparam, revisam e publicam revisões de modelos, barreiras e a matriz de risco (13.03 §4).
- **Comandante, Mestre do rebocador e tripulação:** leem os modelos publicados e enviam **Sugestões da tripulação** para um modelo.

## Antes de começar
- A leitura exige a permissão de leitura de modelos. Escrever e publicar modelos exige escopo em todo o tenant e a permissão de escrita ou de publicação de modelos. Enviar uma sugestão exige apenas a permissão de sugestão de modelos.
- Criar e publicar um método de risco são permissões separadas e exigem escopo em todo o tenant.
- Os comandos de risco exigem uma conexão online vigente; nada é enfileirado ou aprovado offline.

## Passo a passo
1. Veja os modelos na lista. Cada linha mostra o estado, a revisão **Vigente** e a contagem de **Suggestions**. Abra um modelo.
2. Para um novo modelo, pressione **Novo modelo**. Preencha **Código do modelo**, **Título**, **Tipo de trabalho**, **Escopo navio / rebocador**, **Responsável pela aprovação**, **Motivo da alteração**, **Required barriers**, **SMS / requirement sources** e **Perigos** e pressione **Salvar rascunho**.
3. Em uma revisão em rascunho, pressione **Enviar para revisão**. Os itens faltantes são listados como itens bloqueantes.
4. Um publicador insere **Vigente a partir de** e um **Motivo** de pelo menos 10 caracteres e pressiona **Publicar (registro humano HSEQ)** ou **Devolver**.
5. Para alterar um modelo publicado, escreva um motivo e pressione **Iniciar nova revisão**; a revisão anterior não muda.
6. A tripulação escreve uma sugestão em **Enviar sugestão** e a envia. Uma pessoa autorizada a analisa com **Aceitar** ou **Rejeitar** e um motivo.
7. Em **Métodos de risco (matriz)**, use **Novo método** para inserir **Código do método**, **Likelihood codes**, **Severity codes** e **Matrix cells**; outra pessoa o publica com **Publicar método**.

## O que significam os estados
- **Rascunho / Em revisão / Devolvido / Publicado / Substituído:** o ciclo de vida da revisão do modelo.
- **Vigente / Agendado / Não vigente:** a visão de vigência. A revisão publicada mais recente cuja data de vigência já chegou é **Vigente**; uma com data futura é **Agendado**. Uma revisão futura nunca substitui a anterior antes do tempo.
- **Desconhecido:** o campo não está preenchido; um valor vazio nunca é preenchido com um padrão.
- **No published risk method: ratings stay not assessed.** O sistema não oferece nenhuma matriz ou limite padrão.

## O que você não pode fazer / cuidado
- Um autor de uma revisão ou a pessoa que a enviou não pode publicar a mesma revisão; a publicação é um registro humano separado (segregação de funções).
- A publicação não é uma permissão de trabalho, uma aceitação de risco por especialista nem um catálogo de produção aprovado.
- Um modelo é aplicado a uma avaliação de riscos apenas como **cópia**; ele não traz nenhuma classificação, assinatura, confirmação de participante, aprovação ou ciência. Quando um modelo muda, as avaliações existentes não mudam por si mesmas.
- Uma revisão publicada nunca é editada; inicie uma nova revisão para alterá-la.

## Se algo der errado
- **Bloqueado até que os itens listados sejam concluídos.** Conclua os itens bloqueantes e envie novamente.
- **This record changed elsewhere.** Pressione **Recarregar** para ver a revisão atual.
- **Você não tem permissão para esta ação (segregação de funções ou escopo).** A publicação deve ser feita por outra pessoa autorizada que não seja autora da revisão; se faltar uma permissão, entre em contato com o administrador do tenant (13.03 §10).
- **Esta ação não se aplica mais ao estado atual. Recarregue.** O estado da revisão mudou.

## Páginas relacionadas
