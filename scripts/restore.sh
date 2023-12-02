#!/bin/sh

apt-get update && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
		ca-certificates \
		gcc \
		wget \
		g++ \
		python3 \
		mono-runtime \
		mono-mcs \
		mono-devel \
		nodejs \
		openjdk-8-jdk-headless


wget https://github.com/criyle/go-judge/releases/download/v1.8.0/go-judge_1.8.0_linux_amd64 -O /usr/bin/sandbox && chmod +x /usr/bin/sandbox

wget https://github.com/bflattened/bflat/releases/download/v8.0.0/bflat-8.0.0-linux-glibc-x64.tar.gz

rm -rf /opt/bflat

mkdir -p /opt/bflat

tar -xzf bflat-8.0.0-linux-glibc-x64.tar.gz -C /opt/bflat

rm -rf bflat-8.0.0-linux-glibc-x64.tar.gz