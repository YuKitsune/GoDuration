# Changelog

All notable changes to this project are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- All packages target `netstandard2.0`, `net8.0`, and `net10.0`.
- `GoDuration` and `GoDuration.SystemTextJson` are AOT-compatible on `net8.0`
  and later.
- The release bundle zip contains one folder per target framework for each
  package.

## [0.1.0] - 2026-08-27

### Added

- `GoDuration` core package. Parses and formats Go-style duration strings.
- `GoDuration.SystemTextJson` package. `TimeSpan` converter for `System.Text.Json`.
- `GoDuration.NewtonsoftJson` package. `TimeSpan` converter for `Newtonsoft.Json`.
- `GoDuration.YamlDotNet` package. `TimeSpan` type converter for `YamlDotNet`.
- `DurationFormatOptions` for the format output. Options: `OmitZeroUnits`,
  `IncludePositiveSign`, and `MicrosecondSymbol`.
