---
key: defects
title: Defeitos e salvaguardas
roles: [Chief Engineer, Technical Superintendent, Master, Crew / OOW / Engineer]
permissions: [defect.read, equipment.read, defect.write, defect.verify, defect.effectiveness, defect.close]
related: [pms-command-center, work-order-center, operational-downtime, service-engineer-report, drydock-planning]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página de defeitos e medidas temporárias contém os **defeitos, restrições e medidas temporárias declaradas registrados** da embarcação selecionada. Ela responde: "Quão crítico é este defeito, qual salvaguarda ou restrição operacional está registrada, a ação corretiva foi verificada e o que ainda impede o encerramento?" Cada etapa é um registro humano; a página não concede nenhuma autoridade de segurança ou de operação.

## Quem usa
- **Chefe de máquinas / Superintendente Técnico:** para registrar a salvaguarda ou a restrição e o impacto de um defeito crítico na prontidão, e para acompanhar a ação corretiva (13.03 §5).
- **Comandante:** para ver o impacto de um defeito em termos de restrição operacional (13.03 §2).
- **Engenheiro / tripulação:** para declarar um defeito que encontraram (13.03 §6).

## Antes de começar
- A leitura exige `defect.read` e `equipment.read`. Declarações e registros do ciclo de vida exigem `defect.write`.
- A verificação exige `defect.verify`, a eficácia `defect.effectiveness` e o encerramento `defect.close`, cada um com um usuário humano autenticado.
- Escolha a embarcação correta em **Selecione um navio ou rebocador**.

## Passo a passo
1. Escolha a embarcação. Se necessário, filtre **Declarações registradas** com **Filtrar observações de vencimento retornadas** e abra uma declaração.
2. Para um novo defeito, pressione **Nova declaração de defeito**, preencha os campos e pressione **Salvar declaração não avaliada**. Rascunhos incompletos podem ser salvos.
3. Em uma declaração salva, pressione **Iniciar ciclo de vida** na seção **Ciclo de vida do defeito**.
4. Use **Registrar avaliação** para escolher a criticidade e escrever a declaração (no mínimo 10 caracteres).
5. Use **Registrar salvaguarda ou restrição** e escolha **Salvaguarda** ou **Restrição operacional** como **Tipo de medida**. Para uma medida temporária, informe o vencimento (UTC).
6. Use **Registrar ação corretiva** para descrever o trabalho realizado.
7. Uma pessoa autorizada que não registrou nenhuma ação corretiva registra **Registrar verificação** e depois **Registrar eficácia**.
8. Quando não restarem **Bloqueios de encerramento**, uma pessoa autorizada que também não registrou nenhuma ação corretiva registra **Encerrar e retornar ao serviço**. Os registros aparecem em **Registros do ciclo de vida**.

## O que significam os estados
- **Detecção / Avaliação / Salvaguarda / Ação / Verificação / Eficácia:** a etapa do ciclo de vida.
- **Encerrado (decisão humana registrada):** o encerramento é uma decisão humana; não é um resultado verde de "corrigido".
- **Regra crítica aplicável:** um defeito crítico não pode ser encerrado sem uma salvaguarda.
- **Ativa / Vencida — bloqueante / Suspensa / Encerrada no fechamento:** o estado de uma salvaguarda ou restrição. Uma medida temporária vencida deve ser renovada ou suspensa.
- **Defeito crítico sem salvaguarda ativa / Criticidade desconhecida e sem salvaguarda:** alertas. Esses defeitos também aparecem em **Alertas de condição** na [central de comando do PMS](/app/pms-command-center).
- **Desconhecido:** a criticidade não foi avaliada. Desconhecido não significa "Baixa" nem "Não crítico".

## O que você não pode fazer / cuidado
- Se você registrou a ação corretiva, não pode registrar a verificação, a eficácia nem o retorno ao serviço; eles exigem autoridade própria e uma pessoa diferente.
- Verificado, Eficaz e Encerrado são registros humanos separados. Uma etapa ou medida declarada não autoriza o serviço, não verifica a eficácia nem permite o encerramento.
- Se a declaração mudar após a verificação, o encerramento é bloqueado e o registro volta para revisão.
- A criticidade do ativo não é copiada como a avaliação deste defeito.
- Offline, as fontes atuais de defeitos ficam indisponíveis; não há armazenamento offline.

## Se algo der errado
- **Outra pessoa alterou este registro:** use **Recarregar** para ler o registro atual e registre novamente.
- **O resultado do salvamento é desconhecido:** **Repetir o mesmo comando** envia o mesmo comando.
- **Você não tem a permissão do servidor para esta ação ou registro.:** verifique seu papel e o contexto da embarcação; contate o administrador do seu tenant, se necessário (13.03 §10).
- **Bloqueios de encerramento:** complete o registro que falta na lista. Se um registro não puder ser verificado, confira a evidência com o verificador (13.03 §10).

## Páginas relacionadas
