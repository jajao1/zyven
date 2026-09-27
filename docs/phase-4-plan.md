# Fase 4 — página pública e checkout persistente

Executar após validação da fase 3. Seguir seções 10–11 e preservar a separação entre Offer, Checkout e Payment.

## Página pública

`/o/{slug}` apresenta somente produto/oferta ativos. A resposta pública exclui dados internos da organização e quaisquer segredos de entrega. Editor simples de conteúdo (título, subtítulo, descrição, benefícios, FAQ, garantia e CTA), sem construtor visual complexo. URLs de mídia são validadas e nunca buscadas pelo servidor sem controles apropriados.

## Checkout

Criar CheckoutSession persistente com OrganizationId e OfferId resolvidos no servidor, status CREATED/PENDING_PAYMENT/COMPLETED/EXPIRED/ABANDONED, CreatedAt e ExpiresAt. A sessão pública usa uma credencial aleatória de acesso, armazenada como hash; um ID adivinhado não revela dados de cliente. Nome/e-mail são validados; vínculo CustomerId será adicionado na fase 5. Preço/moeda são carregados da Offer, nunca de valores enviados pelo comprador.

Nesta fase, o checkout coleta dados e apresenta o total. Não indicar pagamento realizado nem fabricar PIX; Payment entra na fase 6. Rejeitar oferta inativa e sessão expirada. Chamadas públicas têm limitação de taxa. Não expor integrações/entitlements/links de entrega antecipadamente.

## Verificação

- [ ] Testes de página ativa/inativa e ausência de campos internos.
- [ ] Preço enviado pelo cliente não altera o total, sessão persiste e expira, credencial ausente/inválida não acessa dados.
- [ ] Interface pública responsiva e formulário de checkout com erros claros.
- [ ] Migration, build, testes, lint, verificação do modelo e smoke pelo Compose.
- [ ] Revisão e commits antes da fase 5.
