# Telechron .NET Toolchain image (R-DM14, R-SYS9).
# Pinned by digest — see containers/README.md for the provenance record.
# Used for build/test/verify commands issued by the "dotnet" Toolchain module (Phase 6).
FROM mcr.microsoft.com/dotnet/sdk@sha256:72b2c1fba104eed0765e76c66256dd57b8b00c5e7c7fd16ad3eb254ad18db3fc

WORKDIR /workspace

# No implicit network access beyond what R-SYS7's egress allowlist grants at container-run time
# (e.g. NuGet restore endpoints). This image declares no default entrypoint — the Agent supplies
# the build/test/verify command per invocation.
