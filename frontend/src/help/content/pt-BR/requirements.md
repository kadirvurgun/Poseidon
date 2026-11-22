---
key: requirements
title: Rascunhos de requisitos
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.draft.read, regulatory.draft.write]
related: [sources, audit-ready, audit]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Rascunhos de requisitos mantém os **requisitos** derivados de uma revisão de fonte como rascunhos versionados e vincula cada requisito a uma revisão exata da fonte. Ela responde: "Em qual revisão de qual fonte este requisito se baseia, e qual é o seu ciclo de vida registrado?" Um requisito é o primeiro elo da cadeia Requisito → Controle → Evidência → Verificação. Nenhum registro nessa cadeia deve ficar sem vínculo (órfão) (13.03 §8).

## Quem usa
- **Usuário de Regulatório / Garantia:** vincula o requisito à sua fonte, avalia a aplicabilidade com as entradas de contexto e mantém a cadeia completa (13.03 §8).
- **DPA / HSEQ:** lê a base de um requisito durante o trabalho de constatações e auditorias (13.03 §4).

## Antes de começar
- A visualização exige leitura em todo o tenant e leitura de rascunhos regulatórios; editar também exige escrita de rascunhos regulatórios.
- As subvisões exigem suas próprias permissões, sempre com escopo de leitura em todo o tenant. **Ciclo de vida e aplicabilidade** exige leitura de aplicabilidade; editar regras exige escrita de regras, solicitar revisão exige revisão de regras e avaliar exige avaliação de aplicabilidade; qualificação e atestação de fatos exigem qualificação regulatória ou atestação de fatos, além de uma designação registrada no servidor. **Controles e evidências** exige leitura de controles, leitura de evidências, leitura de arquivos e leitura de aplicabilidade; vincular exige escrita de controles ou escrita de evidências com direitos de edição na embarcação, e a aprovação exige aprovação de controles.
- Registre primeiro a fonte na página **Rascunhos de fontes**. Um requisito só pode ser vinculado a uma revisão de fonte existente.
- Salvar exige uma conexão ativa com o servidor; offline, as edições são mantidas na aba.

## Passo a passo
1. Veja os rascunhos de requisitos na lista; pesquise com **Search drafts** e **Pesquisar**.
2. Para um novo requisito, pressione **Novo rascunho**.
3. Em **Draft fields**, escolha um **Estado** para cada campo (**Ausente**, **Desconhecido**, **Known (declared)**) e insira os valores conhecidos: **Instrumento**, **Declaração de versão / emenda**, **Data de vigência declarada**, **Declaração / proveniência**, **Fonte observada em (com deslocamento UTC)**.
4. Em **Immutable source citation**, digite em **Find a source** e pressione **Search sources**. Escolha a fonte em **Source draft** e, em seguida, a revisão exata em **Exact revision**. Confira o conteúdo da revisão escolhida em **Selected immutable source content**.
5. Pressione **Salvar rascunho**. Quando o registro estiver aberto, as seções **Ciclo de vida registrado** e **Histórico imutável** aparecem.
6. Use **Ciclo de vida e aplicabilidade** para a aplicabilidade e **Controles e evidências** para os vínculos de controle e evidência. O **Relatório de elementos críticos sem vínculo** dentro de **Controles e evidências** lista declarações críticas sem vínculo com requisitos; vincule esses registros a um requisito.

## O que significam os estados
- **Ciclo de vida registrado:** o estado que o servidor deriva das regras vinculadas: **Rascunho**, **Adotado**, **Vigência futura**, **Em vigor**, **Substituído**. Se não puder ser lido, aparece **Indisponível**; o sistema não presume rascunho nem em vigor.
- "Bound rules record different lifecycle states. The shown value is a software precedence; expert review is required." Nesse caso, o valor exibido não é uma decisão; é necessária a revisão de um especialista.
- **Desconhecido:** a informação não é conhecida. Quando dados críticos são desconhecidos, o resultado de aplicabilidade permanece **Revisão necessária** (REVIEW_REQUIRED); ele nunca é contado como "não aplicável" ou "não" (13.03 §8).
- **Revisão de aplicabilidade necessária / Vínculo de requisito ausente / Verificação não realizada:** itens em aberto na cadeia controle-evidência.
- **Conclusão desconhecida — Não verificado — Eficácia desconhecida:** conclusão, verificação e eficácia são estados separados. Um registro concluído não está verificado, e um registro verificado não está eficaz.

## O que você não pode fazer / cuidado
- Você não pode vincular um requisito à "versão mais recente" de uma fonte; sempre se escolhe uma **Exact revision**. Quando a fonte recebe uma nova revisão, o vínculo antigo é mantido no histórico.
- Uma prévia de aplicabilidade não estabelece conformidade, prontidão, permissão nem aprovação legal: "PREVIEW / DRAFT. Predictions do not establish compliance, readiness, permits or legal approval."
- Uma mudança de ciclo de vida passa por **Solicitar revisão humana**; quem decide são a admissão da fonte e um revisor independente qualificado. O sistema nunca a encerra por conta própria.
- Não presuma que fontes de regras de classe, bandeira, empresa e locais substituem umas às outras (13.03 §8).

## Se algo der errado
- **Source selection unavailable.** Pressione **Retry sources**.
- **Conflict. Your edits are retained. Reload only after discarding them.** Abra a revisão atual com **Recarregar rascunho salvo**.
- **Check field states, bounds, dates and source revision.** Se você escolheu uma fonte, escolha também sua revisão exata.
- **History unavailable. Editor retained.** Pressione **Retry history**.
- Se o ciclo de vida for inesperado, revise as regras vinculadas e as revisões das fontes; se necessário, entre em contato com o responsável pelo controle ou com o especialista no assunto (SME) (13.03 §10).
- Sem permissão, verifique sua função e o escopo do tenant e consulte o Tenant Admin (13.03 §10).

## Páginas relacionadas
