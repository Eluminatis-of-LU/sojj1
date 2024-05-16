#!/bin/bash

set -e

GO_JUDGE_VERSION=1.8.4

wget https://github.com/criyle/go-judge/releases/download/v${GO_JUDGE_VERSION}/go-judge_${GO_JUDGE_VERSION}_linux_amd64 -O /usr/bin/sandbox && chmod +x /usr/bin/sandbox