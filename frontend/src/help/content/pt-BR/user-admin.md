---
key: user-admin
title: Users and permissions
roles: [Tenant Admin]
permissions: [membership.read, membership.manage]
related: [audit, companies, fleet]
screenshots: [1, 2, 7]
reviewed: false
---
## Para que serve esta página
A página Users and permissions gerencia as **associações (memberships)** do seu espaço de trabalho. Ela responde: "O que esta pessoa pode ver e fazer, e em quais navios?" A conta em si (nome de usuário, senha, MFA) fica no provedor de identidade central (IdP), não no POSEIDON. Esta página **convida** essa conta para uma associação e concede pacotes de funções, permissões e escopo de navios (13.03 §9). Um novo membro é convidado: o convite não concede nada até que a pessoa entre e o aceite.

## Quem usa
- **Tenant administrator:** atribui o escopo de usuário, função, navio e objeto com o menor privilégio, convida novos membros e gerencia delegações e suspensões (13.03 §9).
- Outros usuários com acesso de leitura às associações podem ver a lista, mas não alterá-la.
- A **pessoa convidada** apenas abre o link do convite; ela nunca vê esta página.

## Antes de começar
- Visualizar exige leitura de associações; alterar exige gerenciamento de associações mais gerenciamento em todo o tenant. Apenas com acesso de leitura, você vê "You can view but not change memberships."
- A pessoa precisa de uma conta no provedor de identidade. O **Expected IdP subject (optional)** limita o convite a essa única conta; sem ele, qualquer pessoa que entrar com o link pode aceitá-lo.
- Planeje um canal confiável para entregar o link do convite (por exemplo, o sistema de mensagens da sua própria organização). O POSEIDON não envia e-mails.
- Você só pode conceder permissões que você mesmo possui. Outro administrador edita a sua própria associação.
- A administração de associações exige uma conexão ativa com o servidor; nada pode ser alterado offline.

## Passo a passo
1. Veja os membros na lista **Members**. Filtre por **Status** (por exemplo **Invited**) e **Role bundle** e pressione **Apply**. Para pesquisar, informe um prefixo de id de associação ou de conta ou o IdP subject exato e pressione **Search**.
2. Abra um membro com **Open**. Revise **Effective permissions**, **Vessel scope**, **Invitation**, **Grant history** e **Delegations**.
3. Para convidar um membro, pressione **Add member**. Verifique o **IdP issuer**, informe opcionalmente o **Expected IdP subject (optional)**, escolha pelo menos um **Role bundle**, adicione **Individual permissions** se necessário e defina o escopo (todo o tenant ou pelo menos um navio). Depois pressione **Create membership**.
4. O membro abre como **Invited** com o **Invitation link**. Pressione **Copy invitation link** e entregue o link. O link só é mostrado agora; funciona uma única vez e expira após 24 horas.
5. Se o link foi perdido ou expirou, pressione **Create a new invitation link**; o link anterior deixa de funcionar. Para retirar o convite, pressione **Revoke invitation**.
6. A pessoa convidada abre o link, entra, verifica o que o convite concede e pressiona **Accept invitation**. Depois de entrar novamente, ela trabalha com a concessão que você definiu; o membro passa então a mostrar **Active** e **Invitation accepted**.
7. Para alterar a concessão de um membro existente, pressione **Edit grant** e depois **Save grant**. Cada gravação cria uma nova revisão da concessão; as revisões anteriores permanecem em **Grant history**.
8. Para uma transferência temporária, use **Delegate from this member** e preencha o delegado, **Valid from**, **Valid until**, **Reason** e **Delegated permissions**.
9. Para interromper o acesso, use **Suspend** em **Lifecycle**; **Resume** o reabre. **Revoke membership** é definitivo.

## O que significam os estados
- **Invited:** a associação aguarda a pessoa convidada. Ela não concede nada e não reserva a conta em lugar nenhum, nem em nenhuma outra organização.
- **Invitation pending / Invitation expired / Invitation replaced / Invitation revoked / Invitation accepted:** o estado do link de convite mais recente. Um link expirado, substituído ou revogado nunca pode ser aceito.
- **Active / Suspended / Revoked:** o ciclo de vida da associação. A próxima solicitação de um membro suspenso ou revogado é negada.
- **Origem da concessão — Managed here:** a concessão foi feita nesta página e pode ser alterada aqui.
- **Origem da concessão — Operator configuration:** a concessão vem da configuração do servidor (bootstrap/break-glass) e não pode ser editada aqui.
- **Origem da concessão — No grant:** existe um registro, mas ele não concede nada.
- **Not held by you / You do not hold every permission of this bundle:** você não pode conceder esta permissão ou este pacote porque não os possui.
- **Saved. The change takes effect on the member's next request.** O acesso de um membro convidado começa no próximo login após a aceitação.

## O que você não pode fazer / cuidado
- Você não pode conceder uma permissão, um navio ou um acesso em todo o tenant que você não possui.
- Você não pode alterar a sua própria associação; o último administrador ativo não pode ser removido.
- Uma conta só pode ter uma associação aceita em uma única organização. Um convite nunca transfere uma conta que já pertence a outra organização; a pessoa convidada então vê apenas que o convite não pode ser aceito.
- Trate o link do convite como uma chave: qualquer pessoa que o tenha e entre pode aceitá-lo, a menos que você tenha informado um subject esperado. Não o publique em canais compartilhados. Repasse sempre o link inteiro: a parte secreta depois de **#** é lida apenas pelo navegador e nunca chega ao servidor nem aos seus logs.
- Permissões de decisão, verificação e aprovação nunca fazem parte de um pacote. Somente um administrador que as possui pode concedê-las, uma a uma. Ações que dependem de designação (por exemplo, decisões de rebocador ou de saída) também exigem uma designação feita pelo operador. Conceder um pacote de funções não designa ninguém como Comandante ou verificador.
- Você não pode aprovar uma delegação em que você é a origem ou o delegado. Uma delegação nunca excede a autoridade atual da origem e nunca abrange todo o tenant.
- O POSEIDON não armazena senhas, guarda apenas uma impressão digital do link do convite e nunca mostra os IdP subjects de volta na lista.

## Se algo der errado
- **This identity is configured by the operator, already a member here or already invited here.** Abra o membro existente ou revogue primeiro o convite antigo.
- **A pessoa convidada vê "This invitation cannot be accepted":** o link expirou, foi usado, substituído ou revogado, pertence a outra conta, ou a conta dela já pertence a uma organização. Verifique o estado de **Invitation** e pressione **Create a new invitation link** se necessário.
- **O link não foi copiado:** selecione o campo do link e copie-o manualmente.
- **Exceeds your own authority:** escolha um pacote ou escopo mais restrito, ou peça a um administrador que possua a permissão.
- **The record changed meanwhile:** pressione **Reload current state** e faça a sua alteração novamente.
- **The outcome is unknown. Retry with the same request or reload.** Você pode reenviar o mesmo comando com segurança.
- **Not the trusted identity provider:** verifique o emissor. Se a conta vier de outro IdP, entre em contato com o administrador do sistema (13.03 §10).

## Páginas relacionadas
