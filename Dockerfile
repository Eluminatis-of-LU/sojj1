#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0-noble AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src
COPY ["sojj/sojj.csproj", "sojj/"]
RUN dotnet restore "sojj/sojj.csproj"
COPY sojj/* sojj/
WORKDIR "/src/sojj"
RUN dotnet build "sojj.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "sojj.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS prepare-languages
WORKDIR /app

COPY ./scripts/restore/restore-from-apt.sh /root/scripts/restore/restore-from-apt.sh
RUN chmod +x /root/scripts/restore/restore-from-apt.sh
RUN /root/scripts/restore/restore-from-apt.sh

COPY ./scripts/restore /root/scripts/restore
RUN chmod +x /root/scripts/restore/*.sh

RUN /root/scripts/restore/restore.sh

FROM prepare-languages AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY ./entrypoint.sh /root/entrypoint.sh
COPY ./sandbox.sh /root/sandbox.sh
RUN chmod +x /root/entrypoint.sh
RUN chmod +x /root/sandbox.sh

EXPOSE 5050/tcp

ENV JUDGER_VERSION=$(git describe --tags --always --dirty)

ENTRYPOINT [ "/root/entrypoint.sh" ]
