---
key: audit-ready
title: Rastreabilidade pronta para auditoria
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [audit.read, human.trace.read]
related: [requirements, sources, audit]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Rastreabilidade pronta para auditoria mostra, etapa por etapa, as cadeias de rastreabilidade atribuídas a você para uma embarcação: Requisito → controle → evidência → verificação → decisão, com estados, carimbos de data/hora e localizadores de fonte declarados. Ela responde: "Em qual requisito esta evidência se baseia, quem a verificou e qual decisão foi registrada?" Uma cadeia registrada é um registro humano, não uma aprovação nem uma declaração de prontidão (13.03 §4, §8).

## Quem usa
- **Usuário de Regulatório / Garantia:** verifica se nenhum registro órfão ficou na cadeia Requisito→Controle→Evidência→Verificação (13.03 §8).
- **DPA / HSEQ:** apresenta a trilha Constatação→Ação→Verificação com suas evidências ao se preparar para auditoria, PSC ou inspeção (13.03 §4).

## Antes de começar
- A página exige leitura de auditoria e leitura de rastreabilidade humana, além de uma designação vigente de leitor de rastreabilidade para aquela embarcação. A permissão sozinha não basta; sem a designação, nenhuma cadeia é listada.
- Escolha a embarcação correta. A lista mostra apenas as cadeias atribuídas a você.
- A página é carregada por uma conexão ativa. Offline, os dados mostrados podem estar desatualizados.

## Passo a passo
1. Escolha uma embarcação em **Embarcação** (**Selecione uma embarcação**).
2. Abra uma cadeia em **Cadeias atribuídas**.
3. Em **Etapas da rastreabilidade**, expanda cada etapa (Requisito, Controle, Evidência, Verificação, Decisão) e revise **Estado**, **Registrado em**, **Revisão**, **Hash do conteúdo** e **Localizador da fonte (declarado, não verificado)**.
4. Se houver **Itens em aberto** listados, repasse os códigos ao responsável pelo registro em questão.
5. Quando houver cadeias atribuídas legíveis, compile um pacote em **Pacote de auditoria** com **Compilar novo pacote**, abra-o em **Pacotes compilados** e use **Exportar JSON**, se necessário.
6. Pressione **Tentar novamente** no cabeçalho para atualizar os dados.

## O que significam os estados
- **Cadeia registrada — apenas registro humano, não uma aprovação:** todas as etapas estão registradas. Isso não declara a embarcação ou a operação pronta ou em conformidade.
- **Revisão necessária:** pelo menos uma etapa está ausente, desconhecida ou indisponível. Uma entrada desconhecida ou ilegível nunca é mostrada como completa.
- **Não registrado:** não existe registro para aquela etapa; por exemplo, nenhuma verificação ou decisão foi registrada ainda.
- **Indisponível / UNKNOWN:** o registro não pode ser lido agora; seu conteúdo não é presumido.
- **Desatualizado: uma entrada foi substituída ou expirou; revisão necessária.** Uma evidência ou fonte mudou depois; revise a cadeia novamente.
- **Localizador não informado:** nenhum localizador de fonte foi informado; um localizador é uma declaração, não um fato verificado.
- Evidência verificada não significa que a ação foi eficaz: Verificado ≠ Eficaz (13.03 §4).
- **Pacote de auditoria:** "System compilation; not an official record". Itens fora das suas designações são apenas contados, não mostrados; itens **Substituído (mantido para rastreabilidade)** nunca são tratados como vigentes.

## O que você não pode fazer / cuidado
- Você não pode editar registros, verificar nem decidir nesta página; ela é somente leitura. A verificação e as decisões são registradas pela pessoa designada em suas próprias páginas.
- Você não pode ver cadeias que não foram atribuídas a você; cadeias ocultas nunca são nomeadas, e cadeias atribuídas indisponíveis são mostradas apenas como contagem.
- Hashes de integridade e de contexto não são autoridade nem aprovação.

## Se algo der errado
- **Você não tem autorização para esta visualização.** Verifique seu papel, a embarcação e o contexto de designação; contate o Tenant Admin (13.03 §10).
- **Nenhuma cadeia atribuída a você para esta embarcação.** Verifique se selecionou a embarcação correta; pergunte ao responsável se é necessária uma designação.
- **Não foi possível carregar a rastreabilidade.** Pressione **Tentar novamente**.
- **Cadeia não encontrada ou não mais visível para você.** Sua designação pode ter terminado ou o registro saiu do seu escopo.
- Se um registro não puder ser verificado, confira suas evidências e sua procedência e contate o verificador, se necessário (13.03 §10).

## Páginas relacionadas
