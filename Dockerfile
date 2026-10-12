FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]

COPY ["My.XXX.APIs/01My.XXX.APIs.csproj", "My.XXX.APIs/"]
COPY ["My.XXX.Application/02My.XXX.Application.csproj", "My.XXX.Application/"]
COPY ["My.XXX.Domain/03My.XXX.Domain.csproj", "My.XXX.Domain/"]
COPY ["My.XXX.Persistence/04My.XXX.Persistence.csproj", "My.XXX.Persistence/"]
COPY ["My.XXX.Infrastructure/05My.XXX.Infrastructure.csproj", "My.XXX.Infrastructure/"]

RUN dotnet restore "My.XXX.APIs/01My.XXX.APIs.csproj"

COPY . .

WORKDIR "/src/My.XXX.APIs"

RUN dotnet build "01My.XXX.APIs.csproj" -c Release -o /app/build

FROM build AS publish

RUN dotnet publish "01My.XXX.APIs.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base

RUN apk add --no-cache icu-libs icu-data-full tzdata

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

WORKDIR /app

COPY --from=publish /app/publish .

RUN chown -R app:app /app \
	&& chmod -R 755 /app

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "My.XXX.APIs.dll"]
