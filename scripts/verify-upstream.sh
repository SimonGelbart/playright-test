#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
upstream_url="https://github.com/microsoft/dotnet-framework-docker.git"
commit="$(sed -n 's/^Commit: //p' "$repository_root/UPSTREAM_COMMIT.txt")"

if [[ -z "$commit" ]]; then
  echo "Could not find the upstream commit in UPSTREAM_COMMIT.txt" >&2
  exit 1
fi

temporary_root="$(mktemp -d)"
trap 'rm -rf "$temporary_root"' EXIT

git clone --quiet --filter=blob:none --no-checkout "$upstream_url" "$temporary_root/repository"
git -C "$temporary_root/repository" sparse-checkout init --cone
git -C "$temporary_root/repository" sparse-checkout set samples/aspnetapp
git -C "$temporary_root/repository" checkout --quiet "$commit"

upstream_root="$temporary_root/repository/samples/aspnetapp"
while IFS= read -r -d '' upstream_file; do
  relative_path="${upstream_file#"$upstream_root"/}"
  local_file="$repository_root/$relative_path"

  if [[ ! -f "$local_file" ]]; then
    echo "Missing upstream file: $relative_path" >&2
    exit 1
  fi

  if ! cmp --silent "$upstream_file" "$local_file"; then
    echo "Changed upstream file: $relative_path" >&2
    exit 1
  fi
done < <(find "$upstream_root" -type f -print0)

echo "Imported application matches upstream commit $commit."
