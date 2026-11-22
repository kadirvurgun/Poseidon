---
key: audit
title: Histórico de ações
roles: [DPA / HSEQ, Regulatory / Assurance, Tenant Admin]
permissions: [audit.read]
related: [audit-ready, user-admin, regulatory-review]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Histórico de ações mostra um registro **somente leitura** e atribuído das alterações em registros de negócio dentro do seu escopo autorizado. Ela responde: "Quando este registro mudou, com qual ação e sob qual contexto de ator e papel?" Os eventos de segurança têm acesso restrito separadamente e não aparecem aqui (13.03 §4, §9).

## Quem usa
- **DPA / HSEQ:** rastreia registros de constatação, ação e verificação (13.03 §4).
- **Usuário de Regulatório / Garantia:** verifica o histórico de registros antes de uma auditoria ou revisão (13.03 §8).
- **Tenant Admin:** audita alterações de associação e de permissões e os horários de início e fim de delegações (13.03 §9).

## Antes de começar
- A visualização exige leitura de auditoria. Sem ela, você vê "É necessária permissão de auditoria."
- Somente registros no escopo de tenant e de embarcação para o qual você está autorizado são listados.
- Se estiver procurando um registro específico, tenha em mãos o **ID do navio** ou o **ID do objeto**. Os links de histórico em outras páginas já abrem esta página filtrada.

## Passo a passo
1. Preencha os filtros: **ID do navio**, **ID do objeto**, **Tipo de objeto** e **Ação** (**Todos**, **Criado**, **Atualizado**, **Excluído**).
2. Pressione **Filtrar**. Use **Limpar filtros** para redefinir.
3. Revise as colunas **Horário**, **Objeto** e **Ação** em **Registros autorizados**. Navegue entre as páginas com **Primeira página** e **Próximo**.
4. Pressione o link do objeto em uma linha para abrir **Detalhes do registro**. Leia **Referência do ator**, **Referências dos papéis designados**, **Capacidades confiáveis**, **Motivo**, **Metadados**, **Versão do recurso** e **Correlação**.
5. Se houver evidência de revisão imutável, pressione **Abrir revisão imutável exata** para ver o registro como ele era. Pressione **Fechar** para fechar os detalhes.

## O que significam os estados
- **Criado / Atualizado / Excluído:** o tipo de ação sobre o registro.
- **Registrado:** os metadados foram capturados por completo.
- **Metadados históricos incompletos / Identificador histórico indisponível / Contexto histórico indisponível:** registros mais antigos não guardaram essa informação. Informações ausentes nunca são preenchidas por suposição.
- **Valores restritos:** o valor do campo está oculto por privacidade; apenas o fato de que ele mudou é mostrado.
- **Evidências da revisão imutável estão indisponíveis para este registro.** Não há instantâneo de revisão para este tipo de registro.

## O que você não pode fazer / cuidado
- A página é somente leitura. Uma entrada do histórico de ações não pode ser editada nem excluída.
- Um registro aparecer no histórico não significa que o trabalho foi verificado ou eficaz; Verificado ≠ Eficaz (13.03 §4).
- Esta página registra alterações de negócio. Por si só, ela não é uma auditoria completa das ações dos usuários nem prova de conformidade com a privacidade; os eventos de segurança são mantidos separadamente.
- A lista mostra apenas o escopo para o qual você está autorizado. Registros fora do seu escopo não são listados; não ver um registro não significa que ele nunca foi alterado.

## Se algo der errado
- **É necessária permissão de auditoria.** Verifique seu papel e seu escopo; contate o Tenant Admin, se necessário (13.03 §10).
- **Entre para continuar.** Entre novamente.
- **Histórico indisponível. Verifique os filtros e tente novamente.** Verifique se os identificadores estão completos e corretos e pressione **Tentar novamente**.
- **Nenhum registro autorizado corresponde a estes filtros.** Amplie os filtros.
- Se um erro da aplicação persistir, informe-o ao suporte com o ID de correlação, a tela, o horário e os passos (13.03 §10).

## Páginas relacionadas
