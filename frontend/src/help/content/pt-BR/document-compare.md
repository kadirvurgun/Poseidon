---
key: document-compare
title: Comparação de revisões de documento
roles: [Master, DPA, Regulatory / Assurance, Chief Engineer, Crew / OOW]
permissions: [file.read, document.acknowledge, document.restricted.download, document.isps.read]
related: [data-trust-center, audit, sources, today]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página mostra **duas revisões de um documento controlado lado a lado, em modo somente leitura**, para a embarcação selecionada. Ela responde: "O que difere entre estas duas revisões no nível de arquivo e de metadados, e qual é o histórico de publicação, verificação e custódia de cada uma?" A página mostra diferenças; ela não aprova, não publica nem julga alterações. Ela não coloca uma revisão em vigor e não faz com que ela substitua uma anterior. Os originais das evidências nunca são alterados silenciosamente; quando uma nova revisão substitui uma anterior, o estado de publicação da revisão anterior passa a Substituída e o evento aparece em seu histórico de custódia (13.03 §1).

## Quem usa
- **Comandante / Chefe de máquinas / Tripulação:** para comparar a revisão vigente com a anterior e registrar uma ciência de leitura (13.03 §2, §5, §6).
- **DPA / Usuário de regulatório e garantia:** para revisar o histórico de revisões e os registros de verificação (13.03 §4, §8).

## Antes de começar
- Se nenhuma embarcação estiver selecionada, a página informa **Escolha uma embarcação do seu escopo.** e lista as embarcações. Selecione a embarcação correta.
- Você precisa de acesso de leitura de arquivos para ver a página. Conteúdo restrito e documentos restritos ISPS também exigem permissão própria.
- Carregar conteúdo e registrar uma ciência funcionam apenas online.

## Passo a passo
1. Abra um documento na lista **Documentos controlados**. Os rótulos de classificação e **Restrito ISPS** são mostrados na linha.
2. Em **Escolha duas revisões**, selecione as revisões **Esquerda (base)** e **Direita (revisada)**. A página propõe primeiro a revisão mais recente à direita e a anterior a ela à esquerda.
3. Pressione **Comparar**. Use **Trocar lados** para invertê-las.
4. Leia a declaração sobre os bytes e a tabela **Comparação de metadados**: **Tipo de arquivo**, **Tamanho (bytes)**, **SHA-256 do conteúdo**, **Recebimento**, **Publicação**, **Verificação** e os campos de horário.
5. Revise o **Histórico de aprovação e custódia** de cada lado.
6. Para ver o conteúdo, pressione **Carregar conteúdo (acesso registrado)**. O conteúdo é mostrado com uma marca d'água de atribuição.
7. Depois de **Carregar conteúdo**, se você leu a revisão vigente, marque **Li esta revisão e pude acessar seu conteúdo.** e pressione **Registrar ciência**.

## O que significam os estados
- **Não publicada / Vigente / Substituída:** o estado de publicação da revisão. Uma revisão que não está vigente mostra **Sem vigência operacional**.
- **Não verificada / Verificada válida / Verificada inválida / Verificação inconclusiva:** o registro de verificação da revisão. Não verificada não significa "válida".
- **Igual / Alterado / Não comparável:** o resultado de uma linha de metadados. Não comparável não significa "Igual".
- **Arquivos idênticos byte a byte:** apenas bytes idênticos; isto não é uma revisão do conteúdo.
- **Os arquivos diferem no nível de bytes:** qual regra mudou, e sua importância, precisa da revisão de uma pessoa.
- **Conteúdo restrito para sua função; apenas metadados.** o conteúdo não é aberto para esta função.

## O que você não pode fazer / cuidado
- Você não pode publicar, aprovar, colocar em vigor nem substituir um documento nesta página.
- A página não interpreta automaticamente as diferenças de conteúdo; uma pessoa autorizada decide sua importância.
- Uma ciência é registrada apenas para a revisão vigente, uma vez por pessoa por revisão. Ela não é uma aprovação nem uma verificação.
- Uma cópia impressa não é controlada. O conteúdo não é armazenado em cache nem mantido offline.
- Offline, a comparação fica desatualizada e não é possível carregar conteúdo novo.

## Se algo der errado
- **Este documento não está disponível para você.:** verifique o contexto da embarcação e sua permissão; contate o administrador do seu tenant, se necessário (13.03 §10).
- **Você não tem permissão para esta ação.:** verifique seu papel e o contexto da embarcação (13.03 §10).
- **The record changed meanwhile:** use **Recarregar** para abrir a versão mais recente.
- **Uma revisão não pode ser verificada:** confira a evidência e o histórico de procedência; fale com um verificador autorizado (13.03 §10).
- **A solicitação falhou.:** envie a tela, o horário e os passos ao suporte (13.03 §10).

## Páginas relacionadas
