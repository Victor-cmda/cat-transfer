@echo off
REM =================================================================
REM Script de Deploy para Cat Transfer (Windows CMD/Batch)
REM =================================================================

setlocal enabledelayedexpansion

set "COMMAND=%~1"
set "SERVICE=%~2"

if "%COMMAND%"=="" set "COMMAND=help"

echo.
echo [%date% %time%] Cat Transfer - Deploy Script
echo.

goto %COMMAND% 2>nul || goto help

:build
echo [INFO] Construindo imagens Docker...
echo.
docker --version >nul 2>&1
if errorlevel 1 (
    echo [ERRO] Docker nao esta instalado ou nao esta no PATH
    goto end
)

echo [INFO] Building cat-transfer:latest...
docker build -f Node/Dockerfile -t cat-transfer:latest .
if errorlevel 1 (
    echo [ERRO] Falha no build da imagem principal
    goto end
)

echo [INFO] Building cat-transfer:dev...
docker build -f Dockerfile.dev -t cat-transfer:dev .
if errorlevel 1 (
    echo [ERRO] Falha no build da imagem de desenvolvimento
    goto end
)

echo [SUCESSO] Imagens construidas com sucesso
goto end

:dev
echo [INFO] Fazendo deploy em desenvolvimento...
echo.

REM Limpar containers antigos
echo [INFO] Limpando containers antigos...
docker-compose -f docker-compose.dev.yml down --remove-orphans >nul 2>&1

REM Subir ambiente de desenvolvimento
echo [INFO] Subindo ambiente de desenvolvimento...
docker-compose -f docker-compose.dev.yml up --build -d
if errorlevel 1 (
    echo [ERRO] Falha no deploy de desenvolvimento
    goto end
)

echo [INFO] Aguardando servicos ficarem prontos...
timeout /t 30 /nobreak >nul

echo [SUCESSO] Deploy de desenvolvimento concluido
echo.
echo URLs disponiveis:
echo   API: http://localhost:5000
echo   Swagger: http://localhost:5000/swagger
echo   Peer: http://localhost:5001
echo.
echo Para ver logs: docker-compose -f docker-compose.dev.yml logs -f
goto end

:prod
echo [INFO] Fazendo deploy em producao...
echo.

REM Limpar containers antigos
echo [INFO] Limpando containers antigos...
docker-compose down --remove-orphans >nul 2>&1

REM Subir ambiente de producao
echo [INFO] Subindo ambiente de producao...
docker-compose up --build -d
if errorlevel 1 (
    echo [ERRO] Falha no deploy de producao
    goto end
)

echo [INFO] Aguardando servicos ficarem prontos...
timeout /t 60 /nobreak >nul

echo [SUCESSO] Deploy de producao concluido
echo.
echo URLs disponiveis:
echo   API Principal: http://localhost:5000
echo   Swagger: http://localhost:5000/swagger
echo   Peer 1: http://localhost:5001
echo   Peer 2: http://localhost:5002
echo.
echo Para ver logs: docker-compose logs -f
goto end

:test
echo [INFO] Executando testes...
echo.

REM Verificar se API esta respondendo
curl -f http://localhost:5000/api/node/status >nul 2>&1
if errorlevel 1 (
    echo [AVISO] API nao esta respondendo em http://localhost:5000
) else (
    echo [SUCESSO] API Principal esta saudavel
)

REM Verificar Swagger
curl -f http://localhost:5000/swagger >nul 2>&1
if errorlevel 1 (
    echo [AVISO] Swagger nao esta acessivel
) else (
    echo [SUCESSO] Swagger esta acessivel
)

echo [INFO] Testes concluidos
goto end

:logs
if "%SERVICE%"=="" (
    echo [INFO] Mostrando logs de todos os servicos...
    docker-compose logs -f
) else (
    echo [INFO] Mostrando logs do servico: %SERVICE%
    docker-compose logs -f %SERVICE%
)
goto end

:status
echo [INFO] Status dos containers:
echo.
docker-compose ps
echo.
echo [INFO] Imagens cat-transfer:
docker images | findstr cat-transfer
echo.
echo [INFO] Uso de recursos:
docker stats --no-stream
goto end

:stop
echo [INFO] Parando todos os servicos...
docker-compose down
docker-compose -f docker-compose.dev.yml down
echo [SUCESSO] Servicos parados
goto end

:restart
echo [INFO] Reiniciando servicos...
docker-compose restart
echo [SUCESSO] Servicos reiniciados
goto end

:cleanup
echo [INFO] Limpando recursos antigos...
docker-compose down --remove-orphans >nul 2>&1
docker-compose -f docker-compose.dev.yml down --remove-orphans >nul 2>&1
docker image prune -f >nul 2>&1
echo [SUCESSO] Limpeza concluida
goto end

:help
echo Cat Transfer - Script de Build e Deploy (Windows)
echo.
echo Uso: deploy.bat [comando] [servico]
echo.
echo Comandos disponiveis:
echo   build        - Construir imagens Docker
echo   test         - Executar testes basicos
echo   dev          - Deploy em desenvolvimento
echo   prod         - Deploy em producao
echo   cleanup      - Limpar recursos antigos
echo   logs [srv]   - Mostrar logs
echo   status       - Mostrar status dos containers
echo   stop         - Parar todos os servicos
echo   restart      - Reiniciar servicos
echo   help         - Mostrar esta ajuda
echo.
echo Exemplos:
echo   deploy.bat build
echo   deploy.bat dev
echo   deploy.bat logs cat-transfer-api
echo   deploy.bat cleanup
echo.
echo URLs apos deploy:
echo   Desenvolvimento: http://localhost:5000/swagger
echo   Producao: http://localhost:5000/swagger
goto end

:end
echo.
