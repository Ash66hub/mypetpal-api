FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./mypetpal-api.csproj
RUN dotnet publish ./mypetpal-api.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install system dependencies required for Npgsql/PostgreSQL
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "mypetpal-api.dll"]