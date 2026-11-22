---
key: pre-survey-checklist
title: Checklist pré-vistoria
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.survey.plan, certificate.survey.review]
related: [survey-planner, certificate-command-center, renewal-workflow]
screenshots: []
reviewed: false
---
## Para que serve esta página
O Checklist pré-vistoria acompanha as tarefas de preparação para a vistoria de um certificado, com responsável, prazo, evidência e verificação humana independente. Ele responde: "Quais itens de preparação desta vistoria estão abertos, quais têm evidência e quais foram verificados por outra pessoa?" A prontidão é consultiva e nunca é aprovação da vistoria (13.03 §1, §8).

## Quem usa
- **Comandante, Chefe de máquinas / Superintendente técnico:** iniciam um checklist, respondem aos itens e anexam evidências.
- **Verificador:** compara os itens respondidos por outra pessoa com suas evidências e os verifica ou rejeita (13.03 §1).
- Usuários com acesso apenas de leitura veem "Read-only for your role."

## Antes de começar
- A visualização exige leitura de certificados. Iniciar e responder a um checklist exige planejamento de vistoria naquela embarcação; verificar exige revisão de vistoria.
- Escolha a embarcação correta com **Escolha uma embarcação** (13.03 §1).
- A janela de vistoria do certificado deve estar declarada na [Central de comando de certificados](/app/certificate-command-center).
- A página exige uma leitura online. Offline, você vê "Offline: a current online read is required."

## Passo a passo
1. Veja as listas da embarcação em **Checklists**, ou no [Planejador de vistorias](/app/survey-planner) pressione **Abrir checklist** ou **Iniciar checklist** em uma linha.
2. Para uma nova lista, use **Escolha um certificado**, escolha um **Modelo** e pressione **Criar checklist**. O modelo é um modelo inicial, não um catálogo aprovado.
3. Em **Dados do plano de vistoria**, preencha **Início planejado**, **Término planejado**, **Local previsto (texto)** e, se disponível, **Escala**.
4. Em **Itens**, insira **Função responsável**, **Prazo**, **Resposta** e, se necessário, **Constatação** e **Comentário** para cada item. Vincule evidências com **Adicionar evidência** e pressione **Salvar**. Cada salvamento cria uma nova revisão.
5. Como verificador, nos itens respondidos por outra pessoa, escolha **Verificar** ou **Rejeitar** em **Decisão**, insira o motivo e pressione **Registrar verificação**.
6. Acompanhe **Prontidão** e **Bloqueios**.

## O que significam os estados
- Resposta do item: **Aberto**, **Pronto**, **Requer atenção**, **Bloqueado**, **Não aplicável**. Um item cuja resposta é desconhecida permanece **Aberto**; ele nunca conta como "Não aplicável" ou "Pronto".
- Verificação: **Pendente**, **Verificado (humano)**, **Rejeitado**. Um item respondido permanece **Pendente** até ser verificado.
- Prontidão: **Bloqueado**, **Incompleto**, **Preparado, aguardando confirmação humana**. Mesmo o melhor estado aguarda confirmação humana; o sistema nunca diz "vistoria aprovada".
- **Crítico / Obrigatório / Opcional:** a importância do item. Itens críticos e obrigatórios não resolvidos contam contra a prontidão e aparecem em **Bloqueios**.
- **Viagem: desconhecida (sem módulo de viagem):** a adequação à viagem ainda não pode ser avaliada e permanece desconhecida.

## O que você não pode fazer / cuidado
- Você não pode verificar um item que você respondeu: "You answered this item; another person must verify it."
- Responder a um item como **Pronto** não o verifica; Concluído ≠ Verificado. Cada decisão de verificação fica vinculada à resposta e à evidência exatas que você vê.
- A prontidão não é aprovação da vistoria, agendamento nem validade do certificado. A coordenação com classe, bandeira e autoridades é externa e não é simulada aqui.
- Os itens do modelo não são um catálogo aprovado; uma pessoa autorizada avalia se eles se aplicam à embarcação.

## Se algo der errado
- **Este checklist mudou desde que você o abriu. Recarregue antes de editar.** Pressione **Recarregar a versão atual**.
- **O resultado ainda não é conhecido. Repetir envia a mesma solicitação.** Use **Repetir a mesma solicitação**.
- **Itens não registrados:** corrija os itens listados e salve novamente.
- **Você não tem acesso a esta visualização.** Verifique sua função, a embarcação e o escopo do objeto; se necessário, entre em contato com o Tenant Admin (13.03 §10).
- Se um item não puder ser verificado, confira sua evidência e sua fonte e encaminhe-o ao verificador (13.03 §10).

## Páginas relacionadas
