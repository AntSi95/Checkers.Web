FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

ARG ENGINE_PATH=src/Checkers.Engine/Checkers
ARG TEST_ENGINE_PATH=src/Checkers.Engine/Tests

COPY Checkers.Web/*.csproj ./Checkers.Web/
COPY ${ENGINE_PATH}/*.csproj ./${ENGINE_PATH}/
# COPY ${TEST_ENGINE_PATH}/*.csproj ./${TEST_ENGINE_PATH}/
RUN dotnet restore Checkers.Web/Checkers.Web.csproj

COPY . ./

# Прогон тестов
# RUN dotnet test Checkers.Engine.Tests/*.csproj -c Release --no-restore

RUN dotnet publish Checkers.Web/*.csproj -c Release -o /release

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /release .
EXPOSE 8080

ENTRYPOINT ["dotnet", "Checkers.Web.dll"]