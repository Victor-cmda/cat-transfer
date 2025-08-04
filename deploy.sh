#!/bin/bash
# =================================================================
# Script de Build e Deploy para Cat Transfer
# =================================================================

set -e

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Função de logging
log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

success() {
    echo -e "${GREEN}✅ $1${NC}"
}

warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

error() {
    echo -e "${RED}❌ $1${NC}"
    exit 1
}

# Verificar dependências
check_dependencies() {
    log "Verificando dependências..."
    
    if ! command -v docker &> /dev/null; then
        error "Docker não está instalado"
    fi
    
    if ! command -v docker-compose &> /dev/null; then
        error "Docker Compose não está instalado"
    fi
    
    success "Dependências verificadas"
}

# Build das imagens
build_images() {
    log "Construindo imagens Docker..."
    
    # Build da imagem principal
    log "Building cat-transfer:latest..."
    docker build -f Node/Dockerfile -t cat-transfer:latest .
    
    # Build da imagem de desenvolvimento
    log "Building cat-transfer:dev..."
    docker build -f Dockerfile.dev -t cat-transfer:dev .
    
    success "Imagens construídas com sucesso"
}

# Executar testes
run_tests() {
    log "Executando testes..."
    
    # Executar testes unitários no container
    docker run --rm -v $(pwd):/app -w /app mcr.microsoft.com/dotnet/sdk:9.0 \
        dotnet test --logger trx --collect:"XPlat Code Coverage"
    
    success "Testes executados com sucesso"
}

# Limpar recursos antigos
cleanup() {
    log "Limpando recursos antigos..."
    
    # Parar containers existentes
    docker-compose down --remove-orphans || true
    docker-compose -f docker-compose.dev.yml down --remove-orphans || true
    
    # Remover imagens antigas
    docker image prune -f
    
    success "Limpeza concluída"
}

# Deploy em desenvolvimento
deploy_dev() {
    log "Fazendo deploy em desenvolvimento..."
    
    # Subir ambiente de desenvolvimento
    docker-compose -f docker-compose.dev.yml up --build -d
    
    # Aguardar serviços ficarem prontos
    log "Aguardando serviços ficarem prontos..."
    sleep 30
    
    # Verificar health
    check_health_dev
    
    success "Deploy de desenvolvimento concluído"
    log "API disponível em: http://localhost:5000"
    log "Swagger disponível em: http://localhost:5000/swagger"
    log "Peer disponível em: http://localhost:5001"
}

# Deploy em produção
deploy_prod() {
    log "Fazendo deploy em produção..."
    
    # Subir ambiente de produção
    docker-compose up --build -d
    
    # Aguardar serviços ficarem prontos
    log "Aguardando serviços ficarem prontos..."
    sleep 60
    
    # Verificar health
    check_health_prod
    
    success "Deploy de produção concluído"
    log "API Principal: http://localhost:5000"
    log "Swagger: http://localhost:5000/swagger"
    log "Peer 1: http://localhost:5001"
    log "Peer 2: http://localhost:5002"
}

# Verificar health dos serviços em desenvolvimento
check_health_dev() {
    log "Verificando health dos serviços de desenvolvimento..."
    
    # API Principal
    if curl -f http://localhost:5000/api/node/status > /dev/null 2>&1; then
        success "API Principal (5000) está saudável"
    else
        warning "API Principal (5000) não está respondendo"
    fi
    
    # Peer
    if curl -f http://localhost:5001/api/node/status > /dev/null 2>&1; then
        success "Peer (5001) está saudável"
    else
        warning "Peer (5001) não está respondendo"
    fi
}

# Verificar health dos serviços em produção
check_health_prod() {
    log "Verificando health dos serviços de produção..."
    
    # API Principal
    if curl -f http://localhost:5000/api/node/status > /dev/null 2>&1; then
        success "API Principal (5000) está saudável"
    else
        warning "API Principal (5000) não está respondendo"
    fi
    
    # Peer 1
    if curl -f http://localhost:5001/api/node/status > /dev/null 2>&1; then
        success "Peer 1 (5001) está saudável"
    else
        warning "Peer 1 (5001) não está respondendo"
    fi
    
    # Peer 2
    if curl -f http://localhost:5002/api/node/status > /dev/null 2>&1; then
        success "Peer 2 (5002) está saudável"
    else
        warning "Peer 2 (5002) não está respondendo"
    fi
}

# Mostrar logs
show_logs() {
    local service=${1:-""}
    
    if [ -z "$service" ]; then
        log "Mostrando logs de todos os serviços..."
        docker-compose logs -f
    else
        log "Mostrando logs do serviço: $service"
        docker-compose logs -f "$service"
    fi
}

# Mostrar status
show_status() {
    log "Status dos containers:"
    docker-compose ps
    
    log "\nStatus das imagens:"
    docker images | grep cat-transfer
    
    log "\nUso de recursos:"
    docker stats --no-stream
}

# Menu de ajuda
show_help() {
    echo "Cat Transfer - Script de Build e Deploy"
    echo ""
    echo "Uso: $0 [comando]"
    echo ""
    echo "Comandos disponíveis:"
    echo "  build        - Construir imagens Docker"
    echo "  test         - Executar testes"
    echo "  dev          - Deploy em desenvolvimento"
    echo "  prod         - Deploy em produção"
    echo "  cleanup      - Limpar recursos antigos"
    echo "  logs [service] - Mostrar logs"
    echo "  status       - Mostrar status dos containers"
    echo "  stop         - Parar todos os serviços"
    echo "  restart      - Reiniciar serviços"
    echo "  help         - Mostrar esta ajuda"
    echo ""
    echo "Exemplos:"
    echo "  $0 build && $0 dev    # Build e deploy em dev"
    echo "  $0 logs cat-transfer-api  # Logs da API"
    echo "  $0 cleanup && $0 prod # Limpar e deploy em prod"
}

# Parar serviços
stop_services() {
    log "Parando todos os serviços..."
    docker-compose down
    docker-compose -f docker-compose.dev.yml down
    success "Serviços parados"
}

# Reiniciar serviços
restart_services() {
    log "Reiniciando serviços..."
    docker-compose restart
    success "Serviços reiniciados"
}

# Main
case "${1:-help}" in
    "build")
        check_dependencies
        build_images
        ;;
    "test")
        check_dependencies
        run_tests
        ;;
    "dev")
        check_dependencies
        cleanup
        deploy_dev
        ;;
    "prod")
        check_dependencies
        cleanup
        deploy_prod
        ;;
    "cleanup")
        cleanup
        ;;
    "logs")
        show_logs "$2"
        ;;
    "status")
        show_status
        ;;
    "stop")
        stop_services
        ;;
    "restart")
        restart_services
        ;;
    "help"|*)
        show_help
        ;;
esac
