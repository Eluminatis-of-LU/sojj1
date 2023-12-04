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
		ruby \
		openjdk-8-jdk-headless

wget https://github.com/bflattened/bflat/releases/download/v7.0.2/bflat-7.0.2-linux-glibc-x64.tar.gz

rm -rf /usr/local/bflat

mkdir -p /usr/local/bflat

tar -xzf bflat-7.0.2-linux-glibc-x64.tar.gz -C /usr/local/bflat

rm -rf bflat-7.0.2-linux-glibc-x64.tar.gz

wget https://github.com/criyle/go-judge/releases/download/v1.8.0/go-judge_1.8.0_linux_amd64 -O /usr/bin/sandbox && chmod +x /usr/bin/sandbox