# Fase 6 — Payment core e integração PIX

Executar somente após a validação da fase 5. A integração financeira real depende da identificação do PSP/banco e de sua documentação; isso ainda não foi fornecido. Não inventar API, credenciais, QR Code ou confirmação de pagamento.

## Trabalho independente do fornecedor

Preparar o domínio MerchantAccount e Payment com os campos/estados das seções 17–20, contratos IPaymentProcessor/IPixProvider/ICardProvider e modelos de requisição/resposta independentes do transporte do fornecedor. Contratos de cartão recebem referência tokenizada; não criar armazenamento de PAN/CVV.

Dinheiro permanece decimal/numeric(18,2). Referências de checkout, cliente, oferta e merchant precisam preservar OrganizationId, inclusive por FKs compostas. Índices e restrições devem proteger referências externas e vínculos. Contratos de PIX devem carregar referência idempotente, valor autoritativo, moeda e expiração; consultas precisam permitir reconciliação posterior.

Nenhum provider simulado deve ser registrado na aplicação real. Doubles de teste são permitidos somente nos testes. Sem fornecedor configurado, nenhuma operação cria cobrança ou anuncia PIX disponível. Não aprovar merchants automaticamente em produção. Regras persistidas de taxa e ledger pertencem à fase 8; não assumir taxa de produção arbitrária para habilitar cobranças.

## Dependências para concluir a fase

O adaptador real exige documentação de autenticação, criação/consulta/cancelamento de cobrança conforme suporte, idempotência, limites, tratamento de timeout e ambiente sandbox. A etapa seguinte também exige especificação de assinatura e identificação dos webhooks. Credenciais devem ser configuradas localmente/por secret manager, nunca registradas em documentação ou logs.

Criar Payment de forma idempotente e consistente com a sessão. Não manter transação de banco aberta durante chamada de rede. Uma resposta perdida do provedor não autoriza gerar outra cobrança sem consulta/reutilização da referência idempotente. Valor e moeda vêm do servidor; alterações de preço desde o checkout precisam ser apresentadas para reconfirmação antes da cobrança.

Validar por build, testes de domínio/persistência/isolamento, migrations e smoke de indisponibilidade segura. A preparação independente não será registrada como FASE 6 concluída enquanto o fluxo de cobrança real estiver pendente. Não avançar à fase 7 deixando essa dependência encoberta por mocks.
