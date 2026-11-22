---
key: drydock-planning
title: Planejamento de docagem
roles: [Technical Superintendent, Chief Engineer]
permissions: [drydock.read, drydock.write, drydock.verify]
related: [work-order-center, defects, certificate-command-center, survey-planner, pre-survey-checklist]
screenshots: []
reviewed: false
---
## Para que serve esta página
O planejamento de docagem contém os **pacotes de docagem** da embarcação selecionada (projeto, estaleiro, datas planejadas e itens do escopo). Ele responde: "A qual ordem de serviço, defeito, certificado ou lista de verificação de vistoria cada item do escopo da docagem está vinculado, e esses vínculos estão atualizados?" O escopo é vinculado a esses registros (13.03 §5). **Completude dos vínculos** é apenas a completude dos vínculos; não é um resultado de vistoria de classe ou de bandeira nem uma declaração de navegabilidade.

## Quem usa
- **Superintendente Técnico:** para preparar um pacote de docagem, vincular o escopo aos registros, planejá-lo e concluí-lo (13.03 §5).
- **Chefe de máquinas:** para acompanhar como o escopo se vincula aos registros de defeitos e ordens de serviço e, com permissão, verificar um pacote concluído por outra pessoa (13.03 §5).

## Antes de começar
- Selecione a embarcação correta. Se nenhuma embarcação estiver selecionada, a página informa **Selecione uma embarcação no espaço de trabalho para continuar.**
- Você precisa de acesso de leitura de docagem para ver os pacotes. Criar, editar e alterar o status exigem acesso de escrita; verificar exige acesso de verificação.
- Tenha em mãos os identificadores e as revisões dos registros de ordens de serviço, defeitos, certificados e listas de verificação de vistoria que você vai vincular.

## Passo a passo
1. Abra um pacote na lista ou crie um rascunho vazio com **Criar pacote de docagem**.
2. Preencha **Projeto de docagem**, **Estaleiro**, **Início planejado** e **Término planejado**.
3. Adicione itens com **Adicionar item do escopo**; informe o tipo de **Item do escopo**, **Título**, **Pacote de trabalho**, **Requisito de classe**, **Requisito estatutário** e **Prioridade**.
4. Em cada item, use **Adicionar vínculo** e informe o **Tipo de registro**, o **Identificador do registro** e a **Revisão fixada**.
5. Para a conclusão, adicione uma referência de evidência em **Evidência de conclusão** com **Adicionar**. Em seguida, pressione **Salvar**.
6. Confira a lista **Impedimentos de planejamento** ou **Impedimentos para conclusão**. Em **Alterar status**, escolha uma **Ação** (**Planejar**, **Voltar para rascunho**, **Iniciar docagem**, **Concluir docagem**, **Cancelar pacote**) e confirme-a. Cancelar exige uma observação. Somente as ações válidas para o estado atual são oferecidas; um pacote só pode ser cancelado enquanto estiver em Rascunho ou Planejado.
7. Em um pacote concluído que você não concluiu, escreva uma **Justificativa** de no mínimo 10 caracteres e use **Verificar registro** ou **Rejeitar verificação**.
8. Acompanhe as datas planejadas e reais e cada revisão na **Linha do tempo**.

## O que significam os estados
- **Rascunho / Planejado / Em andamento / Concluído / Verificado / Cancelado:** o ciclo de vida do pacote. Concluído e Verificado não são a mesma coisa.
- **Vínculos atualizados / Atenção / Revisão necessária / Incompleto:** a completude dos vínculos. Mesmo com os vínculos atualizados, isto não é um resultado de vistoria.
- **Vinculado / Não vinculado / Atrasado / Revisão necessária:** o estado de vínculo de um item do escopo (Revisão necessária quando algum vínculo está Desatualizado ou Indisponível).
- **Atual / Desatualizado / Indisponível:** um registro vinculado comparado com sua revisão fixada. Revise um vínculo desatualizado e use **Fixar novamente em** para fixá-lo na nova revisão.
- **Desconhecido / não definido:** nenhuma prioridade foi informada. Desconhecido não significa "Normal" nem "Baixa".

## O que você não pode fazer / cuidado
- A página não decide que uma docagem é segura nem que a embarcação está em condições de navegar.
- Você não pode verificar um pacote que você concluiu. Rejeitar a verificação faz o pacote voltar para **Em andamento**.
- Custo, moeda, fornecedor e orçamento são apenas referências externas; nada é totalizado, aprovado ou encomendado aqui.
- Um pacote concluído, verificado ou cancelado não pode ser editado.
- Os comandos não são enviados offline.

## Se algo der errado
- **O servidor listou itens ausentes ou impeditivos.:** resolva a lista de impedimentos e tente novamente.
- **The record changed or the command conflicts:** use **Recarregar a versão mais recente** e revise.
- **The outcome is unknown:** **Tentar novamente** envia de novo o comando idêntico.
- **Você não tem acesso a este registro.:** verifique seu papel e o contexto da embarcação; contate o administrador do seu tenant, se necessário (13.03 §10).
- **Você não pode ler estes tipos de registros vinculados:** verifique a permissão com o responsável pelo registro ou com o administrador do seu tenant.

## Páginas relacionadas
