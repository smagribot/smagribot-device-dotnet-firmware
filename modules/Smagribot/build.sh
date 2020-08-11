#!/bin/sh

_runtime=$1
_outputpath=$2
_version=$3
_docker_repo=${4:-smagribot/devicerunner}

if [ $1 == "-h" ] || [ $1 == "--help" ] || [ $1 == "help" ]; then
    echo "Script for building Smagribot device runner and creating docker image"
    echo ""
    echo "Removes the out path. Then restores packages, builds and publishs project for specified runtime. Finally builds docker image with specified"
    echo ""
    echo ""
    echo "Usage:"
    echo "\t./build.sh [RUNTIME] [OUTPATH] [VERSION] [TAG]"
    echo ""
    echo "Common commands:"
    echo "\t./build.sh linux-arm ./out 1.0.0 smagribot/devicerunner"
    echo "\t./build.sh linux-x64 ./out 1.0.0 smagribot/devicerunner"
    echo ""
    echo "RUNTIME:\tlinux-arm or linux-x64 supported"
    echo "OUTPATH:\tpath for output, ex. ./out"
    echo "VERSION:\tversion part of docker image tag, ex. 1.0.0"
    echo "TAG:\t\t(optional) repo part of docker image tag, ex. smagribot/devicerunner"
    exit 0
fi

if [ -z "$_runtime" ]
then
   echo "No runtime specified. Use: ./build.sh runtime outputpath version [docker_repo]"
   echo "Ex.: ./build.sh linux-arm ./out 1.0.0 smagribot/devicerunner"
   exit 3 
fi

if [ -z "$_outputpath" ]
then
   echo "No outputpath specified. Use: ./build.sh runtime outputpath version [docker_repo]"
   echo "Ex.: ./build.sh linux-arm ./out 1.0.0 smagribot/devicerunner"
   exit 3 
fi

if [ -z "$_version" ]
then
   echo "No version specified. Use: ./build.sh runtime outputpath version [docker_repo]"
   echo "Ex.: ./build.sh linux-arm ./out 1.0.0 smagribot/devicerunner"
   exit 3 
fi

rm -rf _outputpath

dotnet restore -r $_runtime
dotnet build -r $_runtime -c Release --no-restore
dotnet publish -r $_runtime -c Release -o $_outputpath --no-build

case "$_runtime" in
  linux-arm)   _arch=arm32v7 ;;
  linux-x64)   _arch=amd64   ;;
esac

if [ -z "$_arch" ]
then
   echo "Unsupported runtime!"
   echo "Use linux-arm or linux-x64"
   exit 3 
fi

docker build \
    --pull \
    --build-arg SMAGRIBOT_OUTPATH=$_outputpath \
    -f Dockerfile."$_arch" \
    -t "$_docker_repo":"$_version"-"$_arch" .