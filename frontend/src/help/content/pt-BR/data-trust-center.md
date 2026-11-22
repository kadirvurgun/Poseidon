---
key: data-trust-center
title: Central de confiança de dados
roles: [Regulatory / Assurance, DPA, Technical Superintendent, Master]
permissions: [control.read, evidence.read, file.read, applicability.read, regulatory.draft.read]
related: [departure-signoff, fleet, industry-assurance, audit-ready, sources]
screenshots: []
reviewed: false
---
## Para que serve esta página
A central de confiança de dados mostra **sinais de qualidade de dados** para controles, evidências e campos do perfil da embarcação selecionada. Ela responde: "Quais registros desta embarcação estão ausentes, desatualizados, conflitantes, não verificados ou órfãos, e quais informações de fonte e atualidade indicam isso?" Um sinal de confiança não é a verificação do registro subjacente. A página nunca marca nada como pronto.

## Quem usa
- **Usuário de Regulatório / Garantia:** para não deixar registros órfãos ou incompletos na cadeia Requisito → Controle → Evidência → Verificação (13.03 §8).
- **DPA:** para acompanhar lacunas de evidência e de verificação (13.03 §4).
- **Superintendente Técnico / Comandante:** para ver os sinais que mantêm uma avaliação de prontidão em revisão necessária (13.03 §2, §5).

## Antes de começar
- Na barra de contexto superior, verifique se a **empresa correta** está selecionada (13.03 §1).
- A página exige, em conjunto, acesso de leitura a controles, evidências, arquivos, aplicabilidade e rascunhos regulatórios do tenant, com escopo de leitura em todo o tenant.
- Se nenhuma embarcação estiver selecionada, a página mostra primeiro a lista "Escolha uma embarcação".

## Passo a passo
1. Escolha a embarcação na lista "Escolha uma embarcação".
2. Em **Resumo**, leia o estado geral ("Revisão necessária" ou "Nenhum sinal relevante") e as contagens de **Objetos por sinal**.
3. Na tabela **Controles e evidências**, verifique as colunas "Fonte / procedência", "Atualidade", "Verificação" e "Sinais" de cada objeto.
4. Anote o código de motivo ao lado de um selo de sinal e revise o registro em sua própria página.
5. Veja os sinais dos campos do perfil na tabela **Campos do perfil do navio**.
6. Use **Anterior** / **Próximo** para outros objetos.
7. Se aparecer "These signals may be out of date.", pressione **Recarregar**.

## O que significam os estados
- **Ausente / Desatualizado / Conflitante / Não verificado / Órfão:** o tipo de sinal em um objeto. Um sinal é um marcador de qualidade; ele não prova que o registro está certo ou errado.
- **Sem sinal:** nenhum sinal foi encontrado neste objeto. Isso não significa que o registro está verificado.
- **Revisão necessária:** existe um sinal relevante. A prontidão permanece em revisão necessária enquanto existirem sinais relevantes.
- **Nenhum sinal relevante:** isto não é uma declaração de prontidão.
- **Verificado por uma pessoa:** uma pessoa autorizada verificou a evidência. Isso vem do próprio registro da evidência; a central não verifica nada.
- **Não verificado / Verificação expirada / Verificação rejeitada:** o estado de verificação da evidência.
- **Não se aplica a controles:** mostrado nas linhas de controles; o estado de verificação é para as linhas de evidências.
- **Horário de observação desconhecido / Sem política de atualidade / Datado no futuro:** não foi possível estabelecer a atualidade ou a data está no futuro. Nenhum desses significa "atual".

## O que você não pode fazer / cuidado
- Os registros não são corrigidos, verificados nem encerrados nesta página; isso acontece na própria página do registro, por uma pessoa autorizada.
- Se apenas os primeiros objetos foram avaliados, o resumo permanece em revisão necessária.
- Objetos que você não pode ler são contados, mas não mostrados, e o resumo então permanece em Revisão necessária. Um registro que você não pode ver não significa que não há problema.
- Offline, os sinais de confiança de dados não podem ser atualizados.

## Se algo der errado
- **Você não tem autorização para ler sinais de confiança de dados.:** verifique seu papel e o contexto da embarcação; contate o administrador do seu tenant, se necessário (13.03 §10).
- **O registro não pode ser verificado:** confira a evidência e sua procedência; leve a questão ao verificador autorizado.
- **Sinal inesperado:** leve o código de motivo ao responsável pelo controle ou a um especialista no assunto.
- **Os sinais de confiança de dados estão indisponíveis.:** use "Tentar novamente"; se persistir, informe ao suporte com uma captura de tela, o horário e seus passos.

## Páginas relacionadas
