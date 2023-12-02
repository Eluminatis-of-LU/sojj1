#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0-jammy AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0-jammy AS build
WORKDIR /src
COPY ["sojj/sojj.csproj", "sojj/"]
RUN dotnet restore "sojj/sojj.csproj"
COPY . .
WORKDIR "/src/sojj"
RUN dotnet build "sojj.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "sojj.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY ./scripts /root/scripts

RUN chmod +x /root/scripts/*.sh

RUN /root/scripts/restore.sh

COPY ./entrypoint.sh /root/entrypoint.sh
RUN chmod +x /root/entrypoint.sh

EXPOSE 5050/tcp

ENTRYPOINT [ "/root/entrypoint.sh" ]