---
key: safety-events
title: Eventos de segurança e quase acidentes
roles: [DPA / HSEQ, Master, Crew / OOW / Engineer]
permissions: [hse.safety_event.read, hse.safety_event.report, hse.safety_event.triage, hse.safety_event.identity.read, hse.safety_event.investigate, hse.safety_event.review, hse.safety_action.write, hse.safety_action.verify, hse.safety_action.effectiveness]
related: [voice-event-draft, risk-assessment, high-risk-work, industry-assurance]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Eventos de segurança e quase acidentes permite relatar rapidamente um evento de segurança em uma embarcação e acompanhar as etapas humanas que vêm depois: triagem, investigação, revisão independente, ações corretivas e preventivas, verificação, eficácia e encerramento. Ela responde: "O que aconteceu, quem está tratando, e a ação realmente funcionou?" Nada aqui decide automaticamente a gravidade, a causa raiz ou o encerramento; cada um é um registro humano separado. A página segue a cadeia Constatação → Ação → Verificação, e **encerrado não é o mesmo que eficaz** (13.03 §4).

## Quem usa
- **Crew / OOW / Engenheiro e Comandante:** relatam quase acidentes, incidentes, acidentes, fadiga e eventos ambientais ou cibernéticos (13.03 §2, §6).
- **DPA / HSEQ:** como ponte de segurança, acompanham eventos críticos e ações atrasadas e gerenciam a investigação e a eficácia das ações (13.03 §4).
- Triagem, investigação, revisão, leitura de identidade, escrita de ações, verificação e revisão de eficácia são permissões separadas; cada usuário vê apenas as partes que possui.

## Antes de começar
- Escolha a embarcação correta (**Escolha uma embarcação**). Sem acesso, você vê "You do not have access to safety events for this vessel."
- Relatar exige a permissão de relato; ler a lista exige a permissão de leitura.
- Um relato **Confidencial** e **Enviar relato** exigem conexão. Offline, você só pode salvar um rascunho identificado, e apenas em um dispositivo preparado previamente.
- O horário é inserido em UTC.

## Passo a passo
1. Veja os eventos da embarcação na lista **Eventos**. Use **Somente meus relatos** para ver apenas os seus.
2. Para um novo registro, pressione **Relatar um evento**. Escolha **Identificado** ou **Confidencial** como **Modo de relato**. Para um relato confidencial que precise de acompanhamento, escolha **Quero um código privado de acompanhamento**.
3. Preencha **Tipo de evento**, **Quando aconteceu?**, **Operação**, **Local a bordo**, **Resumo**, **O que aconteceu**, **Foi tomada ação imediata?** e, opcionalmente, **Sua estimativa de gravidade**. Se o horário não for conhecido, escolha **Desconhecido** e preencha **Por que o horário é desconhecido?**.
4. Você pode usar **Salvar rascunho** enquanto ele estiver incompleto. Quando **Ainda necessário antes do envio** estiver vazio, pressione **Enviar relato**. Um rascunho não enviado pode ser retirado com **Retirar rascunho** e um motivo.
5. Se um código de acompanhamento foi emitido, guarde imediatamente e de forma privada o código em **Seu código confidencial de acompanhamento** e pressione **Já guardei o código**. Ele é exibido apenas uma vez. Depois, insira-o como **Código de acompanhamento** em **Acompanhamento confidencial** e pressione **Abrir** para ler as respostas da equipe.
6. Um responsável pela triagem registra a gravidade e **Investigação necessária** com um motivo em **Triagem** e pressiona **Registrar triagem**, ou devolve o relato com **Devolver ao relator**.
7. Um investigador pressiona **Abrir investigação**, preenche **Fatores contribuintes**, **Causas raiz**, **Barreiras** e **Incertezas** e depois pressiona **Salvar investigação** e **Enviar para revisão**. Em **Revisão independente**, outra pessoa escolhe **Aprovar**, **Devolver** ou **Rejeitar**.
8. Em **Ações corretivas e preventivas**, use **Adicionar ação** com um responsável, um prazo e a constatação que ela trata. O responsável registra **Informar conclusão**, outra pessoa registra **Verificar** e uma terceira registra **Avaliar eficácia**.
9. Quando **Bloqueios de encerramento** estiver vazio, um revisor (com permissão de revisão) que não seja o relator nem autor da investigação pressiona **Encerrar o evento**. Em **Mensagens**, você pode enviar mensagens com visibilidade **Somente equipe** ou **Compartilhada com o relator**.

