# Imagen base de labs Linux (basico) - toolchain del curso, usuario no-root con sudo sin password
FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends \
    bash coreutils sudo ca-certificates curl jq \
    man-db less nano vim-tiny \
    procps psmisc iproute2 iputils-ping dnsutils \
    grep sed gawk findutils gzip tar file \
    && rm -rf /var/lib/apt/lists/*

RUN useradd -m -s /bin/bash lab \
    && echo "lab ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/lab \
    && chmod 440 /etc/sudoers.d/lab

WORKDIR /home/lab
# el runtime crea el contenedor con "sleep infinity"; el usuario entra por exec (bash -l)
CMD ["sleep", "infinity"]
