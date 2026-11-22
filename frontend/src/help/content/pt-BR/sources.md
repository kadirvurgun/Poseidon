---
key: sources
title: Rascunhos de fontes
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Rascunhos de fontes mantém as fontes regulatórias e normativas em que sua organização se baseia (convenção, bandeira, classe, regra da empresa ou local) como **registros de rascunho versionados**. Ela responde: "Em qual revisão de qual fonte oficial este requisito se baseia?" Vale o aviso no topo: "Tenant-local DRAFT · Declared and unverified. Dates and hashes confer no legal authority. Source references are stored as text." (13.03 §8).

## Quem usa
- **Usuário de Regulatório / Garantia:** registra a fonte oficial e versionada e declara sua data de vigência e seu ciclo de vida (13.03 §8).
- **DPA / HSEQ:** consulta a página para ver a qual revisão de fonte uma auditoria ou constatação se refere (13.03 §4).

## Antes de começar
- A visualização exige leitura em todo o tenant e leitura de rascunhos regulatórios. Criar e salvar rascunhos também exige escrita de rascunhos regulatórios; sem ela, o botão **Novo rascunho** não aparece.
- As subvisões exigem suas próprias permissões, sempre com escopo de leitura em todo o tenant. **Ciclo de vida e aplicabilidade** exige leitura de aplicabilidade; editar regras exige escrita de regras, solicitar revisão exige revisão de regras e avaliar exige avaliação de aplicabilidade; qualificação e atestação de fatos exigem qualificação regulatória ou atestação de fatos, além de uma designação registrada no servidor. **Controles e evidências** exige leitura de controles, leitura de evidências, leitura de arquivos e leitura de aplicabilidade; vincular exige escrita de controles ou escrita de evidências com direitos de edição na embarcação, e a aprovação exige aprovação de controles.
- Tenha à mão o texto oficial da fonte e os detalhes de versão/emenda. O POSEIDON não baixa nem verifica o texto da fonte; quem o declara é você.
- Salvar exige uma conexão ativa com o servidor. Offline, suas edições são mantidas na aba, mas não são salvas.

## Passo a passo
1. Veja os rascunhos de fontes registrados na lista. Para pesquisar, digite em **Search drafts** e pressione **Pesquisar**. Navegue entre as páginas com **Anterior** e **Próximo**.
2. Para uma nova fonte, pressione **Novo rascunho**.
3. Em **Draft fields**, escolha primeiro um **Estado** para cada campo: **Ausente**, **Desconhecido** ou **Known (declared)**. Você só pode inserir um valor depois de escolher **Known (declared)**. Os campos são **Título**, **Autoridade declarada**, **Referência da fonte (apenas texto)**, **Declaração de versão / emenda**, **Data de vigência declarada**, **Declaração / proveniência** e **Fonte observada em (com deslocamento UTC)**.
4. Pressione **Salvar rascunho**. Em caso de sucesso, você vê "Draft saved. Unverified."; abra o registro com **Open saved draft and history**.
5. Abra um rascunho existente da lista, altere-o e salve novamente. Cada salvamento cria uma nova **Revisão**; as revisões anteriores permanecem em **Histórico imutável** com seu conteúdo e o **Hash de integridade (não autoridade)**.
6. Para ir às regras de aplicabilidade ou à cadeia controle-evidência, use **Ciclo de vida e aplicabilidade** ou **Controles e evidências** acima da lista. Pressione **Back to register** para voltar.

## O que significam os estados
- **RASCUNHO:** o estado armazenado exibido na lista e no cabeçalho do registro. Todo registro de fonte nesta página é um rascunho; não é uma adoção oficial nem uma decisão de entrada em vigor.
- **Ausente:** nenhuma informação foi inserida no campo ainda. As etapas posteriores (por exemplo, uma avaliação de aplicabilidade) ficam bloqueadas enquanto faltarem dados obrigatórios.
- **Desconhecido:** a informação foi procurada, mas não é conhecida. O sistema nunca lê Desconhecido como "não" ou "não aplicável"; quando dados críticos são desconhecidos, o resultado permanece em revisão (REVIEW_REQUIRED).
- **Known (declared):** você declarou o valor. Uma declaração não é uma informação verificada.
- **Hash de integridade (não autoridade):** mostra que o conteúdo de uma revisão não mudou; não é prova de validade legal.

## O que você não pode fazer / cuidado
- Você não pode alterar uma revisão salva. Quando a fonte mudar, salve uma nova revisão; os requisitos permanecem vinculados à revisão anterior e o histórico é mantido (13.03 §1).
- Não presuma que fontes de regras de classe, bandeira, empresa e locais substituem umas às outras; registre cada uma como uma fonte própria (13.03 §8).
- Esta página não admite oficialmente uma fonte e não fornece a ninguém um resultado de conformidade, permissão ou prontidão. A admissão de fontes e a revisão independente são autoridade humana.
- Datas de vigência e hashes não conferem autoridade legal.

## Se algo der errado
- **Permission denied.** Verifique sua função e o escopo do tenant; se necessário, consulte o Tenant Admin (13.03 §10).
- **Conflict. Your edits are retained. Reload only after discarding them.** Alguém alterou o registro antes de você. Abra a revisão atual com **Recarregar rascunho salvo** e faça sua alteração novamente.
- **Check field states, bounds, dates and source revision.** O valor de um campo ou o formato de uma data é inválido.
- **Service unavailable. Your draft is retained.** Quando a conexão voltar, reenvie o mesmo comando com **Retry frozen command**.
- **Sign in to continue; retain your draft.** Sua sessão terminou; use o link **Entrar**.
- Se um erro da aplicação persistir, informe ao suporte o correlation ID, a tela, o horário e os passos (13.03 §10).

## Páginas relacionadas
