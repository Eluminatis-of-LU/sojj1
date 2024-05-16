#!/bin/bash

set -e

KOTLIN_VERSION=1.9.24

wget https://github.com/JetBrains/kotlin/releases/download/v${KOTLIN_VERSION}/kotlin-compiler-${KOTLIN_VERSION}.zip

unzip kotlin-compiler-${KOTLIN_VERSION}.zip -d ./tmp_kotlinc

cp -r ./tmp_kotlinc/kotlinc/bin /usr/local/
cp -r ./tmp_kotlinc/kotlinc/lib /usr/local/

rm -rf ./tmp_kotlinc
rm -rf kotlin-compiler-${KOTLIN_VERSION}.zip