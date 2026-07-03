FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Portfolio.Domain/Portfolio.Domain.csproj src/Portfolio.Domain/
COPY src/Portfolio.Application/Portfolio.Application.csproj src/Portfolio.Application/
COPY src/Portfolio.Infrastructure/Portfolio.Infrastructure.csproj src/Portfolio.Infrastructure/
COPY src/Portfolio.Web/Portfolio.Web.csproj src/Portfolio.Web/
RUN dotnet restore src/Portfolio.Web/Portfolio.Web.csproj

COPY src/ src/
RUN dotnet publish src/Portfolio.Web/Portfolio.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Portfolio.dll"]
