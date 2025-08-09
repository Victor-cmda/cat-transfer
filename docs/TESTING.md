# Guia de Testes — cat-transfer

Este documento descreve como executar um teste ponta a ponta de transferência de arquivo entre dois nós locais (dev e peer) usando o ambiente Docker de desenvolvimento.

## Pré‑requisitos

- Windows com Docker Desktop
- .NET SDK 9 (para compilar antes de subir os contêineres)
- PowerShell e/ou curl disponíveis no PATH

## Visão geral do ambiente

- Serviço dev: API em http://localhost:5000, P2P em 8080
- Serviço peer: API em http://localhost:5001, P2P em 8081
- docker-compose.dev.yml usa bind mount de `./Node/bin/Debug/net9.0` para executar os binários compilados e mapeia `./test-files` para `/app/test-files` dentro dos contêineres.

## Passo 1 — Compilar a solução

```cmd
cd e:\Projects\cat-transfer
dotnet build cat-transfer.sln -v minimal
```

## Passo 2 — Subir o ambiente de desenvolvimento

```cmd
docker compose -f docker-compose.dev.yml up -d
```

Verifique o status básico dos nós:

```cmd
curl -s http://localhost:5000/api/node/status
curl -s http://localhost:5001/api/node/status
```

Ambos devem retornar JSON com `isRunning: true`.

## Passo 3 — Conectar os peers (dev → peer)

Envie a conexão a partir do nó dev para o peer:

```cmd
curl -s -X POST http://localhost:5000/api/peers/connect ^
  -H "Content-Type: application/json" ^
  -d "{\"address\":\"cat-transfer-peer-dev\",\"port\":8081}"
```

Liste os peers conectados:

```cmd
curl -s http://localhost:5000/api/peers
curl -s http://localhost:5001/api/peers
```

## Passo 4 — Preparar um arquivo de teste e checksum

Crie um arquivo de 5 MB em `./test-files` e calcule o SHA-256 (base64):

```powershell
# PowerShell
$dir = 'e:\\Projects\\cat-transfer\\test-files'
$null = New-Item -ItemType Directory -Force -Path $dir
$file = Join-Path $dir 'sample-5mb.bin'
if (-not (Test-Path $file)) {
  $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  $buf = New-Object byte[] (1MB)
  $fs = [System.IO.File]::Open($file,'Create','Write')
  try { 1..5 | ForEach-Object { $rng.GetBytes($buf); $fs.Write($buf,0,$buf.Length) } }
  finally { $fs.Close() }
}
$hash = [Convert]::ToBase64String([System.Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($file)))
$size = (Get-Item $file).Length
"FILE=$file`nHASH=$hash`nSIZE=$size"
```

Guarde o NodeId do peer (porta 5001):

```cmd
curl -s http://localhost:5001/api/node/status
```

Copie o valor de `nodeId` do JSON (ex.: `e0b1aec8027d45e5a68deb92be29f426`).

## Passo 5 — Iniciar uma transferência

Monte um payload JSON e envie para o nó dev (porta 5000). O campo `checksum` deve ser uma string base64; `sourcePath` precisa existir no contêiner (use `/app/test-files/<nome>`):

```powershell
# PowerShell — substitua <PEER_NODE_ID> pelo nodeId do peer
$payload = [ordered]@{
  fileId = 'sample-5mb.bin'
  fileName = 'sample-5mb.bin'
  fileSize = $env:SIZE ? $env:SIZE : (Get-Item 'e:\\Projects\\cat-transfer\\test-files\\sample-5mb.bin').Length
  targetNodeId = '<PEER_NODE_ID>'
  checksum = $env:HASH
  checksumAlgorithm = 1   # Sha256
  chunkSize = 65536       # opcional
  sourcePath = '/app/test-files/sample-5mb.bin'
} | ConvertTo-Json -Compress
Invoke-RestMethod -Uri http://localhost:5000/api/transfers -Method Post -ContentType 'application/json' -Body $payload | ConvertTo-Json -Compress
```

Alternativa com arquivo payload via cmd:

```cmd
REM Crie arquivo de payload
mkdir e:\Projects\cat-transfer\tmp 2>nul
powershell -NoProfile -Command "$hash=[Convert]::ToBase64String([System.Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes('e:\\Projects\\cat-transfer\\test-files\\sample-5mb.bin'))); $size=(Get-Item 'e:\\Projects\\cat-transfer\\test-files\\sample-5mb.bin').Length; $obj=[ordered]@{ fileId='sample-5mb.bin'; fileName='sample-5mb.bin'; fileSize=$size; targetNodeId='<PEER_NODE_ID>'; checksum=$hash; checksumAlgorithm=1; chunkSize=65536; sourcePath='/app/test-files/sample-5mb.bin' }; ($obj | ConvertTo-Json -Compress) | Set-Content -Encoding UTF8 -Path 'e:\\Projects\\cat-transfer\\tmp\\start.json'"

curl -s -X POST http://localhost:5000/api/transfers -H "Content-Type: application/json" --data-binary @e:\Projects\cat-transfer\tmp\start.json
```

## Passo 6 — Validar

- Emissor (dev):

```cmd
curl -s http://localhost:5000/api/transfers
```

- Receptor (peer):

```cmd
curl -s http://localhost:5001/api/transfers
```

- Logs (peer) para confirmar recebimento e conclusão:

```cmd
REM Windows: use PowerShell para ver as últimas linhas
docker logs --since 5m cat-transfer-peer-dev | powershell -NoProfile -Command "$input | Select-Object -Last 200 | Out-String"
```

Observações atuais:
- A transferência é iniciada automaticamente no peer ao receber `file_init`. Os chunks são armazenados e o ator finaliza a transferência. As rotas de progresso na API podem não refletir os bytes reais ainda—confirme pelos logs do peer.

## Troubleshooting

- 400 “checksum could not be converted”: garanta que `checksum` é uma string base64 (entre aspas), não um array.
- “Arquivo de origem não encontrado”: verifique se `./test-files/<arquivo>` existe e se `sourcePath` aponta para `/app/test-files/<arquivo>`.
- Peer não conectado: repita o POST `/api/peers/connect` do dev para o peer (endereço `cat-transfer-peer-dev`, porta `8081`).
- Verifique NodeIds corretos com `/api/node/status` (5000 e 5001) e use o `nodeId` do peer em `targetNodeId`.

## Encerramento

```cmd
docker compose -f docker-compose.dev.yml down
```

## Execução sem Docker (opcional)

Você pode iniciar o nó como API diretamente:

```cmd
REM Em dois terminais separados
cd e:\Projects\cat-transfer

dotnet run --project Node/Node.csproj -- --api
REM Em outro terminal, ajuste porta/host via variáveis de ambiente ou appsettings
```

Em seguida, conecte os peers e teste como acima (ajuste portas conforme necessário).
