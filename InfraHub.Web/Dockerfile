FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore InfraHub.Web.csproj

# Copy everything else and build
COPY . ./
RUN dotnet publish InfraHub.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set timezone to Jakarta
RUN apt-get update \
    && apt-get install -y curl tzdata \
    && ln -fs /usr/share/zoneinfo/Asia/Jakarta /etc/localtime \
    && dpkg-reconfigure -f noninteractive tzdata \
    && rm -rf /var/lib/apt/lists/*

EXPOSE 80

ENV ASPNETCORE_HTTP_PORTS=80

WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InfraHub.Web.dll"]

