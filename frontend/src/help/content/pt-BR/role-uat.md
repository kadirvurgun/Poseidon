---
key: role-uat
title: Registros de UAT por função
roles: [Product Owner, Tenant Admin]
permissions: [uat.read, uat.record, uat.threshold.record, uat.attest]
related: [pilot-gate, pilot-program]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Registros de UAT por função registra as sessões de teste de aceitação do usuário (UAT) com funções de navio e de rebocador, os resultados dos cenários e os achados como **evidência humana**, e os compara com o limite acordado. Ela responde: "A evidência de UAT atestada para cada função e cenário atende ao limite que as pessoas acordaram?" Vale o aviso no topo: "Records human UAT evidence only. The system never accepts UAT and never decides a pilot GO; automated test results are attachments, not a PASS."

## Quem usa
- **Facilitador de UAT:** abre uma sessão, registra os resultados de cenário observados do participante e os achados, e envia a sessão para atestação. As funções de participante de navio incluem Comandante, DPA, Chefe de máquinas e Tripulação (13.03 §2, §4, §5, §6).
- **Atestador:** uma pessoa diferente do facilitador atesta a sessão.
- **Responsáveis pelo UAT / Product Owner:** registram o limite acordado e usam o resultado na página **Revisão go/no-go do piloto**.

## Antes de começar
- A visualização exige leitura em todo o tenant e leitura de UAT. Registrar sessões e resultados, registrar o limite e atestar exigem, cada um, sua própria permissão.
- Os responsáveis pelo UAT devem ter acordado o limite previamente; o sistema nunca define um.
- Escolha um pseudônimo para o participante. Nunca use nome, e-mail ou número de tripulante.
- O registro exige uma conexão ativa; ele fica desativado offline.

## Passo a passo
1. Escolha **Navio** ou **Rebocador** com os botões **Modo de operação**.
2. Em **Evidências de UAT frente ao limite acordado**, revise o estado, as contagens de **Bloqueadores abertos**, **Achados maiores abertos**, **Sessões atestadas** e **Aguardando atestação** e a tabela de cenários.
3. Se não houver limite, preencha **Taxa mínima de aprovação por cenário**, **Máximo de achados maiores abertos**, **Conformidade com o design**, **Referência do acordo** e **Justificativa** em **Registrar limite acordado** e pressione **Registrar**.
4. Em **Sessões de UAT**, use o formulário **Nova sessão de UAT** para inserir **Função**, **Ambiente**, **Commit do build** e **Pseudônimo do participante** e pressione **Registrar**.
5. Abra a sessão. Com **Registrar resultado**, escolha o **Cenário** e o **Resultado**; adicione **Houve ajuda** e uma **Referência de evidência automatizada**, se for o caso. Para um problema, use **Registrar achado** com **Severidade** e **Resumo do achado**.
6. Depois de registrar os cenários, pressione **Enviar sessão para atestação** (somente o facilitador). Cenários sem resultado contam como **Não executado**. Uma pessoa diferente do facilitador escreve uma **Declaração de atestação** em **Atestar sessão** e pressiona **Registrar**.
7. Em **Achados abertos**, encerre um achado com uma **Resolução** e uma **Nota de resolução** e pressione **Resolver**.

## O que significam os estados
- **Limite não definido:** "No human-agreed threshold is recorded. UAT cannot pass until one is recorded."
- **Critérios não atendidos / Evidências incompletas:** a evidência atestada não atende ao limite, ou nem todas as funções e cenários estão cobertos.
- **Critérios atendidos; decisão humana pendente:** o melhor estado possível. Não é uma aceitação de UAT nem um GO do piloto; a decisão é humana.
- **Não executado:** não existe resultado para o cenário. Um cenário que não foi executado nunca conta como aprovado.
- **Aberta / Enviada / Atestada:** o estado da sessão. Somente sessões atestadas contam para o limite.
- **Aprovado / Reprovado / Bloqueado / Não executado:** o resultado de cenário observado do participante.
- **Bloqueador / Maior / Menor:** a severidade do achado. Um bloqueador aberto reprova o portão.
- **Dados fictícios:** um registro de exemplo sintético; não é evidência real de UAT.

## O que você não pode fazer / cuidado
- Você não pode atestar uma sessão que você facilitou: "Attestation must be recorded by a different human than the facilitator."
- Você não pode resolver um achado que você relatou. Um bloqueador só é encerrado como **Resolvido (corrigido e retestado)**; **Risco aceito** não é oferecido.
- Um resultado automatizado (Playwright) é apenas evidência anexada; o resultado é o observado pelo participante.
- O sistema nunca aceita o UAT, nunca define um limite e nunca fabrica atestações ou aprovações.

## Se algo der errado
- **Você não tem acesso a estes registros.** Verifique sua função e o escopo do tenant e entre em contato com o Tenant Admin (13.03 §10).
- **O registro não foi encontrado no seu tenant.** Verifique o link da sessão e o modo selecionado.
- **O registro mudou ou já foi usado. Recarregue.** Abra o estado atual com **Recarregar**.
- **O resultado é incerto. Tentar novamente envia o mesmo comando.** Use **Tentar novamente** para reenviar o mesmo comando com segurança.
- **Recusado pelas regras.** Por exemplo, o cenário não pertence à função da sessão, ou um bloqueador não pode ser encerrado como risco aceito.
- **Não permitido.** Por exemplo, você facilitou esta sessão e não pode atestá-la, ou você relatou este achado e não pode resolvê-lo.
- **Você está offline. O registro fica desativado até a conexão voltar.** Quando ela voltar, pressione **Atualizar**.

## Páginas relacionadas
