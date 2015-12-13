FROM ubuntu:15.04

# Things to do:
# - mono (including CA certs)
# - nuget
# - git

# Install things we can install without setup

RUN apt-get update && apt-get install -y \
 python-software-properties \
 software-properties-common \
 nuget \
 nunit \
 git \
 joe \
 nano \
 sudo

# Install Mono and CA keys
RUN apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
RUN echo "deb http://download.mono-project.com/repo/debian wheezy main" | tee /etc/apt/sources.list.d/mono-xamarin.list

# Do the final installs now that all the setup is done
RUN apt-get update && apt-get install -y \
 mono-complete \
 ca-certificates-mono

# copy in the source folder
COPY source/ /tmp/source/

# compile it and copy the output to the /srv/graverobber directory
RUN \
  nuget restore /tmp/source/GraveRobber.sln && \
  xbuild /p:Configuration=Release /tmp/source/GraveRobber.sln && \
  mkdir -p /srv/graverobber && \
  cp /tmp/source/GraveRobber/bin/Release/* /srv/graverobber/
  
CMD ["mono", "/srv/graverobber/GraveRobber.exe"]
