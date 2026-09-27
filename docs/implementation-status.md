# Mapa de implementação

A especificação mestre permanece a autoridade. Este mapa registra a sequência e dependências; não representa funcionalidades disponíveis antes de sua validação.

| Fase | Entrega | Estado |
| --- | --- | --- |
| 1 | Solution, Compose, PostgreSQL, Redis, Auth | Validada e enviada |
| 2 | Organizações, isolamento, equipe e papéis | Validada e enviada |
| 3 | Produtos e ofertas | Validada e enviada |
| 4 | Editor simples, página pública e checkout persistente | Validada e enviada |
| 5 | Clientes e identificação por organização | Próxima fase |
| 6 | Payment core, MerchantAccount e abstração PIX | Depende da infraestrutura financeira definida |
| 7 | Webhook assinado, confirmação e idempotência | Pendente |
| 8 | Ledger imutável, wallet e regras de taxas | Pendente |
| 9 | Fulfillment e entitlements | Pendente |
| 10 | Arquivos protegidos e external link | Pendente |
| 11 | Telegram | Após primeiro fluxo completo |
| 12 | Área de membros | Pendente |
| 13 | Assinaturas e expiração de acesso | Pendente |
| 14 | Dashboard, vendas e clientes | Pendente |
| 15 | Cupons, order bump e upsell | Pendente |
| 16 | Tracking, campanhas e pixels | Pendente |
| 17 | Automações persistentes e recuperação | Pendente |
| 18 | Afiliados e comissões idempotentes | Pendente |
| 19 | Saques e reserva atômica | Pendente |
| 20 | Discord e webhook de entrega | Pendente |
| 21 | Administração da plataforma | Pendente |
| 22 | Reconciliação com provedor | Pendente |
| 23 | Revisão e endurecimento de segurança | Pendente |
| 24 | Ampliação de testes e cenários financeiros | Pendente |
| 25 | Documentação final, seed Development e acabamento | Pendente |

Segurança, testes, migrations e documentação são exigidos em cada fase, não adiados às fases 23–25. Os campos e telas de recursos futuros são introduzidos quando a dependência for funcional.

## Dependências que não podem ser simuladas como produção

O provedor financeiro real precisa de contrato documentado para criação e consulta de cobrança, autenticação, idempotência, assinatura de webhook e ambiente de testes. A interface IPixProvider não autoriza inventar esse contrato. Cartão pode permanecer contrato preparado conforme seção 20. Valores e taxas de produção exigem configuração explícita.

A regra da seção 74 exige o primeiro fluxo EXTERNAL_LINK completo antes das integrações adicionais. Portanto, a fase 10 deve demonstrar confirmação server-side, ledger, wallet, entitlement, entrega e registro básico de venda; o dashboard mais abrangente continua na fase 14. Nenhum saldo será um campo livremente editável.
