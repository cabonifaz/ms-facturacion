# ms-facturacion

Microservicio de facturación construido con **.NET 10 (ASP.NET Core Web API)**.

## Requisitos previos

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) o superior
- Git

Verificá tu versión instalada:

```bash
dotnet --version
```

## Cómo levantar el proyecto en local

### Windows (PowerShell)

```powershell
# Clonar el repositorio (si todavía no lo tienes)
git clone https://github.com/cabonifaz/ms-facturacion.git
cd ms-facturacion

# Restaurar dependencias
dotnet restore

# Levantar el proyecto (perfil https por defecto)
dotnet run --project ms-facturacion
```

### Linux / macOS (bash)

```bash
# Clonar el repositorio (si todavía no lo tienes)
git clone https://github.com/cabonifaz/ms-facturacion.git
cd ms-facturacion

# Restaurar dependencias
dotnet restore

# Levantar el proyecto
dotnet run --project ms-facturacion
```

Ambos comandos son idénticos porque el CLI de `dotnet` es multiplataforma; lo único que cambia es la terminal que uses.

## URLs disponibles en desarrollo

Al levantar el proyecto con el perfil `https` (por defecto), quedan disponibles:

- API: `https://localhost:7164` / `http://localhost:5221`
- Swagger UI: `https://localhost:7164/swagger`

Si prefieres forzar un perfil específico:

```bash
dotnet run --project ms-facturacion --launch-profile http
dotnet run --project ms-facturacion --launch-profile https
```

### Visual Studio

1. Abre `ms-facturacion.sln`.
2. Selecciona el perfil `https` (o `http`) en el dropdown de ejecución, al lado del botón verde de Play.
3. Presioná **F5** (con debugger) o **Ctrl+F5** (sin debugger).

Visual Studio va a levantar el proyecto y abrir el navegador en Swagger automáticamente (`launchBrowser: true` en `launchSettings.json`).

## Hot reload (watch)

Para que el proyecto se reinicie automáticamente al guardar cambios:

```bash
dotnet watch --project ms-facturacion run
```

## Compilar y testear

```bash
dotnet build
dotnet test
```

## Estructura del proyecto

```
ms-facturacion/
├── ms-facturacion.sln          # Solución
└── ms-facturacion/             # Proyecto Web API
    ├── Controllers/
    ├── Program.cs               # Entry point
    ├── appsettings.json
    └── appsettings.Development.json
```
