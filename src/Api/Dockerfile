FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy csproj files and restore dependencies
COPY UltimaMilla.sln ./
COPY src/Domain/UltimaMilla.Domain.csproj src/Domain/
COPY src/Application/UltimaMilla.Application.csproj src/Application/
COPY src/Infrastructure/UltimaMilla.Infrastructure.csproj src/Infrastructure/
COPY src/Api/UltimaMilla.Api.csproj src/Api/

RUN dotnet restore src/Api/UltimaMilla.Api.csproj

# Copy source code and publish
COPY src/Domain/ src/Domain/
COPY src/Application/ src/Application/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Api/ src/Api/

WORKDIR /source/src/Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UltimaMilla.Api.dll"]
