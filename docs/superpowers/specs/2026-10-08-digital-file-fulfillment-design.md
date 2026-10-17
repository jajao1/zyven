# Entrega de arquivo digital protegido

## Objetivo

Adicionar `DIGITAL_FILE` como segundo fulfillment funcional, sem alterar a confirmação financeira nem expor arquivos por URL permanente.

## Modelo

Cada oferta pode possuir uma definição `DIGITAL_FILE` além de `EXTERNAL_LINK`. O upload cria um `DigitalAsset` imutável com organização, nome apresentado, tipo detectado, tamanho, hash SHA-256 e chave aleatória de armazenamento. A definição aponta para o asset ativo. Substituir o arquivo cria outro asset; compras já entregues preservam o asset originalmente recebido.

## Upload e armazenamento

OWNER, ADMIN e OPERATOR podem enviar um arquivo de até 25 MiB. São aceitos PDF, ZIP/EPUB/OOXML, PNG e JPEG, validados por assinatura binária e tipo declarado. O nome do cliente nunca compõe o caminho físico. Uma abstração `IPrivateFileStore` usa diretório privado persistente no desenvolvimento e permite trocar por object storage em produção.

## Entrega

Após pagamento confirmado, a execução guarda o `DigitalAssetId` e fica `COMPLETED`. O checkout autenticado e a Área do comprador recebem uma URL interna específica para a execução. O download consulta novamente checkout/sessão, pagamento e entitlement ativo antes de abrir o arquivo, usa `Content-Disposition: attachment`, impede cache público e nunca revela a chave do storage.

## Consistência e segurança

O índice único por entitlement e definição mantém idempotência. Replays não duplicam entrega. Upload incompleto remove o arquivo parcial; falha no banco deixa o asset sem referência, que pode ser limpo por rotina posterior. IDs estrangeiros retornam 404. Limites Kestrel e multipart são configurados juntos.

## Interface e validação

O editor de oferta passa a permitir escolher link externo ou arquivo digital, mostrando nome, tamanho e tipo do arquivo atual. A biblioteca apresenta “Baixar arquivo”. Testes cobrem assinaturas inválidas, limite, path traversal, autorização, isolamento, replay e download após pagamento. Build, migrações, testes, lint e smoke Compose devem passar.
