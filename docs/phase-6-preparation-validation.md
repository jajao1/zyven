# Validação da preparação da fase 6

Preparação validada em 27/09/2026, sem declarar a fase 6 concluída. Commits de implementação: a7a6e08 e 249ce33.

- Gate completo: 23 testes unitários, 38 integrações reais e 28 frontend, total 89. Builds, lint, formatação, migrations e verificação do modelo passaram.
- Revisões independentes de especificação e qualidade sem bloqueadores dentro do escopo preparatório.
- Testes financeiros: precisão e limites monetários, taxa explícita, merchant ativo exigido pelo factory, snapshot válido e não expirado, processador indisponível e nenhuma chamada externa.
- Testes PostgreSQL: merchants pendentes para organizações existentes/novas, unicidade de conta/referências, tenant FKs, incompatibilidade de cliente/oferta mesmo dentro do tenant, contato alterável antes do pagamento e protegido após vínculo. Upgrade e downgrade/reaplicação exercitados somente em banco temporário exclusivo.
- Compose atualizado preservando dados. Smoke pelo proxy criou organizações e checkouts, confirmou merchants PENDING e nenhuma criação de pagamento.
- Inspeção local após smoke: 14 organizações, 14 merchants PENDING, zero pagamentos e zero vínculos inválidos de checkout/cliente. Nova execução do migrador confirmou banco atualizado sem alterações.

## Ocorrência na atualização local

A primeira execução do migrador encerrou com timeout Npgsql ao abrir conexão com PostgreSQL, antes de aplicar a migration. PostgreSQL permaneceu saudável; resolução DNS e conexão TCP entre containers passaram, e a versão de schema continuava na fase 5. A inicialização repetida sem alteração de código aplicou a migration e passou o smoke. Uma segunda execução independente do migrador também passou, sem migrations pendentes. A causa transitória exata não foi confirmada; não foi feita alteração especulativa no código ou nas garantias de banco.

## Limite da entrega

Não há endpoint de cobrança, provider real, QR Code, confirmação financeira, ledger ou entrega. O processador registrado informa indisponibilidade. A conclusão da fase 6 depende do PSP/banco e documentação oficial, configuração de taxas e implementação/validação da orquestração real de cobrança. O desenvolvimento não avança à fase 7 encobrindo essa dependência com mocks.