## O que significam os estados
- **Rascunho / Enviado / Devolvido / Triado / Em investigação / Acompanhamento de ações / Encerrado / Retirado:** o ciclo de vida do evento.
- **Gravidade — Não triado / Não avaliado:** ainda não há classificação humana. A **Estimativa do relator** é apenas uma sugestão. **Severity policy: not configured – human classification only** mostra que o sistema não deriva nenhuma gravidade.
- **A necessidade de investigação é UNKNOWN:** nenhuma decisão foi tomada; isso não significa "não" e bloqueia o encerramento.
- **Ação — Aberta / Concluída / Verificada:** Concluída não é Verificada, e Verificada não é Eficaz.
- **Eficácia — Pendente / Eficaz / Não eficaz / Inconclusiva / Sem ações / Não visível:** a eficácia é uma revisão humana por ação e é separada do encerramento. **Sem ações** não significa eficaz.
- Resumo **Eventos de segurança em aberto**: **Eventos de alta gravidade em aberto**, **Revisão necessária** ou **Nenhum evento de alta gravidade em aberto**. É apenas um aviso; não fornece nenhuma decisão de prontidão.
- **Confidencial (protegido):** a identidade do relator fica em um cofre restrito e não é exibida aos leitores.
- **Capturado offline / Aguardando sincronização / Salvo neste dispositivo. Ainda não enviado.** Enfileirado não é enviado.

## O que você não pode fazer / cuidado
- Um relator não pode fazer a triagem do próprio relato. Um autor da investigação, quem a enviou ou o relator não podem fazer a revisão independente.
- Somente o responsável pela ação informa a conclusão. Nem o responsável nem quem concluiu podem verificar; nenhum dentre responsável, quem concluiu e verificador pode avaliar a eficácia. São registros separados feitos por pessoas diferentes.
- As causas raiz são declarações humanas; o sistema nunca propõe uma.
- Um relato enviado nunca pode ser retirado nem excluído pelo seu autor.
- Em um relato confidencial, a identidade só é aberta com base na necessidade de conhecer, por alguém que tenha a permissão de leitura de identidade e escolha uma **Finalidade**; cada leitura é registrada com sua finalidade. O relato anônimo exige o canal público de recebimento, que não está configurado.
- Uma revisão Inconclusiva pode ser repetida; Eficaz e Não eficaz são definitivas; uma ação que não funcionou exige uma nova ação de acompanhamento.

## Se algo der errado
- **O servidor recusou o comando:** conclua os itens faltantes ou bloqueantes listados abaixo dele.
- **O registro foi alterado nesse meio-tempo. Recarregue e revise antes de tentar novamente.** Pressione **Recarregar**.
- **O resultado é desconhecido (conexão perdida).** Use **Repetir o mesmo comando**; ele não será aplicado duas vezes.
- **Esta ação não é permitida para você.** Verifique sua função, a embarcação e o contexto do objeto; se necessário, entre em contato com o administrador do tenant (13.03 §10).
- **The device key is unavailable** ou **Online re-authorisation is needed:** os dados capturados são mantidos; autorize novamente quando estiver online. Em caso de erro da aplicação, entre em contato com o suporte informando o correlation ID, a tela, o horário e os passos (13.03 §10).

## Páginas relacionadas
