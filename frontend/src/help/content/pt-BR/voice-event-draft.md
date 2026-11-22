---
key: voice-event-draft
title: Rascunho de evento por voz
roles: [Crew / OOW / Engineer, Master, DPA / HSEQ]
permissions: [voice.draft.read, voice.draft.write, hse.safety_event.report]
related: [safety-events]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Rascunho de evento por voz transforma uma nota curta, falada ou digitada, em **sugestões de rascunho** para um relato de evento de segurança. Ela responde: "O tipo de evento, o local, o horário, o que aconteceu e a ação imediata foram extraídos corretamente do que eu disse ou escrevi?" As sugestões vêm de regras locais fixas, cada uma rastreada até as suas próprias palavras com um grau de confiança. Uma pessoa edita e confirma o rascunho; nada é enviado, encerrado ou relatado automaticamente (13.03 §1, §6).

## Quem usa
- **Crew / OOW / Engenheiro e Comandante:** fazem uma nota rápida em campo e iniciam um relato de evento de segurança (13.03 §2, §6).
- **DPA / HSEQ:** acompanham **Usabilidade medida (esta embarcação)**. Esses números são uma observação operacional, não uma declaração de precisão.

## Antes de começar
- Escolha um navio dentro do seu escopo. Criar um rascunho exige a permissão de escrita de rascunhos de voz e escopo de edição nesse navio.
- Repassar um rascunho confirmado para um rascunho de evento de segurança também exige a permissão de relato de eventos de segurança.
- Privacidade: o áudio não é armazenado. A fala usa apenas um mecanismo local nesta instalação; se nenhum estiver instalado, digite ou cole a nota. Evite nomes e detalhes pessoais de saúde.
- Offline, nada é enviado.

## Passo a passo
1. Pressione **Nova nota de voz ou digitada**.
2. Se houver um mecanismo local configurado, pressione **Gravar com mecanismo local** e depois **Parar e transcrever**. Caso contrário, digite uma nota curta em **Texto da nota**: o quê, onde, quando, o que você fez.
3. Verifique a **Data da nota**. Ela é usada apenas para interpretar palavras como "hoje" ou "ontem".
4. Pressione **Criar sugestões de rascunho**.
5. Verifique cada campo: **Tipo de evento**, **Quando ocorreu (hora local)**, **Local a bordo**, **Detalhe do local**, **O que aconteceu**, **Ação imediata tomada**. Abaixo de cada campo, você vê a confiança da sugestão, a regra e o trecho correspondente da sua nota. Leia também **Notas das regras**.
6. Corrija as sugestões erradas e pressione **Salvar edições**. Quando **Ainda necessário antes de confirmar:** estiver vazio, pressione **Confirmar rascunho**. Confirmar registra que você verificou cada campo; não envia nem encerra um evento.
7. Após a confirmação, se um rascunho de evento de segurança tiver sido criado, siga **Abrir o rascunho do evento de segurança** até a página Safety events & near misses e complete e envie-o lá. Se ainda não tiver sido criado e você tiver a permissão, use **Criar agora o rascunho do evento de segurança**.
8. Se a nota não for um evento ou estiver errada, escolha um **Motivo**, adicione **Nota (obrigatória para Outro)** se necessário e pressione **Rejeitar rascunho**.

## O que significam os estados
- **Rascunho (não confirmado):** nenhuma pessoa confirmou as sugestões ainda.
- **Confirmado por uma pessoa:** os campos foram verificados; isso não é um relato de evento de segurança e nada foi enviado.
- **Rejeitado:** o motivo da rejeição é registrado para avaliação.
- **Sugerido, inalterado / Alterado por você / Inserido por você / Vazio:** a origem de um campo. Sugerido, inalterado é uma sugestão que uma pessoa ainda precisa verificar.
- **Digitado ou colado / Mecanismo de fala local:** como a nota foi inserida. Editar o texto do mecanismo local o transforma em uma nota digitada.
- Notas de regras como **Hora sem dia; nenhuma data presumida** mostram informações ausentes; o sistema não adivinha o valor ausente.
- Uma nota como **2 missing**: quantos campos obrigatórios do rascunho ainda estão vazios na lista.

## O que você não pode fazer / cuidado
- As sugestões são apenas um rascunho; um grau de confiança não é garantia de exatidão. Verifique você mesmo cada campo antes de confirmar.
- Esta página não envia, não encerra nem relata um evento de segurança. Você envia o relato na página Safety events & near misses.
- Se forem mencionadas evidências, anexe-as você mesmo; a página não anexa nenhuma evidência.
- **Confirmar rascunho** não está disponível enquanto houver edições não salvas; salve-as primeiro.
- Sem a permissão de relato de eventos de segurança, nenhum rascunho de evento de segurança é criado a partir de um rascunho confirmado.

## Se algo der errado
- **Speech capability unavailable: no local engine is configured.** ou **Transcription was not completed.** Digite ou cole a nota.
- **O acesso ao microfone não foi concedido.** Verifique a permissão do navegador ou digite a nota.
- **The record changed meanwhile.** Pressione **Recarregar** e refaça a sua edição.
- **You are offline. Nothing was sent.** Quando estiver online, use **Repetir o mesmo comando**.
- **Você não tem permissão para esta ação.** Verifique seu papel e o contexto do navio; se necessário, entre em contato com o administrador do tenant (13.03 §10).

## Páginas relacionadas
