---
key: pilot-program
title: Programa piloto: semanas e relatório de valor
roles: [Product Owner, Tenant Admin]
permissions: [pilot.program.read, pilot.program.manage, pilot.program.record, pilot.program.triage, pilot.program.decide]
related: [pilot-gate, role-uat, audit-ready]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Programa piloto reúne em um só lugar as semanas do piloto de um navio (e, opcionalmente, de um rebocador), os itens de verificação de cada semana, os problemas do piloto, o feedback diário e o relatório de valor. Ela responde: "Os itens de cada semana do piloto foram atendidos por registros reais, e como os valores medidos mudaram em relação à linha de base?" Vale o aviso no topo: "Software cannot complete a pilot. A checklist item is satisfied only by a link you make to a real record that the server verifies; a missing baseline stays Unknown; only the appointed Product Owner closes a week or records the program decision."

## Quem usa
- **Product Owner:** fecha as semanas e registra a decisão do programa (Continuar, Corrigir, Parar, Expandir). Isso exige uma designação vigente de Product Owner.
- **Equipe do piloto:** vincula registros aos itens de verificação, relata problemas, registra feedback e medições.
- **Responsável pela triagem:** faz a triagem dos problemas e os resolve.
- O 13.03 não define uma função separada para esta página; aplicam-se os princípios de administração do tenant (13.03 §9).

## Antes de começar
- A visualização exige leitura em todo o tenant e leitura do programa piloto. Criar programas, registrar, fazer triagem e decidir exigem, cada um, sua própria permissão; fechamentos de semana e decisões também exigem uma designação de Product Owner.
- Os registros que você vincula (por exemplo, um registro de certificado, de tripulação ou de ordem de serviço) já devem existir em suas próprias páginas.
- No feedback, nunca insira nomes, endereços de e-mail, números de telefone ou de documentos; use apenas um pseudônimo e a função.
- O registro exige uma conexão ativa.

## Passo a passo
1. Abra um programa em **Programas piloto**. Para um novo programa, preencha **Nome do programa**, **Navio**, **Rebocador (opcional)**, **Data de início da semana 1** e **Fuso horário do calendário (IANA)** em **Criar programa piloto** e pressione **Registrar**.
2. Em cada seção **Semana**, revise as colunas **Item de verificação**, **Critério (fonte)**, **Área**, **Estado** e **Registros vinculados**.
3. Para atender a um item, insira o **Item de verificação**, o **Tipo de registro** e o **Identificador do registro** em **Vincular um registro** e pressione **Vincular um registro**. Remova um vínculo errado com **Retirar vínculo** depois de escrever um **Motivo da retirada**.
4. Em **Problemas do piloto**, use **Relatar problema** para inserir a área, a **Categoria**, a gravidade e o **Resumo do problema**. O responsável pela triagem usa **Triagem** com uma **Nota de triagem** e depois resolve o problema.
5. Em **Feedback diário**, use **Registrar feedback** para inserir um **Resumo** e a **Percepção**.
6. Em **Relatório de valor**, use **Registrar medição** para inserir a **Métrica**, a **Fase** (linha de base ou período do piloto), o **Valor**, o **Tamanho da amostra**, **Período de**, **Período até**, a **Referência de evidência** e **Método e amostra**.
7. Se você for o Product Owner, registre o resultado e a justificativa em **Fechar semana**. Ao final do programa, preencha a decisão, a **Designação** e a **Justificativa** em **Registrar decisão do programa** e pressione **Registrar**.

## O que significam os estados
- **Vinculado e verificado:** um registro real verificado pelo servidor está vinculado ao item. Itens com **Vínculo não é mais válido** e **Ausente** não estão atendidos.
- **Conclusão necessária:** o registro vinculado deve estar em seu estado concluído. "Audit trace proves that a person wrote the record; it does not prove the record or flow is completed."
- **Pronta para fechamento humano / Não pronta:** uma recomendação para a semana; somente o Product Owner a fecha como **Aceita** ou **Não aceita**.
- **Triado no mesmo dia / Triado com atraso / Triagem prevista para hoje / Atrasada:** o prazo de triagem dos problemas P0 em relação ao dia do calendário do programa. Para problemas com **Sem meta de mesmo dia na fonte**, o sistema não inventa nenhum prazo.
- **Medido / Desconhecido:** quando falta um valor de linha de base ou do piloto, o relatório permanece **Desconhecido**; ele nunca é contado como zero nem como melhoria. Valores **Não comparável** não são comparados.
- **Telemetria (declarada, não verificada):** um valor de telemetria declarado.
- **Expansão possível; decisão humana pendente / Correção recomendada:** uma recomendação do programa, não uma decisão.

## O que você não pode fazer / cuidado
- "Accepted is possible only when every item is verified and no P0 triage is open, pending or late."
- "Expand is possible only when every week is accepted and values, UAT, defects and P0 are clear."
- Se você for autor de evidências nas quais uma semana ou decisão se baseia (vínculos, feedback, medições), você não pode fechar essa semana nem registrar essa decisão.
- Nenhum fechamento de semana ou decisão pode ser registrado para um programa fictício de fixture.
- O sistema nunca conclui um piloto, nunca fecha uma semana e nunca fabrica decisões ou aprovações.

## Se algo der errado
- **Você não tem designação vigente de Product Owner; fechamentos e decisões são somente leitura.** Entre em contato com o Tenant Admin para obter a designação (13.03 §10).
- **A coleta de feedback fica indisponível até que um operador configure a política de retenção.** Peça ao administrador do sistema que configure a política de retenção.
- **O registro mudou ou já foi usado. Recarregue.** Abra o estado atual com **Recarregar**.
- **O resultado é incerto. Tentar novamente envia o mesmo comando.** Use **Tentar novamente** para reenviar o mesmo comando com segurança.
- **Recusado pelas regras.** Por exemplo, o registro vinculado pode não existir, estar fora do escopo ou não estar concluído.
- **Não permitido.** Você é autor de vínculos, feedback ou medições nos quais este fechamento de semana ou decisão se baseia; outra pessoa deve registrá-lo.
- Uma semana que ainda não começou não pode ser fechada (aparece como "O registro mudou ou já foi usado. Recarregue.").
- **Você não tem acesso a estes registros.** Verifique sua função e o escopo do tenant (13.03 §10).

## Páginas relacionadas
