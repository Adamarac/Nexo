# Imagem da API e do Worker. O argumento PROJECT escolhe qual dos dois publicar.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src

# Primeiro só os arquivos de projeto: assim o restore fica em cache enquanto os pacotes não mudam.
COPY Directory.Build.props Directory.Packages.props global.json .editorconfig ./
COPY src/Nexo.Domain/Nexo.Domain.csproj src/Nexo.Domain/
COPY src/Nexo.Application/Nexo.Application.csproj src/Nexo.Application/
COPY src/Nexo.Infrastructure/Nexo.Infrastructure.csproj src/Nexo.Infrastructure/
COPY src/Nexo.Api/Nexo.Api.csproj src/Nexo.Api/
COPY src/Nexo.Worker/Nexo.Worker.csproj src/Nexo.Worker/
RUN dotnet restore src/${PROJECT}/${PROJECT}.csproj

COPY src/ src/
RUN dotnet publish src/${PROJECT}/${PROJECT}.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
ARG PROJECT
ENV ENTRY_DLL=${PROJECT}.dll \
    ASPNETCORE_HTTP_PORTS=8080 \
    Storage__Root=/data/files
WORKDIR /app

# A pasta dos zips precisa ser do usuário sem privilégios que roda o processo.
RUN mkdir -p /data/files && chown -R $APP_UID /data/files
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ENTRY_DLL\" \"$@\"", "--"]
