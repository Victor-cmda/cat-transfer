# =================================================================
# Multi-stage Dockerfile para Cat Transfer Node
# Suporta tanto modo CLI quanto API
# =================================================================

# Build stage - Compilação da aplicação
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copiar arquivos de projeto e restaurar dependências
COPY ["Node/Node.csproj", "Node/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Protocol/Protocol.csproj", "Protocol/"]

# Restaurar dependências
RUN dotnet restore "Node/Node.csproj"

# Copiar código fonte
COPY . .

# Build da aplicação
WORKDIR "/src/Node"
RUN dotnet build "Node.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage - Preparação para produção
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Node.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime stage - Imagem final
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Criar usuário não-root para segurança
RUN groupadd -r cattransfer && \
    useradd -r -g cattransfer -m -d /home/cattransfer -s /bin/bash cattransfer

# Instalar dependências do sistema
RUN apt-get update && apt-get install -y \
    curl \
    netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*

# Definir diretório de trabalho
WORKDIR /app

# Criar diretórios necessários
RUN mkdir -p /app/data /app/logs /app/temp && \
    chown -R cattransfer:cattransfer /app && \
    chown -R cattransfer:cattransfer /home/cattransfer

# Copiar aplicação
COPY --from=publish /app/publish .
COPY --chown=cattransfer:cattransfer Node/appsettings.json .

# Criar script de entrada simples
RUN echo '#!/bin/bash\n\
set -e\n\
echo "[$(date)] Cat Transfer Node Starting"\n\
echo "Mode: ${NODE_MODE:-cli}"\n\
echo "Port: ${NODE_PORT:-8080}"\n\
echo "Host: ${NODE_HOST:-localhost}"\n\
\n\
case "${NODE_MODE:-cli}" in\n\
    "api")\n\
        echo "Starting in API mode"\n\
        exec dotnet Node.dll --api\n\
        ;;\n\
    "daemon")\n\
        echo "Starting in daemon mode"\n\
        exec dotnet Node.dll --daemon\n\
        ;;\n\
    *)\n\
        echo "Starting in CLI mode"\n\
        exec dotnet Node.dll\n\
        ;;\n\
esac' > /app/entrypoint.sh && chmod +x /app/entrypoint.sh

# Tornar executável
RUN chown cattransfer:cattransfer /app/entrypoint.sh

# Mudar para usuário não-root
USER cattransfer

# Expor portas
EXPOSE 8080 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/api/node/status || exit 1

# Volumes para persistência
VOLUME ["/app/data", "/app/logs"]

# Variáveis de ambiente padrão
ENV NODE_MODE=cli \
    NODE_HOST=0.0.0.0 \
    NODE_PORT=8080 \
    DOTNET_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5000

# Ponto de entrada
ENTRYPOINT ["/app/entrypoint.sh"]