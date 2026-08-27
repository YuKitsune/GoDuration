# Changelog

All notable changes to this project are recorded in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-08-28

### Added

- `GoDuration.NodaTime` package. Reads and writes Go-style duration strings as `NodaTime.Duration`.
- `GoDurationReader` and `GoDurationWriter` public types. Read and write Go duration strings as raw `long` nanosecond counts, without going through `TimeSpan` or `NodaTime.Duration`.

### Changed

- The public parser and formatter type is renamed to `GoDurationConverter` in both packages. Was `Duration` in `GoDuration` and `GoDuration` in `GoDuration.NodaTime`.
- `GoDurationConverter.Parse` and `GoDurationConverter.Format` accept and produce values within Go's signed int64 nanosecond range (about +/-292 years).
- `GoDurationConverter.Format` throws `OverflowException` for `TimeSpan` values outside the accepted range.

### Removed

- `GoDuration.SystemTextJson` package.
- `GoDuration.NewtonsoftJson` package.
- `GoDuration.YamlDotNet` package.

## [0.2.0] - 2026-08-28

### Changed

- All packages target `netstandard2.0`, `net8.0`, and `net10.0`.
- `GoDuration` and `GoDuration.SystemTextJson` are AOT-compatible on `net8.0` and later.
- The release bundle zip contains one folder per target framework for each package.

## [0.1.0] - 2026-08-27

### Added

- `GoDuration` core package. Parses and formats Go-style duration strings.
- `GoDuration.SystemTextJson` package. `TimeSpan` converter for `System.Text.Json`.
- `GoDuration.NewtonsoftJson` package. `TimeSpan` converter for `Newtonsoft.Json`.
- `GoDuration.YamlDotNet` package. `TimeSpan` type converter for `YamlDotNet`.
- `DurationFormatOptions` for the format output. Options: `OmitZeroUnits`, `IncludePositiveSign`, and `MicrosecondSymbol`.
