---
key: work-order-center
title: Ordens de serviço
roles: [Chief Engineer, Technical Superintendent, Crew / OOW / Engineer]
permissions: [workorder.read, equipment.read, maintenance.read, workorder.write, workorder.release, workorder.execute, workorder.verify, workorder.close]
related: [pms-command-center, defects, critical-spares, service-engineer-report, operational-downtime, offline-conflict-center]
screenshots: [1]
reviewed: false
---
## Para que serve esta página
A central de ordens de serviço contém as **ordens de serviço em rascunho e os registros de execução declarados** do navio selecionado. Ela responde: "Quais ordens de serviço existem neste navio, e quais foram liberadas, executadas, tiveram evidências enviadas e foram verificadas por uma pessoa?" Cada revisão salva é mantida em um histórico imutável. A verificação e o encerramento não são retorno ao serviço, permissão nem aprovação de risco.

## Quem usa
- **Chefe de máquinas / Superintendente técnico:** para abrir, liberar, verificar e encerrar ordens de serviço planejadas e não planejadas (13.03 §5).
- **Engenheiro / tripulação:** para iniciar o trabalho atribuído e registrar e enviar leituras, sobressalentes e evidências em foto e documento (13.03 §6).

## Antes de começar
- A leitura exige `workorder.read`, `equipment.read` e `maintenance.read`. Rascunhos exigem `workorder.write`, a liberação `workorder.release`, a execução `workorder.execute`, a verificação `workorder.verify` e o encerramento `workorder.close`.
- Liberação, verificação e encerramento exigem um usuário humano autenticado.
- Escolha o navio correto em **Selecione um navio ou rebocador**.

## Passo a passo
1. Escolha o navio. Em **Ordens de serviço registradas**, filtre esta página com **Buscar números de ordens de serviço retornados** e abra um registro.
2. Para um novo trabalho, pressione **Novo rascunho de ordem de serviço**, preencha os campos e pressione **Salvar rascunho não avaliado**. Rascunhos incompletos podem ser salvos.
3. Em **Execução e evidências**, se você tiver permissão de liberação, escolha **Declarar evidências exigidas** em **Política de evidências exigidas**, marque os tipos de evidência e pressione **Liberar ordem de serviço**. Se você liberar sem declarar, a verificação permanece bloqueada até que alguém com permissão de liberação use **Alterar evidências exigidas** → **Salvar evidências exigidas**.
4. O executor pressiona **Iniciar trabalho**, preenche **Leituras**, **Sobressalentes consumidos**, **Evidências em foto e documento**, **Trabalho realizado** e **Constatações**, pressiona **Salvar evidências** e depois **Enviar evidências**.
5. Um verificador autorizado que não executou o trabalho revisa as evidências. Ele pressiona **Registrar verificação**, ou escolhe um **Motivo da devolução** e pressiona **Devolver ao executor**.
6. Para uma ordem de serviço verificada, uma pessoa autorizada pressiona **Registrar encerramento**.
7. Veja as revisões anteriores em **Histórico imutável de ordens de serviço**. Se for necessário um registro de indisponibilidade, use **Abrir rascunho de indisponibilidade vinculado**.

## O que significam os estados
- **Não liberada para execução. / Liberada / Em andamento:** o trabalho ainda não foi liberado, foi liberado ou está sendo executado.
- **Evidências enviadas:** o executor declarou o trabalho e as evidências. Isso não significa verificado.
- **Verificada (registro humano):** registrado por um verificador autorizado que não executou o trabalho. **Encerrada (registro humano)** é uma etapa separada.
- **Evidências exigidas ausentes / Presente, aguardando verificação humana / Verificada por uma pessoa:** o estado da barreira de evidências exigidas.
- **Revisão necessária: política não configurada:** nenhuma evidência exigida foi declarada. Não configurado nunca significa que nada é exigido; a verificação permanece bloqueada.
- **Devolvida pelo verificador:** o trabalho voltou ao executor com o motivo mostrado.

## O que você não pode fazer / cuidado
- Se você executou o trabalho, não pode verificá-lo nem encerrá-lo. A página diz "You executed this work, so you cannot verify or close it."
- Evidências enviadas, Verificada e Encerrada são registros humanos separados. A página nunca declara um equipamento pronto para o serviço.
- O resumo "Saved work order source" descreve os campos do rascunho salvo e afirma: "Release, evidence recording, independent verification and closure are recorded in the execution panel. Submitted evidence is accepted only by independent human verification, never by the person who did the work; there is no separate evidence-acceptance step, and verification is not a return-to-service decision." Essas ações são executadas em **Execução e evidências**.
- As leituras são mantidas como declaradas; nenhuma conversão de unidade ou limiar é aplicada.
- A fila de evidências offline só funciona quando existe uma política offline aprovada e enquanto o trabalho está em andamento. Conflitos nunca são mesclados automaticamente; use **Abrir central de conflitos** e uma pessoa decide.

## Se algo der errado
- **O registro mudou ou esta ação não é permitida no estado atual:** use **Recarregar** para ler o registro atual e tente novamente.
- **Resultado desconhecido:** **Repetir o mesmo comando** envia o mesmo comando; não cria um novo registro.
- **Falta permissão ou autoridade humana:** verifique seu papel e o contexto do navio; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **A barreira de evidências exigidas rejeitou esta ação:** complete os tipos de evidência que faltam. Se um registro não puder ser verificado, confira as evidências e sua proveniência com o verificador (13.03 §10).

## Páginas relacionadas
