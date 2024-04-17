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
		golang \
		openjdk-8-jdk-headless

BFLAT_VERSION=7.0.2

wget https://github.com/bflattened/bflat/releases/download/v$BFLAT_VERSION/bflat-$BFLAT_VERSION-linux-glibc-x64.tar.gz

rm -rf /usr/local/bflat

mkdir -p /usr/local/bflat

tar -xzf bflat-$BFLAT_VERSION-linux-glibc-x64.tar.gz -C /usr/local/bflat

rm -rf bflat-$BFLAT_VERSION-linux-glibc-x64.tar.gz

GO_JUDGE_VERSION=1.8.3

wget https://github.com/criyle/go-judge/releases/download/v$GO_JUDGE_VERSION/go-judge_$GO_JUDGE_VERSION_linux_amd64 -O /usr/bin/sandbox && chmod +x /usr/bin/sandbox
