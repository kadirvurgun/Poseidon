---
key: industry-assurance
title: Garantia setorial (SIRE 2.0)
roles: [DPA / HSEQ, Regulatory / Assurance, Master]
permissions: [assurance.inspection.read, assurance.inspection.import, assurance.import.accept, assurance.evidence.write, assurance.finding.link, assurance.mapping.write, file.read, hse.safety_event.read]
related: [safety-events, audit-ready, regulatory-review]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Garantia setorial importa os resultados de inspeção SIRE 2.0 de um navio-tanque a partir de uma pasta de trabalho, submete as alterações à revisão humana, vincula constatações a ações de segurança e registra evidências de campo. Ela responde: "Quais constatações desta inspeção estão em aberto, quais têm uma ação vinculada e onde está a evidência?" As importações são rascunhos: uma pessoa diferente aceita cada item alterado. As constatações permanecem em aberto e não verificadas; esta página nunca as encerra nem as verifica. A cadeia é Constatação → Ação → Verificação, e a verificação e a eficácia são registros humanos separados na ação, em Eventos de segurança e quase acidentes (13.03 §4, §8).

## Quem usa
- **DPA / HSEQ:** vincula constatações de inspeção a ações e acompanha a cadeia Constatação → Ação → Verificação (13.03 §4).
- **Usuário de Regulatório / Garantia:** gerencia mapeamentos de colunas e prepara ou revisa importações (13.03 §8).
- **Comandante e pessoal de bordo:** registram evidências de campo (fotos) para uma constatação.

## Antes de começar
- Escolha uma embarcação. O SIRE 2.0 se aplica apenas a navios-tanque. Se o tipo de navio não estiver declarado no perfil da embarcação, as importações permanecem em **revisão necessária**.
- A leitura exige a permissão de leitura de inspeções. Importar, aceitar uma importação, escrever evidências, vincular constatações e escrever mapeamentos são permissões separadas. Vincular uma ação também exige a permissão de leitura de eventos de segurança; registrar evidências também exige leitura de arquivos.
- O esquema de colunas é sintético; o formato licenciado da OCIMF não é usado.
- Offline, apenas as capturas de fotos são colocadas na fila deste dispositivo; importações e decisões exigem conexão.

## Passo a passo
1. **Escolha uma embarcação** e confira a linha **Aplicabilidade**.
2. Se necessário, em **Mapeamentos de colunas** informe uma **Chave do mapeamento**, escreva uma linha por coluna (cabeçalho=campo) e pressione **Salvar como nova versão**.
3. Em **Preparar importação**, escolha a **Versão do mapeamento**, a **Referência da inspeção**, a **Data da inspeção** e a **Pasta de trabalho (.xlsx)** e pressione **Enviar para revisão**.
4. Outra pessoa, em **Revisão da importação**, escolhe **Aceitar** ou **Manter o atual** para cada item alterado, escreve uma **Justificativa** de no mínimo 10 caracteres e pressiona **Aceitar importação** ou **Rejeitar importação**.
5. Abra uma inspeção na lista **Inspeções**. Revise **Itens** e **Constatações**.
6. Para vincular uma constatação a uma ação, informe o **ID da ação de segurança** e pressione **Vincular ação**.
7. Para evidências de campo, informe **Mídia**, **ID do arquivo limpo**, **Revisão do arquivo** e **Checksum do arquivo (SHA-256)** e pressione **Registrar evidência de campo**. Offline, o botão passa a ser **Salvar neste dispositivo**; quando estiver online, envie com **Enviar agora**.

## O que significam os estados
- **Navio-tanque: aplicável / Não aplicável / Tipo de navio desconhecido: revisão necessária:** com um tipo de navio desconhecido, o resultado permanece em **Revisão necessária**; nunca se presume que não seja aplicável.
- **Aguardando revisão / Aceita / Rejeitada:** o estado da importação.
- **Novo / Alterado / Inalterado / Ausente nesta importação:** o item comparado com a importação aceita anteriormente. Um item ausente nesta importação é **Mantido (nunca excluído)**.
- **Observação negativa / Observação positiva / Sem observação / Desconhecido:** a observação sobre um item.
- **Em aberto / Não verificada:** as constatações aqui sempre aparecem como em aberto e não verificadas.
- **Nenhuma ação vinculada / Ação vinculada:** se a constatação está vinculada a uma ação de segurança. Uma ação vinculada não significa que a constatação está verificada nem que a ação é eficaz.
- **Capturado online / Capturado offline:** como a evidência foi capturada.

## O que você não pode fazer / cuidado
- A pessoa que preparou uma importação não pode revisá-la: "You staged this import; someone else must review it."
- Esta página não encerra nem verifica constatações. Verificado não é Eficaz; a eficácia é uma revisão humana separada na ação.
- Evidências em vídeo ficam indisponíveis até que o operador habilite um tipo de arquivo de vídeo verificado por varredura.
- Um item ausente em uma importação posterior nunca é excluído; o histórico é mantido.

## Se algo der errado
- **A solicitação foi recusada.:** resolva os motivos listados abaixo dela.
- **O registro mudou nesse meio-tempo. Recarregue e revise novamente.** Pressione **Recarregar**.
- **O resultado é desconhecido (conexão perdida).** Use **Repetir a mesma solicitação**.
- **Você não tem autorização para ler registros de garantia setorial.** ou **Você não tem permissão para isso.** Verifique seu papel e o contexto da embarcação; se necessário, contate o administrador do tenant (13.03 §10).
- Se uma evidência não puder ser verificada, confira o ID do arquivo, a revisão e o checksum SHA-256; se necessário, contate o verificador (13.03 §10).

## Páginas relacionadas
