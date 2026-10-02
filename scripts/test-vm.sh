#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
vagrant_directory="${VAGRANT_DIRECTORY:-$HOME/tools/windows-docker-machine}"
machine="${WINDOWS_DOCKER_MACHINE:-2022-box}"
remote_root="C:/Users/vagrant/legacy-web"

if [[ ! -d "$vagrant_directory" ]]; then
  echo "Vagrant directory not found: $vagrant_directory" >&2
  echo "Set VAGRANT_DIRECTORY to the windows-docker-machine checkout." >&2
  exit 1
fi

cd "$vagrant_directory"
vagrant upload "$repository_root" "$remote_root" "$machine"
vagrant winrm "$machine" \
  --shell powershell \
  --command 'Set-Location C:\Users\vagrant\legacy-web; powershell.exe -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Clean'
