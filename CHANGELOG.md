# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased] [1.0.1] - 2024-09-16

### Added
- Command timeout configuration (not tested yet)
- New configuration options:
  - `CommandTimeout`: Default is 30 seconds
  - `EnableRequestBodyLogging`: Default is true
  - `EnableResponseBodyLogging`: Default is true

### Changed
- Updated README to include information about `Connect Timeout` in connection string
- Modified activity configuration structure in `appsettings.json`:
- 
```json
{
  "ActivityConfigurations": {
    "ConnectionString": "...",
    "CommandTimeout": 10,
    "EnableRequestBodyLogging": false,
    "EnableResponseBodyLogging": true,
    "Procedures": {
      "UspStoreActivity": "usp_StoreActivity",
      "UspUpdateActivity": "usp_UpdateActivity"
    }
  }
}
```
### TODO
- Finalize changes with Mohammed
- Test command timeout functionality
- Update documentation to reflect new configuration options

## [1.0.0] - 2024-09-02

### Added
- Initial release of the ActivityLogger library for ASP.NET Core.
- Middleware for logging API activity, capturing request and response details.
- Support for configurable stored procedures for logging activities:
  - `uspStoreActivity` for storing activity records.
  - `uspUpdateActivity` for updating existing activity records.
  - `uspGetActivities` for retrieving logged activities with filtering options.

### Configuration
- Added configuration options in `appsettings.json` for database connection and stored procedure names.
- Instructions for setting up the Activity table in the database.

### Usage
- Provided examples for integrating the middleware into an ASP.NET Core application.
- Documentation for adding the ActivityLogger DLL reference in projects.

### Changed
- Improved README documentation for clarity on setup and usage instructions.

### Fixed
- No known issues fixed in this release.

---

## Future Releases

- Planned enhancements for additional logging features and improved performance.