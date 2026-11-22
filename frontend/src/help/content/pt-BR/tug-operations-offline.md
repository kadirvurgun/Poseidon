---
key: tug-operations-offline
title: Operações offline do rebocador
roles: [Tug Master, Crew]
permissions: [mode.tug, tug.read, tug.write]
related: [towage-risk-briefing, towage-execution, tug-daily-log, offline-conflict-center]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página mantém no dispositivo um **instantâneo pré-carregado** de um serviço de rebocador e coloca três tipos de capturas feitas sem conexão em uma **fila durável e criptografada**: confirmações de recebimento do briefing, observações de execução e entradas do diário. Ela responde: "O que posso capturar com segurança sem conexão, o que está aguardando na fila e como o servidor o recebeu?" A liberação do Comandante, o GO de prontidão, o início/as transições da execução, a verificação e as revisões continuam sendo registros humanos online.

## Quem usa
- **Mestre do rebocador:** para continuar o trabalho crítico onde a conexão é fraca e acompanhar o estado da sincronização (13.03 §10).
- **Tripulação:** para fazer confirmações de recebimento do briefing e entradas de quarto offline, e para resolver conflitos sem presumir que o próprio registro prevalece automaticamente (13.03 §6).

## Antes de começar
- Abra a página a partir de um serviço de rebocador; sem um serviço, a página mostra "Open this screen from a tug job to prefetch its snapshot."
- A linha **Política offline** deve mostrar uma política de armazenamento configurada (`CONFIGURED_UNVERIFIED`); sem uma política, o pré-carregamento do instantâneo e a captura offline ficam indisponíveis.
- Pré-carregue o instantâneo **enquanto estiver online**. Antes de capturar, verifique se a concessão do instantâneo não expirou.

## Passo a passo
1. Enquanto estiver online, pressione **Pré-carregar instantâneo do trabalho**. A mensagem "Snapshot sealed on this device." aparece.
2. Em **Instantâneo do trabalho**, verifique o número do serviço, seu estado e **Concessão até**.
3. Se você é participante de um briefing emitido, escolha **Confirmar recebimento** ou **Contestar** (com um motivo de pelo menos 10 caracteres) em **Confirmar briefing** e pressione **Selar e enfileirar**.
4. Para uma execução em andamento, escolha a execução e o tipo de observação (Note, Towline connected, Towline released) em **Registrar observação de execução**, escreva a nota e pressione **Selar e enfileirar**.
5. Para um diário em rascunho ou devolvido, escolha o diário em **Entrada do diário**, escreva o **Resumo do dia** e pressione **Selar e enfileirar**.
6. Acompanhe o estado de cada captura em **Capturas na fila**. As capturas são enviadas em ordem quando a conexão volta; para enviar manualmente, use **Enviar capturas na fila**.
7. Se uma captura mostrar **Conflito material**, leia **Alterado no servidor**, escolha explicitamente **Manter o registro do servidor** ou **Aplicar meu valor capturado no registro atual** e pressione **Resolver conflito**.

## O que significam os estados
- **Na fila do dispositivo (não enviado):** a captura está selada neste dispositivo; estar na fila não significa estar enviada.
- **Aceito pelo servidor:** o servidor recebeu a captura. Isso não é uma aprovação nem uma verificação.
- **Conflito material:** o registro do servidor mudou depois da captura; ele nunca é mesclado automaticamente e aguarda a decisão de uma pessoa.
- **Valor do servidor mantido / Recusado pelo servidor:** o resultado do servidor para a captura.
- **UNKNOWN (offline):** o estado da política não pôde ser lido offline; nenhuma política é presumida.

## O que você não pode fazer / cuidado
- Somente online: liberação do Comandante, GO de prontidão, início e transições da execução, emissão do briefing, verificação, envio e revisão do diário.
- Conflitos materiais nunca são mesclados automaticamente; a resolução é sempre uma escolha humana explícita (13.03 §1).
- Nenhuma nova captura pode ser feita com uma concessão de instantâneo expirada; pré-carregue um novo instantâneo.
- Na reprodução, cada captura é processada uma única vez com seu id original; não crie a mesma captura novamente.

## Se algo der errado
- **Mensagem "The device key for this queue is unavailable.":** os registros capturados são mantidos, mas não podem ser lidos; não os exclua e entre em contato com o suporte.
- **Mensagem "Outcome unknown":** a mesma captura será tentada novamente com seu id original; não crie uma nova.
- **Mensagem "The offline policy does not allow a snapshot.":** fale com o administrador do seu tenant sobre a configuração da política (13.03 §10).
- **Conflito material:** use o formulário **Resolver conflito** nesta página; os conflitos também aparecem na **Central de conflitos materiais** (13.03 §10).

## Páginas relacionadas
