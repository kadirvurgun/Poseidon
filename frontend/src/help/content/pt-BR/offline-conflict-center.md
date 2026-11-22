---
key: offline-conflict-center
title: Central de conflitos materiais
roles: [Crew, Tug Master, Master, Chief Engineer]
permissions: [directory.read, crew.read, mode.tug, tug.read, daily.noon.read, daily.deck.read, daily.engine.read, workorder.read, certificate.read]
related: [tug-operations-offline, tug-daily-log, today]
screenshots: []
reviewed: false
---
## Para que serve esta página
A central de conflitos materiais lista, para o navio ou rebocador selecionado, os **conflitos materiais entre suas alterações offline e o registro atual do servidor** e permite que uma pessoa os resolva com uma decisão fundamentada. Ela responde: "Quais das minhas alterações offline entraram em choque com um registro mais recente do servidor, e como, por quem e com qual motivo isso será resolvido?" Nada aqui é sobrescrito ou resolvido automaticamente.

## Quem usa
- **Tripulação / Mestre do rebocador / Comandante / Chefe de máquinas:** para ver e resolver seus próprios registros em conflito depois de trabalhar offline. Quando aparecer um conflito, não presuma que o seu próprio registro prevalece automaticamente (13.03 §6).
- Um conflito material exige uma resolução humana; esta página é o destino do fluxo "Conflito material → Central de conflitos" de 13.03 §10.

## Antes de começar
- A página lista apenas os conflitos que você registrou e exige acesso de leitura a pelo menos uma fonte legível (por exemplo, designação da tripulação, operações de rebocador, registro diário, evidência de ordem de serviço (com leitura de equipamentos e de manutenção), rascunho de renovação de certificado). Qualquer fonte legível abre a página; as permissões listadas são alternativas.
- A lista e as decisões exigem uma conexão atual com o servidor. Enquanto estiver offline, suas alterações locais continuam guardadas no dispositivo.
- Escolha o navio ou rebocador na lista **Embarcação**.

## Passo a passo
1. Escolha uma embarcação em **Embarcação**. A tabela abre com as colunas **Fonte**, **Registro**, **Versões**, **Capturado**, **Recebido pelo servidor** e **Estado**.
2. Se houver uma nota "Not included:", as fontes para as quais você não tem permissão de leitura não estão na lista.
3. Em uma linha com **Aberto — decisão necessária**, pressione **Revisar**.
4. Em **Revisão do conflito**, compare os valores **Antes da captura**, **Capturado** e **Servidor** de cada campo. Nenhuma opção vem pré-selecionada.
5. Para cada valor, escolha explicitamente **Manter valor do servidor** ou **Usar valor capturado**, preencha **Motivo da resolução (obrigatório)** e pressione **Enviar resolução**.
6. Para remover apenas a cópia local neste dispositivo, marque a confirmação em **Descartar minha alteração local** e pressione **Descartar alteração local**.
7. Use **Fechar revisão** para voltar à lista.

## O que significam os estados
- **Aberto — decisão necessária:** o conflito aguarda uma decisão humana.
- **Substituído por um conflito mais recente:** existe um conflito mais recente para o mesmo registro; revise o novo.
- **Resolvido por decisão explícita:** alguém o resolveu com uma decisão fundamentada. Isso não é uma aprovação operacional.
- **Restrito / Não informado / Desconhecido:** o valor está oculto por autorização, não foi registrado ou não pôde ser lido. Um valor desconhecido nunca é tratado como vazio ou "Não".
- **Recibo do servidor registrado para sua decisão explícita:** o servidor recebeu sua decisão. Este recibo não é uma aprovação operacional.

## O que você não pode fazer / cuidado
- Os conflitos nunca são mesclados nem resolvidos automaticamente; cada valor exige uma escolha humana explícita e um motivo (13.03 §1).
- A central resolve diretamente os conflitos de designação da tripulação. Para outras fontes, ela mostra "Resolve this conflict from the owning record's offline panel."; por exemplo, os registros de rebocador são resolvidos em **Operações offline do rebocador**.
- Descartar uma alteração local remove apenas a cópia do dispositivo; o registro do servidor não muda e o conflito continua registrado até uma decisão fundamentada.
- Uma alteração capturada em outro dispositivo deve ser resolvida naquele dispositivo; nada é descartado.
- O envio verifica novamente seu acesso e as versões dos registros; ele não concede aprovação operacional.

## Se algo der errado
- **Mensagem "The submission outcome is unknown":** antes de enviar novamente, use **Verificar resultado do envio**.
- **Mensagem "The server record changed after your review" / "The server record changed again":** sua decisão não foi aplicada; revise o conflito novamente.
- **"Material conflicts could not be loaded. Nothing was resolved.":** pressione **Tentar novamente**.
- **"You do not have access to the conflict center." / "Conflict access is unavailable for this vessel.":** verifique sua função e o contexto da embarcação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
