---
key: tug-equipment-readiness
title: Prontidão do equipamento de reboque
roles: [Tug Master, Chief Engineer]
permissions: [mode.tug, tug.read, tug.write, equipment.write]
related: [tug-readiness, tug-crew-readiness, tug-assignment, tug-command]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página mostra as **declarações de equipamentos de reboque** do rebocador selecionado (guincho de reboque, gato de reboque, cabo de reboque, liberação de emergência, combate a incêndio FiFi) e suas **verificações do chefe de máquinas**. Ela responde: "Qual é a CMT, as datas de teste, a condição e a criticidade de cada item, quem o verificou e o que diz a verificação de prontidão?" A avaliação é apenas orientativa e alimenta a verificação de prontidão; não é uma decisão GO.

## Quem usa
- **Mestre do rebocador:** para verificar o guincho, o cabo de reboque, o gato de reboque / mordente de corrente, a liberação de emergência, o FiFi e as evidências de defeitos críticos (13.03 §3).
- **Chefe de máquinas:** para verificar ou não verificar as declarações e registrar o efeito de um defeito crítico na prontidão (13.03 §5).

## Antes de começar
- Abra o rebocador em **Operações de rebocador**; esta tela não precisa de atribuição.
- Adicionar ou editar uma declaração exige acesso de escrita de rebocador e de escrita de equipamentos.
- Registrar uma verificação exige uma designação de **Chefe de máquinas** registrada no servidor para esse rebocador.

## Passo a passo
1. Leia o selo geral na linha **Avaliação**.
2. Em cada cartão de equipamento, verifique o selo de resultado, o código de motivo, **CMT (t)**, **Próximo teste previsto**, **Condição** e **Crítico**.
3. Para uma nova declaração, pressione **Adicionar equipamento**; preencha **Tipo de equipamento**, **Rótulo**, **CMT (t)**, **Último teste**, **Próximo teste previsto**, **Condição**, **Crítico**, **ID do ativo PMS**, **ID do certificado** e **Observações** e depois pressione **Salvar declaração**.
4. Para alterar uma declaração existente, use **Editar** no cartão dela.
5. Como Chefe de máquinas, escolha sua **Designação**, escreva um motivo e use **Verificado** ou **Não verificado** para **Registrar verificação**. Um motivo é obrigatório para "Não verificado".
6. Veja o histórico de verificações com revisão e horário em **Verificações do chefe de máquinas** e a tabela **Verificações de fontes** na parte inferior.

## O que significam os estados
- **Pronto / Condicional / Não pronto / Revisão necessária:** a avaliação orientativa. Pronto não é uma decisão GO.
- **Aprovado / Reprovado / Desconhecido:** o resultado da verificação de um item.
- **Operacional / Degradado / Com falha / Desconhecido:** a condição declarada. Se nenhuma condição for escolhida, ela permanece "Desconhecido"; não é contada como operacional.
- **Não avaliado:** a criticidade não foi avaliada. Isso não significa "Não".
- **Verificado / Não verificado:** o registro humano do Chefe de máquinas.
- **substituído por revisão posterior:** a verificação pertencia a uma revisão anterior da declaração; a revisão atual precisa de uma nova verificação.

## O que você não pode fazer / cuidado
- Salvar registra uma declaração não verificada. Qualquer alteração precisa de uma nova verificação do Chefe de máquinas.
- Concluir um registro e verificá-lo não são a mesma ação; a verificação aguarda a pessoa autorizada (13.03 §1).
- O tipo de equipamento não pode ser alterado depois de salvo; adicione uma nova declaração para outro tipo.
- A página nunca declara o equipamento de reboque seguro; a decisão continua com o Mestre do rebocador (13.03 §3).
- Os comandos ficam desativados offline.

## Se algo der errado
- **Mensagem "The record changed":** use **Recarregar** para revisar a declaração atual.
- **Mensagem "The outcome is uncertain":** **Tentar novamente** envia o mesmo comando.
- **O registro não pode ser verificado:** verifique as evidências e a fonte PMS/certificado; entre em contato com o verificador (13.03 §10).
- **Sem autorização:** verifique seu papel, o rebocador e o contexto de designação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).

## Páginas relacionadas
