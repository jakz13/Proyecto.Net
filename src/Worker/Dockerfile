FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy csproj files and restore dependencies
COPY UltimaMilla.sln ./
COPY src/Domain/UltimaMilla.Domain.csproj src/Domain/
COPY src/Application/UltimaMilla.Application.csproj src/Application/
COPY src/Worker/UltimaMilla.Worker.csproj src/Worker/

RUN dotnet restore src/Worker/UltimaMilla.Worker.csproj

# Copy source code and publish
COPY src/Domain/ src/Domain/
COPY src/Application/ src/Application/
COPY src/Worker/ src/Worker/

WORKDIR /source/src/Worker
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimaMilla.Worker.dll"]
