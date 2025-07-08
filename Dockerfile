FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["GitMirror/GitMirror.csproj", "GitMirror/"]
RUN dotnet restore "GitMirror/GitMirror.csproj"
COPY . .
WORKDIR "/src/GitMirror"
RUN dotnet build "GitMirror.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GitMirror.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set non-interactive environment
ENV CI=true
ENV DEBIAN_FRONTEND=noninteractive

ENTRYPOINT ["dotnet", "GitMirror.dll"]