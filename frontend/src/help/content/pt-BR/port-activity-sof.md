---
key: port-activity-sof
title: Atividade portuária e SOF
roles: [Master, Chief Engineer]
permissions: [operations.port.read, operations.port.write, operations.port.finalize]
related: [noon-report, deck-daily-log, fuel-bunker-log, today]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página de atividade portuária e SOF mantém os **eventos portuários** do navio selecionado como um registro operacional com referências de evidência e permite que uma pessoa designada finalize internamente o **Statement of Facts (SOF)**. Ela responde: "Quais eventos portuários declaramos, em qual ordem e intervalo UTC, com quais referências de carga, calado, prático, rebocador, atraso e protesto, e uma pessoa designada finalizou esta revisão do SOF?" A página não produz assinatura, laytime nem demurrage, e os eventos registrados não são instruções nem janelas planejadas aprovadas.

## Quem usa
- **Comandante:** para manter a chegada e a atividade portuária como um registro operacional com evidências e para finalizar o SOF (13.03 §2).
- **Imediato / Chefe de máquinas:** para registrar eventos portuários e, com uma designação vigente, finalizar o SOF.

## Antes de começar
- Na barra de contexto superior, selecione a **empresa e o navio corretos**. Se nenhum navio estiver selecionado, a página mostra "Select a ship".
- A leitura exige `operations.port.read`; salvar rascunhos exige `operations.port.write` e direitos de edição neste navio.
- A finalização exige `operations.port.write`, direitos de edição neste navio e `operations.port.finalize`, além de uma designação de finalização vigente (Comandante, Imediato ou Chefe de máquinas) para este navio.

## Passo a passo
1. Escolha o navio. **Cronologia de eventos portuários registrados** lista os eventos retornados na ordem UTC do servidor. **Filtrar página retornada** restringe apenas esta página.
2. Leia a **Linha do tempo do Statement of Facts** abaixo da lista: cada evento com **Intervalo registrado**, **Motivo do atraso** e **Observações**.
3. Pressione **Novo registro em rascunho**. Preencha **Contexto de escala portuária**, **Atividade e horários registrados**, **Declarações de carga e calado** e **Apoio, exceções e evidências**. Os horários dos eventos exigem Z ou um deslocamento UTC explícito.
4. Pressione **Salvar rascunho não verificado**. Cada salvamento adiciona uma revisão ao **Histórico de revisões registradas**.
5. Em um registro salvo, abra **Finalização do Statement of Facts**. Se **Fatos obrigatórios ausentes** for exibido, preencha primeiro esses campos.
6. Marque cada referência não resolvida listada para reconhecê-la, escolha sua **Designação** e pressione **Finalizar SOF**. O sistema registra quem finalizou, quando, a função e o motivo.

## O que significam os estados
- **RASCUNHO:** o SOF não está finalizado.
- **FINAL (interno):** uma pessoa designada finalizou esta revisão. É interno: não é uma assinatura nem um acordo externo.
- **Assinatura não registrada:** sempre exibido; não existe assinatura nesta página.
- **Alterado após FINAL: nova revisão e finalização são necessárias.:** o registro foi salvo novamente após a finalização e voltou a RASCUNHO.
- **UNKNOWN (referências não resolvidas):** não existe consulta proprietária para essas referências obrigatórias. Reconhecê-las não as torna conhecidas.
- **Revisão necessária / Registro em rascunho não verificado:** os eventos portuários são declarações; nada é verificado.
- **Rascunho (não finalizado) / Final (interno; não é uma assinatura):** o estado do SOF que o servidor registrou para esta revisão, exibido no resumo do registro. Final só resulta de uma finalização humana registrada.

## O que você não pode fazer / cuidado
- O sistema nunca finaliza o SOF. A finalização é um ato humano de um Comandante, Imediato ou Chefe de máquinas designado (13.03 §2).
- Finalizar SOF permanece desativado enquanto faltarem fatos obrigatórios, não houver designação ou as referências não resolvidas não tiverem sido reconhecidas.
- A finalização não é uma assinatura, não é um acordo externo e não é laytime nem demurrage.
- O rodapé da página informa: "SOF finalization is an internal human act recorded in the finalization panel by an appointed Master, Chief Officer or Chief Engineer. Signatures, operational/legal approval and offline capture remain unavailable."
- Salvar rascunhos e finalizar exigem uma sessão online vigente; esta página não tem captura offline.

## Se algo der errado
- **"No current finalization appointment for this vessel.":** verifique sua função e o contexto do navio com o administrador do seu tenant (13.03 §10).
- **"Outcome unknown: retry sends the same command once.":** use **Tentar novamente**; não inicie uma nova finalização.
- **"Command not accepted":** leia o código exibido, recarregue o registro com **Ler registro salvo atual** e tente novamente.
- **"The saved source changed.":** recarregue ou descarte explicitamente antes de salvar novamente.

## Páginas relacionadas
