# Escopo do projeto Zyven

Atualizado em 11 de outubro de 2026. A especificação mestre continua em [`zyven-master-spec.txt`](zyven-master-spec.txt); este documento apresenta o produto, o que já funciona e o trabalho restante.

## Visão do produto

Zyven é uma plataforma SaaS multi-tenant para vender produtos digitais. O fluxo central é:

**Product → Offer → Checkout → Payment → Entitlement → Fulfillment**

O vendedor organiza produtos e ofertas, publica uma página de venda, recebe por PIX e acompanha clientes, vendas e saldo. Após a confirmação do pagamento, o comprador recebe seu direito de acesso e pode recuperar a entrega em uma área própria.

## Escopo já entregue

### Plataforma e segurança

- Backend em .NET 10 e ASP.NET Core 10, frontend React/TypeScript/Vite e ambiente Docker Compose.
- PostgreSQL, Redis, Hangfire, migrations EF Core, OpenAPI, Serilog e health checks.
- Cadastro, login, logout, JWT curto e refresh token rotativo em cookie seguro.
- Proteção contra reutilização de refresh token, rate limit, auditoria de sessões e segredos fora do repositório.
- Testes unitários, integrados e de frontend, lint, build e CI.

### Organizações e equipe

- Criação e edição de organizações.
- Isolamento de dados por organização em serviços, consultas e restrições do banco.
- Equipe com papéis Proprietário, Administrador e Operador.
- Inclusão, alteração de papel e remoção de membros com regras de autorização.

### Produtos, ofertas e checkout

- Cadastro e edição de produtos.
- Ofertas independentes vinculadas a produtos, com preço, status e slug público.
- Editor simples da página pública.
- Checkout persistente com identificação e atualização segura do comprador.
- Página pública e recuperação do checkout por cookie protegido.
- Diretório de clientes deduplicado e isolado por organização.

### Pagamento PIX

- Núcleo de pagamentos e conta de recebimento por organização.
- Integração PushinPay em que cada vendedor informa o token da própria conta.
- Token do vendedor cifrado no banco.
- Criação e consulta de cobrança PIX.
- Webhook autenticado, confirmação no servidor e processamento idempotente.
- Taxa da plataforma fixa e configurável, com suporte a split.
- Falha segura quando o gateway ou a conta ainda não estão configurados.

### Financeiro

- Ledger imutável como fonte financeira.
- Carteira e saldo derivados dos lançamentos.
- Registro de valor bruto, taxa do gateway, taxa da plataforma e valor líquido.
- Painel com resumo financeiro, lista e detalhe de vendas e extrato paginado.
- Consistência transacional entre pagamento, ledger, entitlement e entrega.

### Entrega e área do comprador

- Entitlement criado somente após pagamento confirmado.
- Entrega `EXTERNAL_LINK` com snapshot da configuração comprada.
- Entrega `DIGITAL_FILE` com upload privado de PDF, ZIP, EPUB, XLSX, PNG e JPEG de até 25 MiB.
- Validação de assinatura do arquivo, nome seguro, hash SHA-256 e armazenamento fora da área pública.
- Download autorizado novamente pelo pagamento e entitlement, sem expor a chave física.
- Restrições e verificações contra entrega duplicada.
- Área do comprador com login por código temporário, sessão independente e biblioteca unificada.
- Recuperação de compras e entregas por comprador autenticado.

### Interface do vendedor

- Visão geral da organização.
- Telas distintas de Produtos e Ofertas.
- Clientes, equipe e configurações.
- Vendas, carteira, extrato e detalhe financeiro.
- Layout escuro responsivo baseado no design fornecido.

## Dependências para uso real

- Cadastrar credenciais válidas da PushinPay e configurar a conta da plataforma para o split.
- Configurar URL pública HTTPS e webhook no gateway.
- Contratar/configurar um provedor de e-mail transacional para os códigos da Área do Comprador; em desenvolvimento o código é entregue pelo mecanismo local.
- Definir armazenamento persistente de produção para arquivos privados e sua rotina de backup.
- Preparar infraestrutura de produção com TLS, segredos gerenciados, observabilidade e política de backup.

## Escopo restante

### Próximas entregas funcionais

1. Entrega e automação via Telegram.
2. Área de membros nativa com módulos, aulas e progresso.
3. Assinaturas, renovação e expiração automática de acesso.
4. Gráficos históricos e exportação do painel financeiro.
5. Cupons, order bump e upsell.
6. Tracking, campanhas, UTMs e pixels.
7. Automações persistentes, tentativas e recuperação de falhas.
8. Afiliados e comissões idempotentes.
9. Saques com reserva atômica de saldo e ciclo de aprovação.
10. Entregas Discord e webhook genérico.
11. Administração interna da plataforma.
12. Reconciliação automática com o provedor de pagamento.

### Preparação para produção

- Testar o ciclo completo no sandbox/conta real da PushinPay.
- Integrar e-mail transacional e acompanhar entregabilidade.
- Ampliar cenários financeiros concorrentes, reembolso, chargeback e reconciliação.
- Revisar segurança, permissões, retenção de dados e LGPD.
- Criar deploy de produção, monitoramento, alertas, backups e recuperação de desastre.
- Finalizar documentação operacional, dados de demonstração e acabamento responsivo.

## Estado atual do produto

O Zyven já cobre um **MVP de venda digital por PIX**: o vendedor cria produto e oferta, publica o checkout, recebe uma cobrança, confirma o pagamento por webhook, registra o financeiro, gera o acesso e entrega link ou arquivo ao comprador. Para operação comercial em produção ainda são necessárias as integrações externas e o endurecimento operacional listados acima.
