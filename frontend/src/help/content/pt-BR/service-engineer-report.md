---
key: service-engineer-report
title: Relatórios de serviço e engenheiro
roles: [Chief Engineer, Technical Superintendent, Crew / OOW]
permissions: [service.report.read, equipment.read, service.report.write, service.report.verify, file.read]
related: [work-order-center, defects, pms-command-center, today]
screenshots: []
reviewed: false
---
## Para que serve esta página
Esta página registra e lista os **relatórios de visitas de serviço e de engenheiro** na embarcação selecionada. Ela responde: "Quem fez qual trabalho, em qual equipamento e para qual ordem de serviço, quais peças e medições foram registradas, e o relatório está verificado?" Um relatório é vinculado a um ID de equipamento, a uma ordem de serviço e a defeitos, de modo que fica ligado ao histórico da ordem de serviço e do equipamento (13.03 §5). Um relatório verificado é um registro humano; ele não encerra um defeito nem uma ordem de serviço.

## Quem usa
- **Chefe de máquinas / Superintendente técnico:** para vincular relatórios ao histórico de equipamentos e de ordens de serviço, acompanhar o que falta e verificar relatórios de que não foram autores (13.03 §5).
- **Engenheiro:** para registrar um relatório como rascunho após um serviço e enviá-lo para verificação (13.03 §6).

## Antes de começar
- Se nenhuma embarcação estiver selecionada, a página informa **Escolha uma embarcação.** e lista as embarcações do seu escopo. Selecione a embarcação correta.
- Você precisa de acesso de leitura de relatórios de serviço e de equipamentos para ver os relatórios. O botão **Novo relatório** só aparece com acesso de escrita e enquanto você estiver online.
- Tenha à mão os IDs do equipamento, da ordem de serviço, dos defeitos e dos arquivos de evidência. Os campos de ordem de serviço, defeito e evidência só aparecem se você puder ler esses registros.

## Passo a passo
1. Abra um relatório na lista **Relatórios**, ou inicie um novo rascunho com **Novo relatório**.
2. Preencha **N.º do relatório**, **Prestador de serviço**, **Engenheiro**, **Data do serviço**, **Motivo e escopo** e **Trabalho realizado**.
3. Se necessário, use **Adicionar peça** e **Adicionar medição** para inserir as peças usadas e as medições. Se você não souber o resultado de uma medição, deixe-o como **Desconhecido**.
4. Em **Vínculos**, insira o **ID do equipamento**, o **ID da ordem de serviço** e os IDs dos defeitos e arquivos de evidência vinculados (um por linha).
5. Salve com **Salvar rascunho**. Se o relatório mostrar uma lista **Pendências antes do envio**, conclua primeiro esses itens.
6. Envie o relatório com **Enviar para verificação**.
7. Em um relatório enviado do qual você não é autor, escreva um **Motivo** de pelo menos 10 caracteres e use **Registrar verificação** ou **Devolver ao autor**.
8. Para alterar um relatório verificado, escreva um motivo e use **Corrigir (nova revisão)**. Todas as revisões permanecem em **Histórico imutável**.

## O que significam os estados
- **Rascunho / Enviado / Devolvido:** o ciclo de vida do relatório. Enviar não é verificar.
- **Verificado (decisão humana registrada) / Verificado por um não autor:** uma pessoa autorizada que não é autora nem remetente do relatório registrou uma verificação. Concluído e Verificado não são a mesma coisa.
- **Aguardando verificação:** o relatório foi enviado e ainda não está verificado.
- **Uma revisão anterior foi verificada; esta não:** após uma correção, a nova revisão precisa ser verificada novamente.
- **revisão atual / alterado desde o vínculo / indisponível:** o estado de um equipamento, ordem de serviço ou defeito vinculado em comparação com a revisão que foi vinculada.
- **Transferência aceita, não evidência verificada:** um arquivo anexado é apenas uma transferência aceita.
- Um resultado de medição **Desconhecido** não significa "Dentro dos limites".

## O que você não pode fazer / cuidado
- Você não pode verificar um relatório do qual é autor ou que você enviou; o servidor o rejeita.
- Um relatório verificado não encerra um defeito nem uma ordem de serviço, e não mostra que o equipamento é seguro.
- Os originais das evidências não são alterados; uma correção abre uma nova revisão e a revisão antiga permanece no histórico (13.03 §1).
- Nada é registrado enquanto você estiver offline. Quando a conexão voltar, **Repetir o mesmo comando** envia o mesmo comando novamente.

## Se algo der errado
- **Faltam dados ou registros obrigatórios:** conclua a lista de pendências e salve novamente.
- **Outra pessoa alterou este registro:** use **Recarregar** para abrir a versão mais recente e salve novamente.
- **Você não tem a permissão do servidor para esta ação ou registro:** verifique sua função e o contexto da embarcação; se necessário, entre em contato com o administrador do seu tenant (13.03 §10).
- **Um relatório não pode ser verificado:** confira os registros vinculados e os arquivos de evidência; converse com um verificador autorizado (13.03 §10).
- **Não foi possível carregar o registro:** envie ao suporte a tela, o horário e os passos (13.03 §10).

## Páginas relacionadas
