---
key: tug-command
title: Comando de rebocadores
roles: [Tug Master, Tug Operations Coordinator, Shore Operations, Crew]
permissions: [mode.tug, tug.read]
related: [tug-job-board, tug-job-intake, tug-assignment, tug-readiness, today]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Para que serve esta página
Exibida no menu como **Comando de rebocadores**, esta página mostra o **contexto de origem** do rebocador selecionado. Ela responde: "Quais declarações de função, tipo, tração estática (BP) e equipamentos estão registradas para este rebocador, de onde vêm e quão atuais são?" A página mostra apenas as declarações e suas fontes; não concede nenhuma aprovação de prontidão operacional, elegibilidade, permissão ou segurança. O selo no topo diz isso: "Source declarations · operational readiness unknown".

## Quem usa
- **Mestre do rebocador:** para confirmar o contexto de função, BP e equipamentos do seu próprio rebocador antes de o trabalho começar (13.03 §3).
- **Coordenador de operações de rebocador / Operações em terra:** para garantir que estão no contexto de rebocador correto antes de ir ao quadro de serviços e ao recebimento de serviços (13.03 §7).
- **Tripulação:** para ver sua visão de função e as telas do modo navio abertas para ela (13.03 §6).

## Antes de começar
- Você precisa do modo rebocador e de acesso de leitura de rebocador. Sem isso, a página mostra "O acesso está indisponível."
- Se nenhum contexto estiver selecionado, a página lista os rebocadores autorizados em **Selecionar rebocador**; abra um.
- A lista de equipamentos também exige acesso de leitura de equipamentos; sem ele, a fonte de equipamentos aparece como "unavailable or not granted".

## Passo a passo
1. Abra o rebocador na lista **Selecionar rebocador**. A linha **Contexto do rebocador** abaixo do título mostra o código, o nome e o modo do rebocador.
2. Em **Função (atribuição do servidor)** e **Visão de menu do modo rebocador**, leia a função e a visão de menu que o servidor atribuiu a você.
3. Em **Visibilidade de modo**, veja se as telas do modo navio estão abertas. A seção **Telas do modo navio** só contém links para as telas concedidas a você.
4. Verifique em **Tipo de rebocador**, **Fonte / revisão do tipo** e **Observação / política da fonte do tipo** de qual fonte e revisão o tipo vem.
5. Leia **BP do perfil (não verificada)**, **Fonte / versão / proveniência de BP** e **Atualidade / observação de BP**. Este é um valor declarado, não medido.
6. Em **Contexto do equipamento**, veja as declarações de equipamentos, seus níveis, revisões e proveniência.
7. Use as abas da página para ir ao **Quadro de serviços** ou (com acesso de escrita) ao **Recebimento de serviços**, ou (com acesso de escrita) pressione **Nova solicitação**.

## O que significam os estados
- **Desconhecido — sem atribuição de função:** o servidor não tem nenhuma atribuição de função para você neste rebocador. Isso não é "sem função, sem problema"; é necessária uma atribuição feita por um administrador.
- **Rótulos de função indisponíveis / Visão de menu indisponível:** a fonte não pode ser lida agora. Informação ausente não é tratada como vazia.
- **Nenhuma visão configurada para o modo rebocador:** nenhuma visão de menu está definida para você neste modo.
- **Ocultas no modo rebocador. É necessária uma função multimodo.:** as telas do modo navio exigem uma função multimodo autorizada explicitamente.
- **BP do perfil (não verificada):** a tração estática declarada. Não é um resultado de teste verificado.
- **Nenhuma declaração de equipamento retornada. A aptidão operacional verificada permanece desconhecida.:** uma lista vazia nunca significa que o equipamento está apto.
- **Desconhecido / Indisponível:** o valor de origem está ausente ou não pôde ser lido. Desconhecido nunca substitui "Pronto" ou "Não".

## O que você não pode fazer / cuidado
- Nenhum campo é editado nesta página e nenhum resultado de prontidão é produzido. A decisão operacional e a autoridade de segurança continuam com o Mestre do rebocador a bordo (13.03 §3).
- Uma proposta de atribuição feita em terra nunca se sobrepõe à decisão de segurança a bordo do Mestre do rebocador (13.03 §7).
- Mesmo com uma permissão multimodo declarada, cada tela do modo navio ainda precisa de sua própria autorização do servidor.
- Offline, as fontes atuais do perfil não são mostradas; reconecte-se para verificar novamente o acesso e as versões das fontes.

## Se algo der errado
- **O acesso está indisponível:** verifique seu papel, o rebocador e o contexto do objeto; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **Alerta "Current identity and vessel access must be checked":** pressione **Verificar acesso novamente**.
- **Dados indisponíveis:** verifique sua conexão e pressione **Tentar novamente**.
- **Valores de BP ou de equipamentos inesperados:** anote os campos de fonte e versão e revise-os com o responsável pela fonte; se um registro não puder ser verificado, confira suas evidências e proveniência (13.03 §10).

## Páginas relacionadas
