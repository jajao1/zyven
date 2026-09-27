# Clientes por organização

Customer pertence a uma organização e registra nome, e-mail, telefone, documento e datas. O checkout conserva os dados capturados naquela sessão; esses dados não são substituídos silenciosamente pelo perfil existente.

## Identificação e privacidade

Novos e-mails aceitam caracteres ASCII imprimíveis, removendo somente espaço, tabulação e quebras de linha das extremidades. A chave converte letras ASCII para maiúsculas, sem remover pontos nem aliases; espaços internos e caracteres não ASCII são rejeitados. A migration usa a mesma transformação determinística. A chave única inclui OrganizationId. Telefones novos usam prefixo internacional explícito, removendo espaços, parênteses e hífens; números locais ambíguos não recebem país presumido.

O mesmo e-mail em duas organizações identifica clientes distintos. Dentro de uma organização, checkouts concorrentes reaproveitam o cliente pela chave normalizada sob transação e lock de organização. Uma alegação de telefone não une perfis com e-mails diferentes. Conflitos retornam uma mensagem genérica para revisão dos dados.

Preencher o e-mail de outra pessoa não prova identidade. Um checkout anônimo não altera nome, documento ou contato de um perfil existente nem recebe os dados antigos desse perfil. A resposta contém somente os dados informados naquela sessão. O vínculo CustomerId é persistido junto do checkout e protegido por FK composta com OrganizationId.

## Consulta administrativa

- `GET /api/organizations/{org}/customers?page=1&pageSize=20`: lista paginada, até 100 itens por página.
- `GET /api/organizations/{org}/customers/{id}`: detalhe do cliente naquela organização.

Ambas as rotas exigem sessão válida e vínculo persistido na organização. IDs de outro tenant recebem 404. A interface completa de clientes e timeline pertence à fase 14; esta fase não simula compras ou totais financeiros.

## Atualização de dados existentes e smoke

A migration preserva checkouts da fase 4 e associa clientes por organização/e-mail normalizado antes de exigir CustomerId. Telefones legados sem prefixo internacional ficam preservados como texto sem chave canônica.

`./scripts/smoke-customers.ps1` cria registros locais de teste e verifica deduplicação, preservação de perfil, ausência de PII anterior na resposta anônima e isolamento entre organizações. Os registros ficam no banco local; o script encerra a sessão do vendedor. Não executar em produção.
