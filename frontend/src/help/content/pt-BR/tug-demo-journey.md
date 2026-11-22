---
key: tug-demo-journey
title: Jornada demo do rebocador
roles: [Tug Operations Coordinator, Shore Operations, Tug Master]
permissions: [mode.tug, tug.read]
related: [tug-job-intake, tug-assignment, tug-readiness, towage-plan, towage-risk-briefing, towage-execution]
screenshots: []
reviewed: false
---
## Para que serve esta página
A jornada demo é um percurso guiado pelas telas reais de rebocador com **dados sintéticos, não assinados e não verificados**. Ela responde: "Por quais telas um serviço de rebocador passa, da solicitação ao histórico, e qual controle o servidor aplica em cada etapa?" A demo não cria nenhuma decisão, liberação ou verificação; ela apenas leva às telas reais e as explica.

## Quem usa
- **Coordenador de operações de rebocador / Operações em terra:** para aprender o fluxo de trabalho e o controle de cada etapa (13.03 §7).
- **Mestre do rebocador:** para ver onde ficam, de ponta a ponta, as etapas de decisão a bordo (GO de prontidão, execução) (13.03 §3).

## Antes de começar
- Selecione o rebocador; sem um rebocador selecionado, a página mostra "Select a tug vessel to continue."
- Se você abrir a página com um serviço, os links das etapas levam esse serviço.
- Cada link abre a tela real; as regras de autorização dessa tela se aplicam.

## Passo a passo
1. Siga as etapas em ordem: **Recebimento de serviços**, **Atribuição de rebocador**, **Prontidão de tração estática, tripulação e equipamentos**, **Plano de reboque**, **Avaliação de riscos e briefing**, **Master release (registro humano GO)**, **Execução**, **Evidências e verificação**, **Histórico de operações**, **Continuidade offline**.
2. Em cada etapa, use **Abrir tela** para ir à tela real.
3. Abra **Por quê** para ler o controle que o servidor aplica naquela etapa; por exemplo, o plano é aprovado por um Mestre do rebocador ou Comandante que não o redigiu, e cada participante confirma o recebimento da revisão exata emitida.

## O que significam os estados
- Esta página não tem selo de estado próprio. O significado dos estados nas telas que você abre é descrito no guia de cada tela.
- Como a etapa de prontidão explica, UNKNOWN permanece REVIEW_REQUIRED, nunca READY.
- GO é um registro humano vinculado à avaliação de prontidão atual; o sistema nunca o cria.

## O que você não pode fazer / cuidado
- Os dados da demo são fictícios e não verificados. Liberações, decisões GO e verificações são sempre registradas por pessoas autorizadas, nunca pela demo.
- Um resultado que você vê nas telas da demo nunca substitui uma decisão de segurança de uma operação real; a decisão continua com o Mestre do rebocador (13.03 §3).
- Uma atribuição feita em terra nunca se sobrepõe à decisão de segurança a bordo do Mestre do rebocador (13.03 §7).

## Se algo der errado
- **Mensagem "This page address is not valid.":** abra a página novamente pelo menu.
- **A tela de uma etapa não abre ou falta acesso:** verifique seu papel e o contexto do rebocador para essa tela; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **Resultado de prontidão inesperado:** abra a visão Por quê da tela relacionada e revise-a com o responsável pelo controle (13.03 §10).

## Páginas relacionadas
