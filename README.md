# mypetpal-api

Backend API for the MyPetPal project (ASP.NET Core).

## Status
[![MyPetPal-API Status Check](https://github.com/Ash66hub/mypetpal-api/actions/workflows/keep-awake.yml/badge.svg)](https://github.com/Ash66hub/mypetpal-api/actions/workflows/keep-awake.yml)

## Disclaimer

This README.md was generated with AI assistance.

## Contact

For questions or support, contact: hi@aswanth.net

## License

This project is licensed under the MIT License. See the LICENSE file.

## Related repositories

- API repo (this project): https://github.com/Ash66hub/mypetpal-api
- UI repo (depends on this API): https://github.com/Ash66hub/mypetpal-ui

## Dependencies and run order

This API is the backend for the MyPetPal UI at https://github.com/Ash66hub/mypetpal-ui.

- API local URL: http://localhost:5050/
- UI local URL: http://localhost:4200/

For the full app to work end-to-end, build and run this API first, then build/run the UI repo so it can connect to this service.

## Run

1. Restore dependencies:
   dotnet restore
2. Build:
   dotnet build
3. Run:
   dotnet run

## Local environment setup

This repo ignores local app settings files that can contain secrets or machine-specific values.

1. Copy `appsettings.Development.example.json` to `appsettings.Development.json`.
2. Update local values (for example, connection string and secrets) in your copied file.

Tracked template:

- `appsettings.Development.example.json`

Ignored local files:

- `appsettings.Development.json`
- `appsettings.Local.json`
- `appsettings.*.local.json`
- `secrets.json`
