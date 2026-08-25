# FFGuardian third-party security engines

## Release policy

Engine versions are pinned in `engines.lock.json`. Release workflows must reject missing or malformed SHA-256 values. Version changes, URLs, sizes and hashes require a dedicated pull request and review. `latest` URLs are forbidden for Release builds.

## YARA

- Project: VirusTotal/yara
- Required version: 4.5.5
- Architecture: Windows x64
- Expected package: `yara-4.5.5-2368-win64.zip`
- Expected executables: `yara64.exe` or `yara.exe`
- Destination: `Engine/Yara`
- License: BSD-3-Clause
- Official origin: VirusTotal YARA GitHub Release v4.5.5
- SHA-256 status: **NOT YET APPROVED IN THE LOCK FILE**

A third-party checksum is not sufficient for a commercial Release. The package must be acquired from the official URL, hashed in a controlled environment and the digest approved through review.

## ClamAV and FreshClam

- Project: Cisco Talos ClamAV
- Required version: 1.5.3
- Architecture: Windows x64
- Expected package: `clamav-1.5.3.win.x64.zip`
- Scanner: `clamscan.exe`
- Updater: `freshclam.exe`
- Destination: `Engine/ClamAV`
- Database directory: `Engine/ClamAV/database`
- License: GPL-2.0-only
- Official origin: ClamAV production Windows download
- Detached signature: official `.zip.sig` URL recorded in the lock
- SHA-256 status: **NOT YET APPROVED IN THE LOCK FILE**

The Release pipeline must verify the pinned SHA-256 before extraction. Detached-signature verification can be added after the ClamAV public signing-key distribution and trust procedure are formally pinned.

## ClamAV signature database

Pull-request tests must use a controlled database artifact whose version, source, size and SHA-256 are recorded in `engines.lock.json`. Scheduled and Release workflows may run FreshClam in a dedicated job, preserve logs, record database versions, and publish a verified internal artifact for subsequent jobs.

The repository must not contain large signature databases or EICAR output artifacts.

## Required manual provenance approval

Before enabling mandatory runtime jobs, provide and review:

1. SHA-256 and exact byte size for the official YARA package.
2. SHA-256 for the official ClamAV package.
3. A controlled ClamAV database artifact or protected storage location, with SHA-256 and version.
4. The protected Release environment and signing-key identifier.
