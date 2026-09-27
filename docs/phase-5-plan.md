# Fase 5 — Customers

Executar após validação da fase 4. Implementar Customer com os campos da seção 15; OrganizationId é obrigatório.

Identificar por e-mail normalizado e telefone normalizado dentro da organização. Prevenir duplicação concorrente por e-mail com unique constraint e transação/retry específico. Não unir automaticamente dois clientes diferentes quando e-mail e telefone apontarem para registros distintos: retornar conflito para decisão explícita. Dados de uma organização nunca são usados para identificar clientes em outra.

Vincular a criação do checkout ao Customer, resolvendo OrganizationId pela oferta. Endpoints administrativos de consulta paginada e detalhe exigem vínculo e permissões persistidos. A UI completa de timeline/vendas pertence à fase 14; nesta fase, implementar a base funcional sem histórico de vendas fictício.

- [ ] Testes de isolamento, normalização, duplicação concorrente, conflito e checkout com CustomerId.
- [ ] Migration com índices de OrganizationId, Email e Phone.
- [ ] Gates completos e smoke antes de iniciar Payment.

A fase 6 precisa identificar o PSP/banco e sua documentação para cobranças PIX reais. A abstração IPaymentProcessor/IPixProvider/ICardProvider deve manter detalhes do fornecedor fora das regras comerciais.
