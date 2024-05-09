#!/bin/bash

set -e

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
		openjdk-8-jdk-headless \
		unzip

BFLAT_VERSION=7.0.2

wget https://github.com/bflattened/bflat/releases/download/v${BFLAT_VERSION}/bflat-${BFLAT_VERSION}-linux-glibc-x64.tar.gz

rm -rf /usr/local/bflat

mkdir -p /usr/local/bflat

tar -xzf bflat-${BFLAT_VERSION}-linux-glibc-x64.tar.gz -C /usr/local/bflat

rm -rf bflat-${BFLAT_VERSION}-linux-glibc-x64.tar.gz

GO_JUDGE_VERSION=1.8.4

wget https://github.com/criyle/go-judge/releases/download/v${GO_JUDGE_VERSION}/go-judge_${GO_JUDGE_VERSION}_linux_amd64 -O /usr/bin/sandbox && chmod +x /usr/bin/sandbox

KOTLIN_VERSION=1.9.24

wget https://github.com/JetBrains/kotlin/releases/download/v${KOTLIN_VERSION}/kotlin-compiler-${KOTLIN_VERSION}.zip

unzip kotlin-compiler-${KOTLIN_VERSION}.zip -d ./tmp_kotlinc

cp -r ./tmp_kotlinc/kotlinc/bin /usr/local/
cp -r ./tmp_kotlinc/kotlinc/lib /usr/local/

rm -rf ./tmp_kotlinc
rm -rf kotlin-compiler-${KOTLIN_VERSION}.zip