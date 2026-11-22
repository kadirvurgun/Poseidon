---
key: pilot-gate
title: Revisão go/no-go do piloto
roles: [Product Owner, Tenant Admin]
permissions: [pilot.gate.read, pilot.gate.support.record, pilot.gate.decide]
related: [role-uat, pilot-program, audit-ready]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Revisão go/no-go do piloto mostra em um só lugar o estado dos portões exigidos para um piloto, a contagem de P0 abertos e a prontidão do suporte, e registra a decisão go/no-go do Product Owner designado. Ela responde: "Todos os portões do piloto foram atendidos com evidências, e uma decisão humana foi registrada?" Vale o aviso no topo: "Advisory review only. Unknown or deferred gates and an unknown or open P0 recommend NO-GO. Only the appointed Product Owner records a decision."

## Quem usa
- **Product Owner:** lê o resultado da revisão e registra a decisão go/no-go com uma justificativa. Decidir exige uma designação vigente de Product Owner.
- **Support / Tenant Admin:** marca os itens de prontidão de suporte e de plantão com uma referência de evidência.
- O 13.03 não define uma função separada para esta página; aplicam-se os princípios de administração do tenant (privilégio mínimo, publicação controlada) (13.03 §9).

## Antes de começar
- A visualização exige leitura em todo o tenant e leitura dos portões do piloto. Marcar um item de suporte exige permissão de registro de suporte; decidir exige permissão de decisão mais uma designação vigente de Product Owner provisionada no servidor. A permissão sozinha não basta.
- Somente uma conta humana pode marcar o suporte ou decidir; a designação deve corresponder à sua função ativa e estar dentro do período de validade.
- As entradas dos portões vêm de outras páginas; por exemplo, os portões Ship UAT e Tug UAT são calculados a partir de execuções atestadas na página **Registros de UAT por função**.
- O registro exige uma conexão ativa; ele fica desativado offline.

## Passo a passo
1. Em **Revisão do portão do piloto**, leia a recomendação (**Recomendação consultiva**), a contagem de **P0 abertos** e a tabela de portões: **Portão**, **Estado**, **Fonte**, **Motivos**.
2. Em **Prontidão de suporte e plantão**, escolha um **Estado** para cada item, insira uma **Referência de evidência** para **Pronto**, escreva uma **Nota** e pressione **Marcar**.
3. Se você for o Product Owner, escolha a decisão em **Registrar decisão go/no-go** (**NO-GO**, **GO condicional**, **GO**), escolha sua designação de Product Owner em **Designação** e escreva a **Justificativa**. Para **GO condicional**, preencha **Condições (uma por linha)**.
4. Pressione **Registrar**. A decisão aparece em **Decisões do Product Owner** com seu número de sequência.
5. Pressione **Atualizar** para recarregar os dados.

## O que significam os estados
- **Evidence threshold met (PASS):** a fonte do portão atende ao limite de evidência. Isso não é uma aprovação; quem decide é um ser humano.
- **Fail (FAIL)**, **Unknown (UNKNOWN)**, **Deferred (DEFERRED):** o portão não foi atendido. Um portão desconhecido ou adiado nunca conta como aprovado e recomenda NO-GO.
- **NO-GO recomendado:** pelo menos um portão não está em PASS, ou a contagem de P0 abertos é desconhecida ou maior que zero.
- **Elegível para GO; decisão humana pendente:** todos os portões estão em PASS e a contagem de P0 abertos é zero. O sistema nunca decide GO; somente o Product Owner o registra.
- **Não marcado / Pronto / Não pronto:** o último estado marcado de um item de suporte.
- **Atual / Desatualizada: revisão mudou:** uma decisão fica vinculada à revisão vigente no momento em que foi tomada. Se a revisão mudou depois, a decisão aparece como desatualizada e precisa ser reconsiderada.

## O que você não pode fazer / cuidado
- "GO is possible only when every gate is PASS and open P0 is zero." Caso contrário, a opção GO fica desativada.
- O GO condicional exige o mesmo estado que o GO (todos os portões em PASS, P0 abertos sabidamente igual a zero) e pelo menos uma condição. Ele não pode ser registrado enquanto algum portão estiver reprovado, desconhecido ou adiado, nem enquanto houver P0 aberto ou desconhecido. O NO-GO sempre pode ser registrado, sem condições.
- O sistema, a IA ou a automação nunca tomam a decisão do piloto e nunca fabricam portões ou aprovações. Os portões e as decisões do piloto são registros humanos.
- Não é possível alterar uma decisão registrada; uma nova decisão é adicionada com um novo número de sequência.

## Se algo der errado
- **Você não possui designação vigente de Product Owner. As decisões são somente leitura.** As designações são provisionadas no servidor; entre em contato com o Tenant Admin (13.03 §10).
- **Você não tem acesso a estes registros.** Verifique sua função e o escopo do tenant.
- **O registro mudou ou já foi usado. Recarregue.** A revisão mudou enquanto você decidia; abra a revisão atual com **Recarregar**. Isso também aparece quando os estados dos portões mudaram e a decisão deixou de ser permitida.
- **O resultado é incerto. Tentar novamente envia o mesmo comando.** Use **Tentar novamente** para reenviar o mesmo comando com segurança.
- **Recusado pelas regras.** Um portão está adiado.
- **Não permitido.** Sua designação não está vigente ou sua conta não é uma conta humana.
- **O serviço está indisponível. Tente novamente.** Se persistir, informe ao suporte o ID de correlação, o horário e os passos (13.03 §10).

## Páginas relacionadas
