---
key: operational-downtime
title: Parada operacional
roles: [Master, Chief Engineer, Technical Superintendent, DPA]
permissions: [operations.downtime.read, operations.downtime.write, operations.downtime.review]
related: [defects, work-order-center, engine-daily-log, today]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página registra as **interrupções operacionais declaradas** (paradas) de um navio, junto com a linhagem de suas fontes. Ela responde: "Qual interrupção foi declarada neste navio, quando, por qual causa e vinculada a qual defeito ou ordem de serviço, e em que ponto estão sua revisão e seu encerramento?" A página não decide o direito a loss of hire, a prestação segura do serviço nem qualquer liberação de segurança.

## Quem usa
- **Chefe de máquinas / Superintendente técnico:** para registrar paradas causadas por falha técnica ou manutenção e vinculá-las ao histórico de defeitos ou de ordens de serviço (13.03 §5).
- **Comandante:** para acompanhar o impacto operacional das paradas e revisá-las quando designado (13.03 §2).
- **Revisor designado (Comandante, Imediato, Chefe de máquinas, Fleet Manager, DPA):** para verificar, devolver para correção ou encerrar um registro de parada.

## Antes de começar
- Na barra de contexto superior, verifique se a **empresa correta** está selecionada; a página lista apenas os navios do seu escopo.
- A leitura exige acesso de leitura de paradas; abrir um rascunho exige acesso de escrita. As ações de revisão também exigem acesso de revisão e uma designação de função vigente para este navio.
- Para abrir uma parada vinculada, você pode usar **Abrir rascunho de indisponibilidade vinculado** na página de um defeito ou de uma ordem de serviço.

## Passo a passo
1. Escolha o navio na lista "Select a ship". A lista **Interrupções operacionais registradas** é aberta.
2. Use **Buscar causas nesta página** para restringir os registros desta página.
3. Para um novo registro, pressione **Novo rascunho de indisponibilidade**.
4. Para cada campo, escolha primeiro o estado do valor: **Ausente**, **Desconhecido** ou **Valor declarado**. Você só insere um valor enquanto "Valor declarado" estiver selecionado.
5. Em "Interruption start" e "Interruption end", digite um carimbo de data/hora completo, com segundos e um deslocamento UTC explícito.
6. Pressione **Salvar rascunho não avaliado**. Um rascunho incompleto pode ser salvo.
7. Em **Revisão e encerramento da parada**, vincule evidências da reclamação com **Selecionar evidência**, se necessário, e pressione **Enviar para revisão**.
8. Se você for um revisor designado, escolha sua "Appointment" e use **Verificar**, **Devolver para correção** ou **Encerrar**.
9. Revise as revisões anteriores em **Histórico imutável de rascunhos**.

## O que significam os estados
- **Rascunho / Revisão necessária:** o conteúdo registrado não foi avaliado. Os motivos estão em "Why this draft remains unassessed".
- **Não enviado / Aberto / Em revisão:** o estado do ciclo de vida do registro.
- **Verificado (humano):** uma pessoa designada, diferente de quem registrou, verificou o registro. Concluído não é Verificado (13.03 §1).
- **Encerrado:** a parada foi encerrada. Um encerramento verificado não é uma liberação de segurança.
- **Desconhecido:** o valor não foi declarado ou não pôde ser determinado. Desconhecido não significa "Não" nem "Não aplicável". Por isso, loss of hire e encerramento permanecem "Desconhecido".
- **Candidato a Loss of Hire:** apenas um candidato; não declara nenhum direito nem valor.
- **Intervalo registrado:** uma observação de engenharia não avaliada; não substitui os minutos declarados.

## O que você não pode fazer / cuidado
- Quem registrou a parada não pode verificá-la nem encerrá-la; outra pessoa designada deve fazer isso.
- Enviar, verificar e encerrar não são possíveis enquanto faltarem fatos obrigatórios.
- Vincular evidências da reclamação não é verificação.
- Nenhuma descrição, horário ou decisão de encerramento é copiado da fonte proprietária (defeito, ordem de serviço); apenas o vínculo com a revisão exata é mantido.
- Offline, as fontes atuais não podem ser lidas e não há armazenamento offline; suas entradas ficam apenas na página aberta.

## Se algo der errado
- **O resultado do salvamento é desconhecido:** seu comando original está protegido. Com a mesma conta, use "Retry exact downtime command".
- **O rascunho salvo mudou:** outra pessoa alterou o registro. Suas entradas são mantidas; use "Read current draft" para recarregar, ou descarte.
- **Os registros de parada ou o acesso atual estão indisponíveis:** verifique sua função e o contexto da embarcação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **A fonte mudou:** use a revisão atual da fonte proprietária para um novo rascunho.
- **Comando não aceito:** informe o suporte com uma captura de tela, o horário e seus passos.

## Páginas relacionadas
