FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ms-facturacion/ms-facturacion.csproj ms-facturacion/
RUN dotnet restore ms-facturacion/ms-facturacion.csproj

COPY . .
RUN dotnet publish ms-facturacion/ms-facturacion.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./

EXPOSE 8080

# Railway inyecta PORT en runtime; ENV en el Dockerfile no lo expandiria, por eso
# entrypoint en forma shell. Kestrel debe escuchar en 0.0.0.0, no en localhost.
ENTRYPOINT ["sh", "-c", "exec dotnet ms-facturacion.dll --urls http://0.0.0.0:${PORT:-8080}"]
