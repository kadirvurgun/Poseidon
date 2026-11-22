---
key: survey-planner
title: Planejador de vistorias
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read]
related: [pre-survey-checklist, certificate-command-center, renewal-workflow, drydock-planning]
screenshots: []
reviewed: false
---
## Para que serve esta página
O Planejador de vistorias mostra as janelas de vistoria declaradas em um calendário da frota, junto com a adequação à viagem e ao porto. Ele responde: "No período que escolhi, quais certificados de quais embarcações têm uma janela de vistoria se abrindo ou já encerrada, e existe um checklist de preparação?" É apenas um auxílio ao planejamento: não é um cronograma da autoridade, um agendamento nem um resultado de vistoria (13.03 §8).

## Quem usa
- **Comandante, Chefe de máquinas / Superintendente técnico:** veem as próximas vistorias e iniciam a tempo o checklist de preparação (13.03 §5).
- **Usuário de Regulatório / Garantia:** acompanha as janelas declaradas e as declarações faltantes em toda a frota (13.03 §8).

## Antes de começar
- A visualização exige leitura de certificados. Somente as embarcações às quais você tem acesso são listadas.
- As janelas vêm das declarações no registro do certificado. Se nenhuma janela estiver declarada, a linha aparece em **Janela não declarada**; insira as datas do documento de origem na [Central de comando de certificados](/app/certificate-command-center).
- A página exige uma leitura online. Offline, você vê "Offline: a current online read is required."

## Passo a passo
1. Em **Período do calendário**, escolha **De** e **Até** e pressione **Mostrar**.
2. Leia as contagens de abertas, próximas, vencidas e desconhecidas na linha de resumo.
3. Em cada linha, revise **Certificado**, **Emissor**, **Bandeira declarada**, **Base regulatória**, **Vencimento declarado**, **Janela de vistoria** e **Adequação viagem/porto**.
4. Pressione **Abrir certificado** para ver o certificado.
5. Use **Abrir checklist** se existir uma lista de preparação, ou **Iniciar checklist** se não existir, para ir ao [Checklist pré-vistoria](/app/pre-survey-checklist).

## O que significam os estados
- **Próxima / Janela aberta:** a posição em relação à janela declarada.
- **Janela encerrada, cumprimento não registrado:** a janela terminou e nenhum registro mostra que a vistoria foi feita. Isso também não prova que a vistoria foi perdida; verifique os registros.
- **Janela desconhecida:** a data está ausente ou ilegível. Uma janela desconhecida nunca significa "nenhum problema"; ela precisa de revisão.
- Adequação viagem/porto: **Condicional**, **Não adequado**, **Desconhecido**. A página nunca dá uma aprovação de "adequado".
- Prontidão do checklist: **Bloqueado**, **Incompleto**, **Preparado, aguardando confirmação humana**.

## O que você não pode fazer / cuidado
- O planejador não agenda nada nem protocola nada junto a uma autoridade. A coordenação com classe, bandeira e autoridades é externa e não é simulada aqui.
- O que o calendário mostra não é um resultado de vistoria nem a validade de um certificado. As decisões de viagem e de vistoria cabem a pessoas autorizadas.
- A página é somente leitura; as alterações são feitas nas páginas de certificados e de checklists.

## Se algo der errado
- **Apenas parte da frota é exibida; reduza o período.** Escolha um período de calendário mais curto.
- **Nenhum certificado com janelas de vistoria neste período.** Amplie o período ou verifique as declarações dos certificados.
- **Não foi possível carregar. Tente novamente.** Pressione **Tentar novamente**. Se persistir, informe ao suporte a tela, o horário e os passos (13.03 §10).
- **Você não tem acesso a esta visualização.** Verifique sua função e o escopo da embarcação; se necessário, entre em contato com o Tenant Admin (13.03 §10).

## Páginas relacionadas
