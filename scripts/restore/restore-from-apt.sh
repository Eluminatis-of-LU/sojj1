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