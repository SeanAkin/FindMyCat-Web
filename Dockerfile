FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend

COPY ["frontend/package.json", "frontend/package-lock.json", "./"]
RUN npm ci

COPY frontend/ .
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["FindMyCat.Api/FindMyCat.Api.csproj", "FindMyCat.Api/"]
COPY ["FindMyCat.Core/FindMyCat.Core.csproj", "FindMyCat.Core/"]
COPY ["FindMyCat.Data/FindMyCat.Data.csproj", "FindMyCat.Data/"]

RUN dotnet restore "FindMyCat.Api/FindMyCat.Api.csproj"

COPY FindMyCat.Api/ FindMyCat.Api/
COPY FindMyCat.Core/ FindMyCat.Core/
COPY FindMyCat.Data/ FindMyCat.Data/

WORKDIR "/src/FindMyCat.Api"
RUN dotnet publish "FindMyCat.Api.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .
COPY --from=frontend-build /src/frontend/dist ./wwwroot

EXPOSE 8080

ENTRYPOINT ["dotnet", "FindMyCat.Api.dll"]
