# Multi-Stage Build for .NET 9 Application
# Works on Windows, macOS, and Linux

# ============================================================================
# Stage 1: Build
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

WORKDIR /src

# Copy project files
COPY ["Challenge.API/Challenge.API.csproj", "Challenge.API/"]
COPY ["Challenge.Tests/Challenge.Tests.csproj", "Challenge.Tests/"]

# Restore dependencies
RUN dotnet restore "Challenge.API/Challenge.API.csproj"

# Copy all source code
COPY . .

# Build the project
RUN dotnet build "Challenge.API/Challenge.API.csproj" -c Release -o /app/build

# Publish
RUN dotnet publish "Challenge.API/Challenge.API.csproj" -c Release -o /app/publish

# ============================================================================
# Stage 2: Runtime
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Install required tools
RUN apt-get update && \
    apt-get install -y curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published app from builder
COPY --from=builder /app/publish .

# Create directory for logs
RUN mkdir -p /app/logs && chmod -R 777 /app/logs

# Expose port
EXPOSE 5000

# Set environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Run application
ENTRYPOINT ["dotnet", "Challenge.API.dll"]
