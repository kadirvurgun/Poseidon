---
key: regulatory-review
title: Revisão regulatória por SME
roles: [Regulatory / Assurance, DPA / HSEQ]
permissions: [regulatory.review.read, regulatory.review.manage, regulatory.review.verdict]
related: [sources, requirements, audit-ready, audit]
screenshots: []
reviewed: false
---
## Para que serve esta página
A Revisão regulatória por SME registra campanhas nas quais um especialista humano revisa os itens de fonte e de aplicabilidade de um navio ou rebocador. Ela responde: "Quais itens de fonte e de requisito desta embarcação foram confirmados por um revisor autorizado, e quais ainda estão abertos e rastreados?" É apenas um registro de revisão humana: o sistema não aprova nenhum requisito, não ativa nenhuma regra ou permissão e não declara nenhuma aprovação de classe ou de tipo (13.03 §8).

## Quem usa
- **Usuário de Regulatório / Garantia:** abre campanhas de revisão, registra vereditos sobre os itens e encerra a campanha (13.03 §8).
- **Revisor designado (SME):** confirma ou rastreia itens dentro da sua própria autoridade de fonte.
- **DPA / HSEQ:** acompanha os itens abertos e sem rastreio (13.03 §4).

## Antes de começar
- A visualização exige leitura da revisão regulatória. Abrir e encerrar campanhas exige gerenciamento; registrar vereditos exige a permissão de veredito e uma designação de revisor vigente para este escopo.
- Escolha **Navio** ou **Rebocador** nas abas **Escopo da revisão** e depois abra a embarcação correta em **Escolha uma embarcação** (13.03 §1).
- As fontes devem estar registradas como oficiais e versionadas na página [Rascunhos de fontes](/app/sources) (13.03 §8).
- Offline, os dados podem estar desatualizados e os comandos ficam desativados.

## Passo a passo
1. Escolha o escopo e a embarcação. Se não houver campanha, pressione **Abrir campanha de revisão**.
2. Em **Matriz de responsabilidade das fontes**, revise a categoria de cada autoridade de fonte, as contagens de abertos e confirmados e as funções responsáveis.
3. Selecione um item em **Itens de revisão**.
4. No formulário **Registrar veredito**, preencha **Designação de revisor**, **Veredito**, **Autoridade da fonte** e **Função responsável**. Se o item não tiver fonte registrada, insira **Referência da fonte oficial** e **Versão / edição da fonte**. Para um item que permanece aberto, insira um **ID da ocorrência**.
5. Escreva um motivo de 10 a 2000 caracteres em **Nota do revisor** e pressione **Registrar**.
6. Quando todos os itens estiverem resolvidos, escolha um **Resultado do encerramento** em **Encerrar campanha**, escreva o **Motivo do encerramento** e pressione **Encerrar campanha**.

## O que significam os estados
- Campanha: **Aberta**, **Em revisão**, **Encerrada com itens abertos**, **Encerrada**.
- Veredito: **Confirmado**, **Não aplicável (confirmado)**, **Revisão necessária**, **Ambíguo**. Quando dados críticos são desconhecidos, o item permanece em **Revisão necessária**; um item desconhecido nunca é contado como "Não aplicável".
- **Candidato (não verificado):** a fonte ainda não está vinculada a uma fonte oficial verificada.
- **Desatualizado — referência alterada** e **Perfil do navio alterado:** a fonte ou o perfil da embarcação mudou depois do veredito; o item deve ser revisado novamente.
- **Itens abertos sem rastreio:** itens abertos sem uma ocorrência rastreada. Enquanto essa contagem for maior que zero, a campanha não pode ser encerrada.
- As categorias **Lei estatutária**, **Regra de classe**, **SMS da empresa** e **Orientação — não é lei** nunca substituem umas às outras (13.03 §8).

## O que você não pode fazer / cuidado
- A pessoa que abriu uma campanha não pode registrar vereditos nela; sem uma designação de revisor, os vereditos ficam somente leitura.
- Um revisor cuja qualificação não foi verificada pode rastrear itens abertos, mas não pode confirmar. Um item desatualizado não pode ser confirmado.
- A confirmação exige uma fonte registrada ou uma referência de fonte oficial com sua edição. Não deixe nenhum registro órfão ou sem fonte na cadeia Requisito → Controle → Evidência → Verificação (13.03 §8).
- **Encerrada** exige que todos os itens estejam confirmados; caso contrário, encerre com itens abertos, cada um deles rastreado.
- Não declare aprovação de classe ou de tipo na nota. No escopo de rebocador, nenhum limite de reboque sem uma fonte controlada é um padrão legal.
- Os vereditos são registrados apenas por usuários humanos; contas de IA e de automação não podem registrá-los.

## Se algo der errado
- **O registro mudou. Recarregue e revise antes de continuar.** Pressione **Recarregar**.
- **O resultado é incerto. Tentar novamente envia o mesmo comando.** Use **Tentar novamente**.
- **As regras de revisão recusaram este comando.** Verifique se o item está desatualizado, a referência da fonte e a ocorrência rastreada.
- **Você não tem acesso à revisão regulatória deste navio.** ou **Você não está autorizado para esta ação.** Verifique sua função e seu escopo; se necessário, entre em contato com o Tenant Admin (13.03 §10).
- Se a aplicabilidade parecer inesperada, verifique a fonte e as entradas de contexto e converse com o responsável pelo controle ou com o SME (13.03 §10).

## Páginas relacionadas
