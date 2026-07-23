# AuditAgent - Sistema de Auditoria de Software Corporativo

Sistema completo para auditar equipos empresariales: inventario de hardware,
software instalado, parches de seguridad y red. Desarrollado en C# / .NET 8
con seguridad como prioridad.

## Arquitectura

```
+------------------------------------------+
|        Agente de Escritorio (C#)         |
|  - WMI: hardware, SO, red, parches       |
|  - Registry: software instalado           |
|  - Firma RSA-4096 del reporte             |
|  - Cifrado AES-256-GCM                    |
|  - Publicacion: EXE unico (~15MB)         |
+--------------------+---------------------+
                     | HTTPS + mTLS (TLS 1.3)
                     v
+------------------------------------------+
|         Servidor Central (API)           |
|  - ASP.NET Core 8 Web API                |
|  - Recepcion y validacion de reportes     |
|  - Verificacion de firma digital           |
|  - Descifrado con clave maestra           |
|  - Almacenamiento persistente             |
|  - Dashboard (futuro)                     |
+------------------------------------------+
```

## Proyectos

| Proyecto | Descripcion |
|----------|-------------|
| `AuditAgent.Core` | Modelos, interfaces y orquestador de auditoria |
| `AuditAgent.Collectors` | Recolectores WMI + Registry (solo Windows) |
| `AuditAgent.Security` | AES-256-GCM, RSA-4096, certificados X.509, mTLS |
| `AuditAgent.Agent` | Agente principal (EXE con manifest de admin) |
| `AuditAgent.CLI` | Herramienta CLI standalone para auditorias puntuales |
| `AuditAgent.Api` | Servidor central REST API |
| `AuditAgent.Tests` | Tests unitarios (xUnit) |

## Requisitos

- **Para compilar**: .NET 8 SDK
- **Para ejecutar el agente**: Windows 10/11 (no requiere .NET instalado)
- **Para el servidor**: .NET 8 Runtime o Docker

## Compilacion

```bash
# Restaurar dependencias
.dotnet restore

# Compilar todo
dotnet build

# Ejecutar tests
dotnet test

# Publicizar agente como EXE unico
dotnet publish src/AuditAgent.Agent -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# Publicizar CLI
dotnet publish src/AuditAgent.CLI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Uso del Agente

### Auditoria rapida (sin enviar al servidor)

```cmd
# Ejecutar como administrador
AuditAgent.Agent.exe
```

Esto genera un reporte JSON en `./reports/` con toda la informacion del equipo.

### Opciones de linea de comandos

```cmd
AuditAgent.Agent.exe --send          # Enviar al servidor central
AuditAgent.Agent.exe --use-wmi       # Tambien usar WMI Win32_Product (lento)
AuditAgent.Agent.exe --no-updates    # Excluir actualizaciones de Windows
```

### Uso del CLI (herramienta ligera)

```cmd
# Auditoria basica con salida JSON
AuditAgent.CLI.exe

# Con ruta personalizada y exportacion CSV
AuditAgent.CLI.exe --output C:\auditoria\pc-001.json --csv

# Solo software (sin actualizaciones), modo silencioso
AuditAgent.CLI.exe --no-updates --quiet
```

## Informacion que recolecta

### Sistema (WMI Win32_ComputerSystem + Win32_BIOS)
- Nombre del equipo, fabricante, modelo, numero de serie
- Asset tag, UUID, dominio, usuario actual

### Hardware (WMI multiples clases)
- **CPU**: Nombre, cores, hilos, velocidad, socket
- **RAM**: Modulos individuales (fabricante, capacidad, tipo DDR, velocidad)
- **Discos**: Modelo, tipo (SSD/HDD), interfaz (SATA/NVMe), particiones con espacio libre
- **GPU**: Nombre, driver, VRAM
- **Bateria**: Estado, capacidad (solo laptops)
- **BIOS/Motherboard**: Version, fabricante, fecha

### Software (Registry + opcional WMI)
- Todos los programas instalados (x64 y x86)
- Nombre, version, fabricante, fecha de instalacion
- Tamano estimado, ruta de instalacion, arquitectura
- Filtro configurable para excluir actualizaciones

### Sistema Operativo (WMI Win32_OperatingSystem)
- Version, build, arquitectura, organizacion
- Fecha de instalacion, ultimo arranque

### Parches de Seguridad (WMI Win32_QuickFixEngineering)
- Lista completa de KBs instalados
- Fecha y usuario que instalo cada parche

### Red (WMI Win32_NetworkAdapterConfiguration)
- Adaptadores activos con IP, MAC, DNS, gateway
- Estado de DHCP, sufijo DNS

## Seguridad

### Cifrado AES-256-GCM
- Cifrado autenticado (confidencialidad + integridad en uno)
- Nonce aleatorio unico por cifrado
- Clave de 256 bits con PBKDF2 para derivacion

### Firma Digital RSA-4096
- Cada reporte se firma con la clave privada del agente
- SHA-256 como algoritmo de hash
- No repudio: el agente no puede negar haber enviado el reporte

### Comunicacion mTLS
- TLS 1.3 obligatorio (fallback a 1.2)
- Autenticacion mutua con certificados X.509
- Whitelist de servidores permitidos
- Timeouts estrictos (10s conexion, 60s operacion)

### Proteccion del agente
- Manifest UAC: requiere administrador
- Publicacion como Single File EXE (difficil de decompilar)
- Claves RSA generadas y almacenadas localmente
- Para produccion: ofuscar con ConfuserEx o Dotfuscator
- Para produccion: firmar el EXE con Authenticode

## Configuracion del Servidor

### Opcion 1: Docker

```bash
# Generar certificados (primera vez)
dotnet run --project src/AuditAgent.Api --generate-certs

# Levantar servidor
docker compose up -d
```

### Opcion 2: Ejecutar directamente

```bash
dotnet run --project src/AuditAgent.Api
```

El servidor escucha en `https://localhost:8443`.

## Estructura de Archivos

```
AuditAgent/
+-- AuditAgent.sln
+-- Dockerfile
+-- docker-compose.yml
+-- src/
|   +-- AuditAgent.Core/          # Modelos e interfaces compartidos
|   +-- AuditAgent.Collectors/    # Recolectores WMI + Registry
|   +-- AuditAgent.Security/      # Cifrado, firmas, certificados, mTLS
|   +-- AuditAgent.Agent/          # Agente principal (EXE)
|   +-- AuditAgent.Api/            # Servidor central REST API
|   +-- AuditAgent.CLI/            # Herramienta CLI ligera
+-- tests/
|   +-- AuditAgent.Tests/          # Tests unitarios
+-- keys/                          # Claves RSA (generadas al ejecutar)
+-- certs/                         # Certificados X.509
+-- reports/                       # Reportes de auditoria generados
+-- data/                          # Datos del servidor (API)
```

## Mejoras futuras

- [ ] Servicio de Windows automatico (TopShelf o Worker Service)
- [ ] Despliegue via GPO (Group Policy)
- [ ] Dashboard web React/Next.js para visualizar reportes
- [ ] Base de datos SQL Server/PostgreSQL en el servidor
- [ ] Comparacion de licencias de software
- [ ] Deteccion de software no autorizado
- [ ] Integracion con Active Directory
- [ ] Notificaciones por email

## Licencia

Proyecto privado. Uso corporativo interno.
