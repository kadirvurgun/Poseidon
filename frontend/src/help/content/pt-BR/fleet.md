---
key: fleet
title: Frota
roles: [Master, Tug Master, Chief Engineer, Shore Operations, Tenant Admin]
permissions: [directory.read, directory.write]
related: [today, companies, certificate-command-center, data-trust-center, departure-signoff]
screenshots: [1, 2, 3, 4, 5, 6, 7]
reviewed: false
---
## Para que serve esta página
"Frota" é a **lista de navios e rebocadores do seu escopo**. Ela responde: "A quais embarcações eu tenho acesso, e onde estão o registro, o perfil regulatório, o passaporte de conformidade e os anexos de uma embarcação?" A página mostra o registro da embarcação e as informações registradas; a prontidão operacional é avaliada separadamente.

## Quem usa
- **Comandante / Comandante do rebocador / Chefe de máquinas:** para selecionar a própria embarcação e ir aos seus espaços de trabalho (equipamentos e manutenção, lista de tripulantes) (13.03 §2, §3, §5).
- **Operações em terra:** para encontrar embarcações na frota e revisar seus registros.
- **Tenant Admin:** para criar e editar registros de embarcações (13.03 §9).

## Antes de começar
- Na barra de contexto superior, verifique se a **empresa correta** está selecionada (13.03 §1). A lista mostra apenas as embarcações do seu escopo.
- Você precisa de acesso de leitura para ver a lista. "Novo registro" e "Editar" aparecem apenas para embarcações às quais você tem acesso de escrita.

## Passo a passo
1. Nos cartões de resumo no topo, veja o número de embarcações listadas, navios, rebocadores e registros editáveis nesta página. **As contagens abrangem apenas esta página**.
2. Digite em **Buscar por código ou nome**, escolha "Navio" ou "Rebocador" em **Tipo de navio**, se necessário, e pressione **Pesquisar**.
3. Pressione o nome de uma embarcação na tabela. **Detalhes** mostra seu código, nome e tipo; a embarcação selecionada aparece como "Embarcação atual".
4. Abra o perfil da embarcação com **Perfil regulatório**, ou os dados de integração com **Integração de navio piloto**.
5. Use **Passaporte digital de conformidade** para revisar os requisitos registrados, as fontes, a revisão de aplicabilidade e os impedimentos não resolvidos. Use **Voltar ao navio** para retornar.
6. Vá aos espaços de trabalho da embarcação com **Importação controlada**, **Equipamentos e manutenção** ou **Lista de tripulantes**.
7. Na seção inferior, alterne entre **Vessel attachments** e **Rascunhos de documentos**.
8. Se você tiver acesso, adicione uma embarcação com **Novo registro** ou altere-a com **Editar** e salve. Use **Trocar navio** para outra embarcação.

## O que significam os estados
- **Somente leitura / Registros editáveis:** o seu acesso atual nesta página. "Editar" não é mostrado em um registro somente leitura.
- **Apenas esta página. A prontidão operacional é avaliada separadamente.:** o gráfico de composição das embarcações não fornece nenhuma informação de prontidão.
- **Revisão necessária — atualidade não verificada:** as informações do passaporte são lidas de fontes registradas e não são verificadas. O passaporte não é uma aprovação de conformidade.
- **Nenhuma linha de requisito atual retornada:** isso não estabelece um conjunto vazio de obrigações.
- **Desconhecido / Indisponível:** o valor está ausente na fonte ou não pôde ser lido. Isso não significa "Em conformidade" nem "Pronto".

## O que você não pode fazer / cuidado
- Esta página nunca diz que uma embarcação está pronta para operação ou partida. A decisão de partida cabe ao Comandante e a decisão de reboque ao Comandante do rebocador.
- Um administrador genérico não pode substituir a admissão qualificada de fontes nem uma decisão humana autorizada.
- Se você sair com alterações não salvas, será solicitada uma confirmação; as alterações são mantidas até que você salve ou saia explicitamente.
- Os dados mostrados offline não são verificados; reconecte-se e tente novamente.

## Se algo der errado
- **Você não tem acesso a este espaço de trabalho.:** verifique seu papel e o contexto da embarcação; contate o administrador do seu tenant, se necessário (13.03 §10).
- **Nenhum registro encontrado.:** altere sua pesquisa ou o filtro de tipo de embarcação.
- **Entre para continuar:** sua sessão terminou; use "Entrar" novamente.
- **O serviço está indisponível:** use "Tentar novamente"; se persistir, informe ao suporte com uma captura de tela, o horário e seus passos.

## Páginas relacionadas
