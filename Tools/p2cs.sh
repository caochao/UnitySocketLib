#!/bin/bash
# Generates C# source files from .proto files.

PROTOC=./protoc/macosx_x64/protoc
OUTPUT_DIR=../Genrated
mkdir -p $OUTPUT_DIR
$PROTOC --proto_path=./protos --csharp_out=$OUTPUT_DIR pb.proto
