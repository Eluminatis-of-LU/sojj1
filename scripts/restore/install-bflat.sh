#!/bin/bash

set -e

BFLAT_VERSION=7.0.2

wget https://github.com/bflattened/bflat/releases/download/v${BFLAT_VERSION}/bflat-${BFLAT_VERSION}-linux-glibc-x64.tar.gz

rm -rf /usr/local/bflat

mkdir -p /usr/local/bflat

tar -xzf bflat-${BFLAT_VERSION}-linux-glibc-x64.tar.gz -C /usr/local/bflat

rm -rf bflat-${BFLAT_VERSION}-linux-glibc-x64.tar.gz