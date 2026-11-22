---
key: certificate-command-center
title: Central de certificados
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.extract, certificate.verify.record, file.read, directory.write]
related: [renewal-workflow, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Para que serve esta página
A Central de certificados lista as **declarações** de certificados da embarcação selecionada com seus estados de calendário declarados. Ela responde: "Quais certificados desta embarcação estão perto do vencimento ou de uma janela de vistoria, e quais registros ainda aguardam revisão humana?" As datas mostradas são declarações. A página nunca decide que um certificado é válido ou verificado (13.03 §1, §8).

## Quem usa
- **Comandante, Chefe de Máquinas / Superintendente Técnico:** acompanham o calendário de certificados da embarcação, inserem novos rascunhos de certificados e iniciam a preparação de renovações.
- **Usuário de Regulatório / Garantia:** verifica se as declarações se baseiam em uma fonte oficial e versionada e registra a declaração de um Auditor designado (13.03 §8).
- Usuários apenas com acesso de leitura veem somente a lista e não podem alterá-la.

## Antes de começar
- A visualização exige leitura de certificados. Inserir rascunhos exige escrita de certificados e escopo de edição naquela embarcação; extrair sugestões de documentos exige leitura e extração de arquivos; registrar uma declaração humana exige escrita no diretório, a permissão de registro de verificação e uma designação vigente de **Auditor** para esta embarcação e este certificado. Declarações protegidas da tripulação também exigem leitura da tripulação, leitura de certificados da tripulação, leitura da identidade da tripulação e leitura de arquivos.
- Escolha primeiro a embarcação correta: pressione-a na lista **Selecione uma embarcação** (13.03 §1).
- A página exige uma leitura online ativa. Offline, você vê "Current certificate sources unavailable offline or hidden." e nenhuma concessão offline é inferida.

## Passo a passo
1. Abra a embarcação na lista **Selecione uma embarcação**.
2. Use **Escopo dos certificados** para escolher **Certificados do navio** ou, se tiver a permissão, **Declarações protegidas da tripulação**. Restrinja a lista com **Filtro de calendário declarado**.
3. Abra um registro pressionando sua linha. As linhas têm o rótulo **Declaração de certificado não verificada**.
4. Para um novo registro, pressione **Novo rascunho de certificado**. Preencha **Nome do documento**, **Base regulatória declarada**, **Bandeira declarada**, a janela de vistoria e **Disponibilidade declarada** a partir do documento de origem e pressione **Salvar rascunho não verificado**.
5. Para obter sugestões do documento vinculado, pressione **Extrair do documento vinculado** em **Revisar dados do documento**. Compare cada sugestão com a fonte, marque **Conferi este dado com a fonte** e **Conferi que este documento pertence a este navio e certificado** e pressione **Confirmar dados selecionados e salvar rascunho**.
6. Como Auditor designado, em **Registro de declaração humana** preencha **Selecionar designação real** e **Selecionar afirmação registrada**, informe o motivo e pressione **Registrar declaração não avaliada**.
7. Para iniciar uma renovação, use **Preparar renovação** em **Trabalho relacionado a certificados** no registro. Isso leva você ao fluxo [Renovação de certificados](/app/renewal-workflow).

## O que significam os estados
- **Vencimento declarado <90 dias / <60 dias / <30 dias / Vencimento declarado ultrapassado:** uma observação de calendário a partir da data de vencimento declarada. Não é uma decisão sobre validade.
- **Janela de vistoria declarada aberta / Além da janela de vencimento:** posição em relação à janela de vistoria declarada.
- **Revisão de calendário necessária:** uma data está ausente, ambígua ou conflitante. O registro não está pronto até que uma pessoa o confira com o documento de origem.
- **Desconhecido:** o valor não foi declarado. Um valor desconhecido nunca significa "não ausente" nem "em conformidade"; ele precisa de revisão.
- **Confiança baixa — revisar / Ambíguo · revisar / Revisão necessária:** uma sugestão de documento ainda não confirmada por uma pessoa. Uma pontuação de confiança não é verificação.
- "This page only. Calendar observations are separate from qualification; these are not fleet-wide readiness totals."

## O que você não pode fazer / cuidado
- Salvar um rascunho não verifica o certificado. Escrever o registro, confirmar as sugestões do documento e a declaração do Auditor são etapas separadas; Concluído ≠ Verificado.
- A extração automática apenas sugere. Valores ausentes continuam desconhecidos e somente os dados que você seleciona e corrige atualizam o rascunho.
- Um papel de administrador sozinho não permite registrar uma declaração humana; são necessários uma designação vigente e o arquivo de origem.
- Um certificado salvo nunca é alterado silenciosamente. Cada salvamento cria uma nova revisão, e as revisões anteriores permanecem em **Histórico de revisões registradas** (13.03 §1).
- A página não concede aprovação de classe, de bandeira nem de autoridade e não decide sobre a aptidão para a viagem. Essa decisão cabe às pessoas autorizadas.

## Se algo der errado
- **Fonte de certificados ou acesso indisponível. Não se infere inventário válido, vazio nem verificado.** Pressione **Repetir leitura da fonte**. Se persistir, verifique seu papel e o escopo de embarcação e de objeto; contate o Tenant Admin, se necessário (13.03 §10).
- **Resultado desconhecido.** Mantenha o comando original e reenvie a mesma solicitação com **Repetir comando exato do certificado**.
- **A fonte atual mudou.** Seus campos locais são mantidos; use **Voltar aos registros**, leia novamente o registro atual e refaça sua alteração.
- **A leitura automática não está disponível para este documento:** insira os valores manualmente a partir do documento de origem.
- Se um registro não puder ser verificado, confira suas evidências e a linhagem da fonte e encaminhe-o ao verificador (13.03 §10).

## Páginas relacionadas
