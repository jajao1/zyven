# Fase 3 — Products e Offers

Executar somente após conclusão e validação da fase 2.

## Dados e regras

Implementar os campos e enums das seções 8–9 da especificação. Product pertence a Organization; Offer pertence à mesma Organization e a um Product dela. Usar chave estrangeira composta para impedir vínculos cruzados mesmo fora do endpoint. Product.Slug é único por organização; Offer.Slug é globalmente único para a futura rota pública `/o/{slug}`.

Preço em `decimal` e PostgreSQL `numeric(18,2)`, positivo e com até duas casas decimais; nunca float/double. Currency é código de três letras maiúsculas. A criação começa em DRAFT; estados permitidos são DRAFT/ACTIVE/INACTIVE/ARCHIVED. BillingType é ONE_TIME ou SUBSCRIPTION. Atualizações exigem autorização persistida; não criar cópias financeiras ou saldo nesta fase.

## API e autorização

CRUD sem exclusão física (arquivamento via status) em `/api/organizations/{organizationId}/products` e `/offers`, com leitura individual, listas paginadas e atualização. OWNER/ADMIN/OPERATOR escrevem; membros leem. Não-membros recebem 404. Toda consulta inclui OrganizationId; validar tanto oferta quanto produto em mudanças de vínculo. Validação de nomes, slug, tamanho de descrições, moeda, preço e enum no backend.

## Interface e testes

- [ ] Listar/criar/editar produtos e ofertas na organização ativa, incluindo preço e status; manter interface responsiva e formulários validados.
- [ ] Testes de isolamento para leitura, atualização, listagem e vínculo de produto; papéis sem permissão de escrita.
- [ ] Testes de preço, precisão, slug duplicado e campos inválidos; comprovar persistência no PostgreSQL real.
- [ ] Migration com índices de organização, slug e CreatedAt; gate completo de build/testes/lint/modelo/smoke/revisão.
- [ ] Documentar evidências e criar commits antes da fase 4.

As páginas de catálogo devem ter navegação real em /products, /products/new, /products/:id e /offers, /offers/new, /offers/:id, respeitando contexto de organização e identidade no cache. Auditoria registra criação/alteração de ofertas e preços. Métricas de vendas/receita e abas dependentes de fases futuras não devem apresentar dados fictícios.
