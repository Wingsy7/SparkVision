FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SparkVision.Data/SparkVision.Data.csproj SparkVision.Data/
COPY SparkVision.Api/SparkVision.Api.csproj SparkVision.Api/
RUN dotnet restore SparkVision.Api/SparkVision.Api.csproj

COPY SparkVision.Data/ SparkVision.Data/
COPY SparkVision.Api/ SparkVision.Api/
COPY SparkVision.WinForms/Data/ SparkVision.WinForms/Data/
RUN dotnet publish SparkVision.Api/SparkVision.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.8 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SparkVision.Api.dll"]
