# Corre los tests del backend sin tener el SDK de .NET instalado:
#   docker compose --profile test run --rm tests
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props OrderFlow.sln ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet restore OrderFlow.sln
CMD ["dotnet", "test", "OrderFlow.sln", "--no-restore", "--logger", "console;verbosity=normal"]
