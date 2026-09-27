# Produtos e ofertas

O produto representa o item base; cada oferta define uma condição comercial do produto. Ambos pertencem a uma organização. O vínculo composto `(ProductId, OrganizationId)` também é imposto pelo PostgreSQL.

## API

Para `/api/organizations/{organizationId}/products` e `/offers`:

- `GET`: lista paginada com `page` e `pageSize`.
- `POST`: cria em rascunho; retorna 201.
- `GET /{id}`: consulta individual.
- `PATCH /{id}`: atualiza campos; arquivamento usa `status: ARCHIVED`.

OWNER, ADMIN e OPERATOR podem escrever; FINANCE e SUPPORT podem ler. Um usuário externo à organização recebe 404. Todas as consultas e referências incluem OrganizationId.

Product contém name, slug, description, imageUrl e status. Offer contém productId, name, slug, headline, description, price, currency, status e billingType. Slugs de produto são únicos por organização; slugs de oferta são globais para a futura página pública. Estados são DRAFT, ACTIVE, INACTIVE e ARCHIVED. BillingType aceita ONE_TIME ou SUBSCRIPTION; cobrança recorrente entra na fase 13.

Preços usam decimal no servidor e numeric(18,2) no banco. Valores devem ser positivos, caber nessa precisão e possuir no máximo duas casas decimais; o sistema rejeita precisão excedente em vez de arredondar silenciosamente. Currency possui três letras maiúsculas. Pagamentos futuros recalcularão o valor no servidor a partir da oferta.

## Smoke local

Após `docker compose up -d --build`, executar `./scripts/smoke-catalog.ps1`. O script cria duas contas e organizações de teste, produtos e uma oferta, verifica isolamento e preço, arquiva a oferta e encerra as sessões. Esses registros de teste permanecem no banco local. Não executar em produção.

O JSON devolve price como string decimal exata (por exemplo, "19.90"); clientes devem preservar essa precisão. Requests aceitam string decimal ou número JSON válido; o frontend envia string. A criação exige DRAFT; publicação é uma atualização explícita de status.

Escritas de catálogo e mudanças de permissão usam o mesmo bloqueio de organização antes de ler o papel persistido, impedindo que uma escrita aguarde a alteração de papel e depois use uma permissão antiga. Criação/edição de produto, criação/edição de oferta e alteração de preço geram AuditLog na mesma transação.

A navegação usa `/products`, `/products/new`, `/products/{id}`, `/offers`, `/offers/new` e `/offers/{id}`, com contexto explícito de organização. Identificadores na URL nunca conferem autorização. As chaves de cache incluem identidade e organização.
