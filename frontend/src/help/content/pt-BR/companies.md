---
key: companies
title: Empresas
roles: [Tenant Admin, Shore Operations]
permissions: [canReadCompanies, canCreate]
related: [fleet, audit, today]
screenshots: [1, 2, 3]
reviewed: false
---
## Para que serve esta página
A página Empresas lista os **registros de empresas** do seu espaço de trabalho que você está autorizado a ver. Ela responde: "Quais empresas estão definidas neste espaço de trabalho e quais são seu código e seu nome?" Os registros de navios e rebocadores pertencem a uma empresa, por isso a empresa correta é o primeiro passo para escolher o seu contexto.

## Quem usa
- **Tenant Admin:** para criar e editar registros de empresas com o menor privilégio necessário (13.03 §9).
- **Operações em terra:** para encontrar o contexto de empresa correto.

## Antes de começar
- Você precisa de permissão para ler empresas para ver esta página. Sem ela, é exibida a mensagem "You do not have access to this workspace.".
- Criar um registro exige permissão de criação. Se você pode editar um registro depende da permissão que o servidor concede para aquele registro.

## Passo a passo
1. Veja o código e o nome de cada empresa na lista. Use **Anterior** / **Próximo** para outras páginas.
2. Para pesquisar, digite em **Buscar por código ou nome** e pressione **Pesquisar**.
3. Selecione o nome de uma empresa para abrir a seção **Detalhes**.
4. Para uma nova empresa, pressione **Novo registro**, informe o código e o nome e pressione **Salvar**. Todos os campos são obrigatórios.
5. Para alterar um registro existente, use **Editar** nos detalhes.
6. Abra **Mostrar registro da pessoa jurídica** para ver as informações da pessoa jurídica; esta seção é somente leitura.

## O que significam os estados
- **Nenhum registro encontrado.:** nenhum registro foi retornado para a sua pesquisa ou para o seu escopo de permissão.
- **Pessoa jurídica: Rascunho – incompleto, não ativo / Ativa / Inativa:** o estado do registro da pessoa jurídica. Rascunho significa que ainda faltam valores antes da ativação.
- **Desconhecido:** o valor não está no registro. Desconhecido não significa "Não" nem "válido".
- **Salvo.:** o registro foi gravado no servidor. Este registro não cria nenhuma aprovação operacional.

## O que você não pode fazer / cuidado
- Criar ou editar um registro de empresa não é uma aprovação operacional, de conformidade nem de segurança.
- Os valores da pessoa jurídica não são alterados nesta página; eles mudam apenas por meio de comandos de RH auditados. Os identificadores de registro são mostrados mascarados.
- O mesmo código não pode ser usado por duas empresas.
- Se você sair da página com um rascunho não salvo, será perguntado se deseja descartá-lo ou continuar editando.

## Se algo der errado
- **Este registro foi alterado em outra operação:** seu rascunho é mantido. Descarte o rascunho para carregar o registro atual e refaça sua alteração.
- **Este código já está em uso:** informe um código diferente.
- **Não foi possível confirmar o resultado:** os campos permanecem bloqueados até que o resultado seja conhecido; repita com segurança o mesmo comando.
- **Sua permissão pode ter mudado / Sem acesso:** verifique seu papel e o contexto do espaço de trabalho; contate o administrador do seu tenant, se necessário (13.03 §10).
- **O serviço está indisponível:** use "Tentar novamente"; se o problema continuar, informe-o com a referência de suporte.

## Páginas relacionadas
