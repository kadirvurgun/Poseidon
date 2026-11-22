---
key: renewal-workflow
title: Renovação de certificado
roles: [Master, Chief Engineer / Technical Superintendent, Regulatory / Assurance]
permissions: [certificate.read, certificate.write, certificate.renewal.write, certificate.renewal.verify, certificate.renewal.complete, certificate.renewal.owner, file.read]
related: [certificate-command-center, pre-survey-checklist, survey-planner, audit]
screenshots: []
reviewed: false
---
## Para que serve esta página
A página Renovação de certificado registra, passo a passo, a preparação da renovação de um certificado e seu fluxo interno: preparação, solicitação, aguardo da autoridade, substituto emitido, verificação e conclusão. Ela responde: "Em que ponto está a renovação deste certificado, e quem executa a próxima etapa?" É apenas um registro de fluxo interno; não estabelece validade legal nem aprovação da autoridade (13.03 §1, §8).

## Quem usa
- **Comandante, Chefe de máquinas / Superintendente técnico:** preparam o rascunho de renovação e registram os requisitos e a solicitação.
- **Responsável designado pela renovação:** faz o fluxo avançar. É uma função exercida por membros com a permissão de responsável pela renovação; o fluxo não pode ser aberto nem validado sem um responsável.
- **Verificador e concluinte:** comparam o substituto emitido com sua fonte e o verificam e, como outra pessoa, concluem a renovação (13.03 §1, §8).

## Antes de começar
- A visualização exige leitura de certificados. Preparar um rascunho exige escrita de certificados e escrita de renovação naquela embarcação; verificar exige verificação de renovação; concluir exige conclusão de renovação.
- Primeiro escolha a embarcação correta em **Selecione uma embarcação** (13.03 §1).
- O certificado a renovar deve ter uma revisão salva vigente. Um certificado substituído não pode ser renovado.
- As etapas do fluxo exigem uma conexão ativa. Offline, você vê "You are offline. Workflow steps are unavailable until the connection returns."

## Passo a passo
1. Abra um **Registro de renovação** em **Rascunhos de renovação registrados**. Para uma nova renovação, use **Escolher certificado para renovação** para ir à [Central de comando de certificados](/app/certificate-command-center) e pressione **Preparar renovação** no registro.
2. Preencha o **Formulário de renovação de certificado**: **Data declarada de vencimento da renovação**, a janela de vistoria, **Local planejado**, **Papel designado do responsável pela renovação** e **Declaração de vistoria exigida**, **Declaração de documentos exigidos**, **Declaração de ações exigidas**. Deixe como **Desconhecido** um valor que você não conhece.
3. Salve o rascunho com **Salvar rascunho não verificado**.
4. Em **Fluxo de renovação**, escolha em **Etapas disponíveis** a etapa aberta para a sua função, por exemplo **Confirmar requisitos concluídos**, **Registrar solicitação** ou **Aguardar decisão da autoridade**. Preencha os campos obrigatórios e pressione **Registrar etapa**.
5. Quando a autoridade emitir o novo certificado, use **Registrar substituto emitido** e selecione-o. O próprio documento dele é usado como evidência.
6. O verificador usa **Verificar substituto** e define **O emissor confere**, **O número do certificado confere**, **O objeto confere**, **As datas conferem** e **O escopo confere** comparando com a fonte.
7. Outra pessoa conclui a renovação com **Concluir e substituir**. Se a autoridade recusou, use **Registrar rejeição da autoridade**; se a preparação for abandonada, use **Cancelar preparação**.

## O que significam os estados
- **Rascunho / Em preparação:** a renovação está sendo preparada; o formulário pode ser editado.
- **Pronto para solicitar / Solicitação registrada / Aguardando a autoridade:** a fase de solicitação. A decisão da autoridade acontece fora do sistema.
- **Emitido, não verificado:** o substituto está registrado, mas ainda não foi comparado com sua fonte por uma pessoa.
- **Verificado pelo revisor:** o verificador registrou a comparação; a renovação ainda não está concluída.
- **Concluído:** o fluxo interno está encerrado e o substituto substitui o original. Isso não é uma decisão sobre a validade legal do certificado.
- **Cancelado / Rejeitado:** o fluxo foi interrompido.
- Um requisito pode estar **Concluído / Não exigido / Desconhecido**. **Desconhecido** não significa "Não exigido"; o servidor não avança o fluxo com um requisito desconhecido.

## O que você não pode fazer / cuidado
- A pessoa que registrou o substituto emitido, ou que cadastrou esse certificado substituto, não pode verificá-lo. O concluinte deve ser diferente do verificador, da pessoa que registrou o substituto emitido e do autor do certificado substituto.
- A verificação e a conclusão são exclusivamente decisões humanas; Concluído ≠ Verificado.
- As declarações de planejamento não comprovam vistorias realizadas, solicitações aprovadas, aptidão para viagem nem verificação.
- Os vínculos com substitutos em rascunho não substituem o original. O certificado antigo só é encerrado pela etapa de conclusão, com um vínculo de substituição rastreável (13.03 §1).
- Cada etapa é registrada como uma revisão imutável; o histórico permanece em **Histórico de revisões registradas**.
- Sem um responsável designado explicitamente, o sistema nunca adota um administrador por padrão.

## Se algo der errado
- **Nenhuma etapa disponível para sua função neste estado.** A próxima etapa pertence a outra função; verifique sua permissão e designação e, se necessário, entre em contato com o Tenant Admin (13.03 §10).
- **The renewal changed after it was loaded.** ou **Esta não é a revisão atual.** Pressione **Recarregar** e revise o estado atual.
- **O resultado é desconhecido. Tentar novamente reenvia a mesma solicitação.** Use **Repetir a mesma solicitação**.
- **Refused for separation of duties: you recorded an earlier step of this renewal, so a different person must take this step.** O registro não mudou; recarregar não ajuda. Deixe a etapa para outra pessoa autorizada.
- **A etapa não foi registrada. Verifique os dados e tente novamente.** Verifique os campos obrigatórios e as seleções de conferência.
- Se o substituto não puder ser verificado, confira o documento e a linhagem da fonte e encaminhe-o ao verificador (13.03 §10).

## Páginas relacionadas
